using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Platform playback for the five resolved profession UI Wwise events.
    // Source bus/event gain is baked in the verified PCM clips.
    public sealed class OfflineProfessionSoundHost : MonoBehaviour, IProfessionSoundHost
    {
        private readonly Dictionary<uint, AudioClip> clips = new Dictionary<uint, AudioClip>();
        private uint nextPlayingId;
        private IProfessionSoundHost previous;
        private bool bound;

        public void Bind(OfflineProfessionAudioCatalog catalog)
        {
            if (bound) throw new InvalidOperationException("Profession sound host is already bound.");
            if (catalog == null || catalog.Events == null) throw new ArgumentNullException(nameof(catalog));
            foreach (var entry in catalog.Events)
            {
                if (entry == null || entry.EventId == 0 || entry.Clip == null || clips.ContainsKey(entry.EventId))
                    throw new InvalidOperationException("Profession source audio catalog is incomplete or duplicated.");
                clips.Add(entry.EventId, entry.Clip);
            }
            previous = OfficialWwiseEvent.Host;
            OfficialWwiseEvent.Host = this;
            bound = true;
        }
        public uint PostEvent(uint eventId, GameObject emitter)
        {
            if (!bound) throw new InvalidOperationException("Profession sound catalog is unbound.");
            if (!clips.TryGetValue(eventId, out AudioClip clip))
                throw new InvalidOperationException("Unmigrated profession source event: " + eventId);
            var voice = new GameObject("ProfessionSound:" + eventId);
            if (emitter != null) voice.transform.position = emitter.transform.position;
            DontDestroyOnLoad(voice);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = false;
            source.clip = clip;
            source.Play();
            voice.AddComponent<OfflineProfessionSoundVoice>().ReleaseAfter(clip.length);
            unchecked { nextPlayingId++; }
            if (nextPlayingId == 0) nextPlayingId = 1;
            return nextPlayingId;
        }
        private void OnDestroy()
        {
            if (bound && ReferenceEquals(OfficialWwiseEvent.Host, this)) OfficialWwiseEvent.Host = previous;
        }
    }
}
