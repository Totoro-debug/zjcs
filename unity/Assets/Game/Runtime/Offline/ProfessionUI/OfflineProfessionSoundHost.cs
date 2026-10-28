using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Platform playback for the five resolved profession UI Wwise events.
    // Source bus/event gain is baked in the verified PCM clips.
    public sealed class OfflineProfessionSoundHost : MonoBehaviour, IProfessionSoundHost, IProfessionSoundObjectHost
    {
        private sealed class SoundObject
        {
            public string Name;
            public Vector3 Position, Forward, Up;
            public float ScalingFactor;
            public bool DefaultListeners, EnvironmentInitialized;
        }
        private readonly Dictionary<int, SoundObject> objects = new Dictionary<int, SoundObject>();
        private readonly Dictionary<uint, AudioClip> clips = new Dictionary<uint, AudioClip>();
        private uint nextPlayingId;
        private IProfessionSoundHost previous;
        private bool bound;

        public bool IsInitialized => bound;
        // No Wwise initialization asset is installed in this local host. The
        // original AkGameObj fallback for a missing settings instance is 1.
        public float DefaultScalingFactor => 1;
        public int RegisteredObjectCount => objects.Count;
        public int RegisterGameObject(GameObject emitter, string objectName)
        {
            if (!bound) throw new InvalidOperationException("Profession sound catalog is unbound.");
            int key = emitter.GetInstanceID();
            if (!objects.ContainsKey(key)) objects.Add(key, new SoundObject { Name = objectName });
            return 1;
        }
        private SoundObject RequiredObject(GameObject emitter)
        {
            if (!bound || !objects.TryGetValue(emitter.GetInstanceID(), out var value))
                throw new InvalidOperationException("Profession sound object is unregistered.");
            return value;
        }
        public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up)
        { var value = RequiredObject(emitter); value.Position = position; value.Forward = forward; value.Up = up; }
        public void InitializeEmptyEnvironment(GameObject emitter, Collider sourceCollider)
        {
            if (sourceCollider != null) throw new InvalidOperationException("Profession UI audio does not bind collider environments.");
            RequiredObject(emitter).EnvironmentInitialized = true;
        }
        public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position)
        {
            if (!RequiredObject(emitter).EnvironmentInitialized) throw new InvalidOperationException("Profession environment was not initialized.");
        }
        public void SetDefaultListeners(GameObject emitter) { RequiredObject(emitter).DefaultListeners = true; }
        public void SetScalingFactor(GameObject emitter, float factor) { RequiredObject(emitter).ScalingFactor = factor; }
        public void UnregisterGameObject(GameObject emitter) { objects.Remove(emitter.GetInstanceID()); }

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
