using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Scope the original event ID to its verified local PCM while preserving the
    // host beneath this page. Voices finish independently of page destruction.
    public sealed class OfflinePartnerUiSoundScope : IProfessionSoundHost, IProfessionSoundObjectHost, IDisposable
    {
        private readonly IProfessionSoundHost previous;
        private readonly Func<uint, AudioClip> resolve;
        private uint playingId;
        private bool disposed;

        // Original AkGameObj instances cache this host through destruction, so
        // registration continues to belong to the underlying sound engine.
        private IProfessionSoundObjectHost Objects => previous as IProfessionSoundObjectHost ??
            throw new InvalidOperationException("Original partner sound-object host is unbound.");
        public bool IsInitialized => Objects.IsInitialized;
        public float DefaultScalingFactor => Objects.DefaultScalingFactor;
        public int RegisterGameObject(GameObject emitter, string objectName) => Objects.RegisterGameObject(emitter, objectName);
        public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up) =>
            Objects.SetObjectPosition(emitter, position, forward, up);
        public void InitializeEmptyEnvironment(GameObject emitter, Collider sourceCollider) => Objects.InitializeEmptyEnvironment(emitter, sourceCollider);
        public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) => Objects.UpdateEmptyEnvironment(emitter, position);
        public void SetDefaultListeners(GameObject emitter) => Objects.SetDefaultListeners(emitter);
        public void SetScalingFactor(GameObject emitter, float factor) => Objects.SetScalingFactor(emitter, factor);
        public void UnregisterGameObject(GameObject emitter) => Objects.UnregisterGameObject(emitter);

        public OfflinePartnerUiSoundScope()
            : this(OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault()) { }

        private OfflinePartnerUiSoundScope(OfficialPartnerFriendshipWindowAssetCatalog assets)
            : this(eventId => eventId == assets.ClickEventId ? assets.ClickClip : null) { }

        public OfflinePartnerUiSoundScope(Func<uint, AudioClip> resolve)
        {
            this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            previous = OfficialWwiseEvent.Host;
            OfficialWwiseEvent.Host = this;
        }

        public uint PostEvent(uint eventId, GameObject emitter)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerUiSoundScope));
            var clip = resolve(eventId);
            if (!clip)
            {
                if (previous != null) return previous.PostEvent(eventId, emitter);
                throw new InvalidOperationException("Unbound original partner audio event: " + eventId);
            }
            var voice = new GameObject("PartnerSound:" + eventId);
            UnityEngine.Object.DontDestroyOnLoad(voice);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = false;
            source.clip = clip;
            source.Play();
            UnityEngine.Object.Destroy(voice, source.clip.length);
            unchecked { playingId++; }
            if (playingId == 0) playingId = 1;
            return playingId;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (ReferenceEquals(OfficialWwiseEvent.Host, this)) OfficialWwiseEvent.Host = previous;
            disposed = true;
        }
    }
}
