using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// Shared original sound dispatch. Tick receives the battle's scaled delta;
    /// the injected backend separately owns original event routing, DSP and RTPC.
    /// </summary>
    public sealed class OfficialBattleSoundPlayback : IDisposable
    {
        public sealed class Handle : IDisposable
        {
            private readonly OfficialBattleSoundPlayback owner;
            internal readonly MyPlayableSequence Sequence;
            internal Handle(OfficialBattleSoundPlayback owner, MyPlayableSequence sequence)
            { this.owner = owner; Sequence = sequence; }
            public bool IsPlaying => Sequence != null && Sequence.myStatus != MyPlayable.PlayableStatus.Stop;
            public void Dispose() => owner.Stop(this);
        }

        private readonly OfficialBattleSoundCatalog catalog;
        private readonly IProfessionSoundHost backend;
        private readonly GameObject root;
        private readonly MyPlayableCenter center = new MyPlayableCenter();
        private readonly List<Handle> active = new List<Handle>();
        private readonly Dictionary<uint, WwiseEventReference> references = new Dictionary<uint, WwiseEventReference>();
        private bool disposed;
        public int ActiveCount => active.Count;

        public OfficialBattleSoundPlayback(OfficialBattleSoundCatalog catalog, IProfessionSoundHost backend, Transform parent)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            root = new GameObject("OfficialBattleSoundPlayables");
            root.transform.SetParent(parent, false);
        }

        // A skill owns one sequence. The movement adapter owns a separate handle
        // for each actual cell, disposing it when that cell's action ends.
        public Handle PlaySkill(int skillId, GameObject source, GameObject target)
            => Play(catalog.Skill(skillId), source, target);
        public Handle PlayMovementAction(int actionId, GameObject source, GameObject target)
            => Play(catalog.MovementAction(actionId), source, target);

        public Handle Play(IReadOnlyList<OfficialBattleSoundCue> cues, GameObject source, GameObject target)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSoundPlayback));
            if (cues == null || cues.Count == 0) return null;
            // Resolve every event before creating a partial playable graph.
            foreach (var cue in cues)
            {
                if (cue == null || cue.time < 0f || float.IsNaN(cue.time) || float.IsInfinity(cue.time)
                    || cue.speed <= 0f || float.IsNaN(cue.speed) || float.IsInfinity(cue.speed))
                    throw new ArgumentException("Invalid original sound cue.", nameof(cues));
                if (cue.event_id != 0) catalog.Event(cue.event_id);
                if (cue.stop_event_id != 0) catalog.Event(cue.stop_event_id);
            }
            var instance = new GameObject("BattleSoundSequence");
            instance.transform.SetParent(root.transform, false);
            var sequence = instance.AddComponent<OfficialBattleSoundSequence>();
            Configure(sequence);
            sequence.myPlayables = new List<MyPlayableSequence.MyPlayableTimeCfg>();
            foreach (var cue in cues)
            {
                var child = new GameObject("OriginalBattleSound");
                child.transform.SetParent(instance.transform, false);
                var sound = child.AddComponent<MyPlayableSound>();
                Configure(sound);
                sound.mySpeed = cue.speed;
                sound.Duration = cue.duration;
                sound.LoopInterval = cue.loop_interval;
                sound.SoundEvent = Event(cue.event_id);
                sound.StopSoundEvent = Event(cue.stop_event_id);
                sequence.myPlayables.Add(new MyPlayableSequence.MyPlayableTimeCfg { myTime = cue.time, myPlayable = sound });
            }
            var handle = new Handle(this, sequence);
            active.Add(handle);
            WithBackend(() => { sequence.Init(source, target); sequence.TryPlay(); });
            return handle;
        }

        private void Configure(MyPlayable playable)
        {
            playable.MyPlayableCenter = center;
            // One owner destroys the complete graph after original Stop/Remove.
            playable.myEndDestroy = false;
            playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            playable.myFadeoutTrigerList = new List<MyPlayableTrigger>();
            playable.myEndTrigerList = new List<MyPlayableTrigger>();
        }

        private OfficialWwiseEvent Event(uint id)
        {
            if (id == 0) return null;
            if (!references.TryGetValue(id, out var reference))
            {
                var entry = catalog.Event(id);
                reference = ScriptableObject.CreateInstance<WwiseEventReference>();
                reference.objectName = entry.name; reference.id = id; reference.guid = entry.guid;
                references.Add(id, reference);
            }
            return new OfficialWwiseEvent { WwiseObjectReference = reference };
        }

        public void Tick(float scaledDeltaTime)
        {
            if (disposed || scaledDeltaTime <= 0f) return;
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            WithBackend(() => { center.Update(scaledDeltaTime); center.LateUpdate(); });
            for (int i = active.Count - 1; i >= 0; i--)
                if (!active[i].IsPlaying) Release(i);
        }

        private void Stop(Handle handle)
        {
            int index = active.IndexOf(handle);
            if (index < 0) return;
            if (handle.Sequence != null) WithBackend(() => handle.Sequence.Stop(true));
            Release(index);
        }

        private void Release(int index)
        {
            var sequence = active[index].Sequence;
            if (sequence != null)
            {
                WithBackend(() => sequence.Remove());
                Destroy(sequence.gameObject);
            }
            active.RemoveAt(index);
        }

        public void Stop()
        {
            while (active.Count > 0) Stop(active[active.Count - 1]);
            center.RemoveAll();
        }

        private void WithBackend(Action action)
        {
            var previous = OfficialWwiseEvent.Host;
            try { OfficialWwiseEvent.Host = backend; action(); }
            finally { OfficialWwiseEvent.Host = previous; }
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            foreach (var reference in references.Values) Destroy(reference);
            references.Clear();
            Destroy(root);
            disposed = true;
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
