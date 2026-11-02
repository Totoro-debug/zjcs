using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleSoundCue
    {
        public float time, speed = 1f, duration, loop_interval = -1f;
        public uint event_id, stop_event_id;
    }

    [Serializable]
    public sealed class OfficialBattleSoundEvent
    {
        public uint id, action_id, sound_id, media_id;
        public string name, guid, clip_resource, pcm_sha256;
        public int channels, sample_rate, frames, sample_width;
    }

    /// <summary>Original role movement sounds. Event routing is an explicit backend responsibility.</summary>
    public sealed class OfficialBattleSoundCatalog
    {
        [Serializable] private sealed class Record { public int id; public OfficialBattleSoundCue[] cues; }
        [Serializable] private sealed class Data
        {
            public string manifest;
            public Record[] skills, actions;
            public OfficialBattleSoundEvent[] events;
        }
        private readonly Dictionary<int, OfficialBattleSoundCue[]> skills = new Dictionary<int, OfficialBattleSoundCue[]>();
        private readonly Dictionary<int, OfficialBattleSoundCue[]> actions = new Dictionary<int, OfficialBattleSoundCue[]>();
        private readonly Dictionary<uint, OfficialBattleSoundEvent> events = new Dictionary<uint, OfficialBattleSoundEvent>();
        public IEnumerable<OfficialBattleSoundEvent> Events => events.Values;

        public OfficialBattleSoundCatalog()
        {
            var asset = Resources.Load<TextAsset>("Combat/Audio/movement");
            if (asset == null) throw new InvalidOperationException("Original movement sound bindings are missing.");
            var data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.manifest != "884_156692" || data.skills == null || data.actions == null || data.events == null)
                throw new InvalidOperationException("Original movement sound bindings have the wrong source.");
            foreach (var entry in data.events) events.Add(entry.id, entry);
            foreach (var record in data.skills) { Validate(record.cues); skills.Add(record.id, record.cues); }
            foreach (var record in data.actions) { Validate(record.cues); actions.Add(record.id, record.cues); }
        }

        public OfficialBattleSoundCue[] Skill(int id) => skills.TryGetValue(id, out var cues) ? cues : null;
        public OfficialBattleSoundCue[] MovementAction(int id) => actions.TryGetValue(id, out var cues) ? cues : null;
        public OfficialBattleSoundEvent Event(uint id) => events.TryGetValue(id, out var value) ? value
            : throw new InvalidOperationException("Unbound original battle sound event: " + id);

        private void Validate(OfficialBattleSoundCue[] cues)
        {
            if (cues == null) throw new InvalidOperationException("Original sound cue array is missing.");
            foreach (var cue in cues)
            {
                if (cue == null || cue.time < 0f || cue.speed <= 0f || float.IsNaN(cue.time) || float.IsInfinity(cue.time))
                    throw new InvalidOperationException("Invalid original sound cue timing.");
                if (cue.event_id != 0) Event(cue.event_id);
                if (cue.stop_event_id != 0) Event(cue.stop_event_id);
            }
        }
    }
}
