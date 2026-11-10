using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryGroundEffects;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Owns original movement effect graphs for one battle scene.</summary>
    public sealed class OfficialBattleMovementEffects : IDisposable
    {
        [Serializable] private sealed class Data { public string manifest; public Record[] skills, actions; }
        [Serializable] private sealed class Record { public int id, followNodes; public string resource; }
        private sealed class Playback
        {
            public int UnitId;
            public double Start, End;
            public Record Record;
            public GameObject Source, Target, Instance;
            public MyPlayableSequence Sequence;
            public bool Started, Stopped;
        }
        private readonly Dictionary<int, Record> skills = new Dictionary<int, Record>();
        private readonly Dictionary<int, Record> actions = new Dictionary<int, Record>();
        private readonly Dictionary<int, AttachAgent> ownedAttachments = new Dictionary<int, AttachAgent>();
        private readonly List<Playback> playbacks = new List<Playback>();
        private readonly MyPlayableCenter center = new MyPlayableCenter();
        private readonly IPlayableGroundEffectHost groundEffects;
        private readonly Transform parent;
        private readonly Action<double> advanceSources;
        private double clock;
        private bool disposed;

        public OfficialBattleMovementEffects(Transform parent, IPlayableGroundEffectHost groundEffects,
            Action<double> advanceSources = null)
        {
            this.parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            this.groundEffects = groundEffects ?? throw new ArgumentNullException(nameof(groundEffects));
            this.advanceSources = advanceSources;
            var asset = Resources.Load<TextAsset>("Combat/movement-effects");
            if (asset == null) throw new InvalidOperationException("Missing original movement effect bindings.");
            var data = JsonUtility.FromJson<Data>(asset.text);
            if (data?.manifest != "884_156692" || data.skills == null || data.actions == null)
                throw new InvalidOperationException("Invalid original movement effect bindings.");
            foreach (var record in data.skills) skills.Add(record.id, record);
            foreach (var record in data.actions) actions.Add(record.id, record);
        }

        // One skill sequence per movement; a separate action sequence for each
        // actual path cell. Skill effects finish naturally, as MyFightSkill does;
        // action effects stop at their cell boundary, as MyFightActionSkillMove does.
        public void Schedule(int unitId, Transform source, FightBattleStepEvent step,
            Func<FightGridPoint, Vector3> gridToWorld,
            Func<FightBattleMovementEvent, bool> nativeOwnsAction = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleMovementEffects));
            if (step?.Movements == null) return;
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                if (playbacks[i].UnitId != unitId || double.IsPositiveInfinity(playbacks[i].End)) continue;
                Release(playbacks[i]); playbacks.RemoveAt(i);
            }
            double cursor = 0;
            foreach (var movement in step.Movements)
            {
                if (movement == null || movement.UnitId != unitId || movement.Path == null || movement.Path.Count < 2) continue;
                cursor = Math.Max(cursor, Math.Max(0f, movement.Delay));
                if (skills.TryGetValue(movement.MoveSkillEntityId, out var skill))
                    Enqueue(unitId, source, skill, cursor, -1f, gridToWorld(movement.Path[movement.Path.Count - 1]));
                actions.TryGetValue(movement.MoveBehaviourId, out var action);
                if (nativeOwnsAction?.Invoke(movement) == true) action = null;
                for (int i = 1; i < movement.Path.Count; i++)
                {
                    float duration = movement.MoveDuration >= 0f ? movement.MoveDuration : movement.MoveSpeed > 0f
                        ? Vector3.Distance(gridToWorld(movement.Path[i - 1]), gridToWorld(movement.Path[i])) / movement.MoveSpeed : .5f;
                    if (action != null) Enqueue(unitId, source, action, cursor, duration, gridToWorld(movement.Path[i]));
                    cursor += duration;
                }
            }
            StartDue();
        }

        private void Enqueue(int unitId, Transform source, Record record, double start, float duration, Vector3 targetPosition)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureAttachment(unitId, source);
            var target = new GameObject("Movement effect target " + unitId);
            target.transform.SetParent(parent, false);
            target.transform.position = targetPosition;
            playbacks.Add(new Playback { UnitId = unitId, Source = source.gameObject, Target = target,
                Record = record, Start = clock + start, End = duration < 0f ? double.PositiveInfinity : clock + start + duration });
        }

        private void EnsureAttachment(int unitId, Transform source)
        {
            var agent = source.GetComponent<AttachAgent>();
            if (agent != null)
            {
                if (agent.GetBone("body") == null)
                    throw new InvalidOperationException("Original entity body attachment is missing: " + unitId);
                return;
            }
            var body = FindBodyAttachment(source);
            if (body == null) throw new InvalidOperationException("Original entity body attachment is missing: " + unitId);
            agent = source.gameObject.AddComponent<AttachAgent>();
            agent.myBoneNameList.Add("body"); agent.myBoneList.Add(body); agent.Init();
            ownedAttachments.Add(unitId, agent);
        }

        private static Transform FindBodyAttachment(Transform source)
        {
            Transform body = source.Find("body");
            if (body != null) return body;
            foreach (Transform child in source.GetComponentsInChildren<Transform>(true))
            {
                if (child == source || child.name != "body") continue;
                if (body != null)
                    throw new InvalidOperationException("Original entity has ambiguous body attachments: " + source.name);
                body = child;
            }
            return body;
        }

        private void StartDue()
        {
            foreach (var playback in playbacks)
            {
                if (playback.Started || clock < playback.Start) continue;
                playback.Started = true;
                var prefab = Resources.Load<GameObject>(playback.Record.resource);
                if (prefab == null) throw new InvalidOperationException("Missing native movement graph: " + playback.Record.resource);
                playback.Instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                playback.Sequence = playback.Instance.GetComponent<MyPlayableSequence>();
                if (playback.Sequence == null) throw new InvalidOperationException("Original movement sequence is unbound.");
                foreach (var playable in playback.Instance.GetComponentsInChildren<MyPlayable>(true))
                    playable.MyPlayableCenter = center;
                foreach (var apply in playback.Instance.GetComponentsInChildren<ApplyByGroundType>(true))
                    apply.Host = groundEffects;
                playback.Sequence.Init(playback.Source, playback.Target);
                playback.Sequence.TryPlay();
            }
        }

        public void Tick(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleMovementEffects));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime <= 0f) return;
            double end = clock + deltaTime;
            DispatchDue();
            while (clock < end)
            {
                double next = end;
                foreach (var playback in playbacks)
                {
                    double boundary = !playback.Started ? playback.Start
                        : !playback.Stopped ? playback.End : double.PositiveInfinity;
                    if (boundary > clock && boundary < next) next = boundary;
                }
                // Deliver the old cell's final interval before StopMove/next Play.
                // Original MyPlayable.ProcessFrame still owns its creation-frame guard.
                advanceSources?.Invoke(next - clock);
                center.Update((float)(next - clock)); center.LateUpdate();
                clock = next;
                DispatchDue();
            }
        }

        private bool StopDue()
        {
            bool stopped = false;
            foreach (var playback in playbacks)
            {
                if (playback.Stopped || !playback.Started || clock < playback.End) continue;
                playback.Sequence?.Stop(); playback.Stopped = true; stopped = true;
            }
            return stopped;
        }

        private void DispatchDue()
        {
            if (StopDue()) FlushStops();
            StartDue();
            if (StopDue()) FlushStops(); // A zero-duration cell still plays its original time-zero leaves.
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                var playback = playbacks[i];
                if (!playback.Started || playback.Sequence != null && playback.Sequence.myStatus != MyPlayable.PlayableStatus.Stop) continue;
                Release(playback); playbacks.RemoveAt(i);
            }
        }

        private void FlushStops()
        {
            // Original Stop requests are consumed by PostProcessFrame. Flush them
            // before the next cell so no stopped sequence receives another interval.
            center.Update(0f); center.LateUpdate();
        }

        public void RemoveUnit(int unitId)
        {
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                if (playbacks[i].UnitId != unitId) continue;
                Release(playbacks[i]); playbacks.RemoveAt(i);
            }
            if (ownedAttachments.TryGetValue(unitId, out var agent))
                OfficialBattleSceneBindings.DestroyCreatedObject(agent);
            ownedAttachments.Remove(unitId);
        }

        private static void Release(Playback playback)
        {
            playback.Sequence?.Remove();
            if (playback.Instance != null) OfficialBattleSceneBindings.DestroyCreatedObject(playback.Instance);
            if (playback.Target != null) OfficialBattleSceneBindings.DestroyCreatedObject(playback.Target);
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var playback in playbacks) Release(playback);
            playbacks.Clear(); center.RemoveAll();
            foreach (var agent in ownedAttachments.Values)
                if (agent != null) OfficialBattleSceneBindings.DestroyCreatedObject(agent);
            ownedAttachments.Clear(); disposed = true;
        }
    }
}
