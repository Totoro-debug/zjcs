using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // MyFightSkill starts one sequence; MyFightActionSkillMove starts/stops a
    // separate sequence at every cell. Keep both owners instead of one run alias.
    public sealed class OfficialBattleMovementAnimationCatalog
    {
        [Serializable] private sealed class Data { public string manifest; public Record[] skills, actions; }
        [Serializable] private sealed class Record { public int id; public OfficialBattleSkillPlayableCue[] playables; }
        private readonly Dictionary<int, Record> skills = new Dictionary<int, Record>();
        private readonly Dictionary<int, Record> actions = new Dictionary<int, Record>();

        public OfficialBattleMovementAnimationCatalog()
        {
            var asset = Resources.Load<TextAsset>("Combat/move-animators");
            if (asset == null) throw new InvalidOperationException("Original movement Animator bindings are missing.");
            Data data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.manifest != "884_156692" || data.skills == null || data.actions == null)
                throw new InvalidOperationException("Original movement Animator bindings have the wrong source.");
            foreach (Record record in data.skills) skills.Add(record.id, record);
            foreach (Record record in data.actions) actions.Add(record.id, record);
        }

        public Dictionary<FightBattleMovementEvent, List<OfficialBattleAnimationCue>> Build(
            FightBattleStepEvent step, Func<FightGridPoint, Vector3> gridToWorld)
        {
            var result = new Dictionary<FightBattleMovementEvent, List<OfficialBattleAnimationCue>>();
            if (step?.Movements == null) return result;
            var cursors = new Dictionary<int, float>();
            foreach (FightBattleMovementEvent movement in step.Movements)
            {
                if (movement == null || movement.Path == null || movement.Path.Count < 2) continue;
                cursors.TryGetValue(movement.UnitId, out float cursor);
                cursor = Mathf.Max(cursor, Mathf.Max(0f, movement.Delay));
                var cues = new List<OfficialBattleAnimationCue>();
                if (skills.TryGetValue(movement.MoveSkillEntityId, out Record skill))
                    Append(cues, skill, movement.UnitId, cursor, -1f);
                actions.TryGetValue(movement.MoveBehaviourId, out Record action);
                for (int i = 1; i < movement.Path.Count; i++)
                {
                    float duration = movement.MoveDuration >= 0 ? movement.MoveDuration
                        : movement.MoveSpeed > 0
                            ? Vector3.Distance(gridToWorld(movement.Path[i - 1]), gridToWorld(movement.Path[i])) / movement.MoveSpeed
                            : .5f;
                    if (action != null) Append(cues, action, movement.UnitId, cursor, duration);
                    cursor += duration;
                }
                cursors[movement.UnitId] = cursor;
                if (cues.Count > 0) result.Add(movement, cues);
            }
            return result;
        }

        private static void Append(List<OfficialBattleAnimationCue> cues, Record record,
            int unitId, float start, float ownerDuration)
        {
            foreach (OfficialBattleSkillPlayableCue playable in record.playables)
            {
                if (ownerDuration >= 0f && playable.time >= ownerDuration) continue;
                cues.Add(new OfficialBattleAnimationCue
                {
                    UnitId = unitId, Delay = start + playable.time, Priority = 0,
                    SkillPlayable = playable,
                    StopAfter = ownerDuration < 0f ? -1f : ownerDuration - playable.time
                });
            }
        }
    }
}
