using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Scope the original event ID to its verified local PCM while preserving the
    // host beneath this page. Voices finish independently of page destruction.
    public sealed class OfflinePartnerUiSoundScope : IProfessionSoundHost, IDisposable
    {
        private readonly IProfessionSoundHost previous;
        private readonly OfficialPartnerFriendshipWindowAssetCatalog assets;
        private uint playingId;
        private bool disposed;

        public OfflinePartnerUiSoundScope()
        {
            assets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            previous = OfficialWwiseEvent.Host;
            OfficialWwiseEvent.Host = this;
        }

        public uint PostEvent(uint eventId, GameObject emitter)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerUiSoundScope));
            if (eventId != assets.ClickEventId)
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
            source.clip = assets.ClickClip;
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
