using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionSoundHost
    {
        uint PostEvent(uint eventId, GameObject emitter);
    }

    [Serializable]
    public sealed class OfficialWwiseEvent
    {
        public uint idInternal;
        public byte[] valueGuidInternal;
        public WwiseEventReference WwiseObjectReference;
        [NonSerialized] private uint playingId;
        public static IProfessionSoundHost Host { get; set; }
        public bool IsValid => WwiseObjectReference != null;
        public uint Id => IsValid ? WwiseObjectReference.id : 0;
        public uint PlayingId => playingId;

        // APK AK.Wwise.Event.Post(GameObject), 0x7926b20: validate reference,
        // obtain original event ID, post, retain and return the playing ID.
        public uint Post(GameObject emitter)
        {
            if (!IsValid) return 0;
            var host = Host ?? throw new InvalidOperationException("Original profession sound-bank host is unbound.");
            playingId = host.PostEvent(Id, emitter);
            return playingId;
        }
    }
}
