using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class OfflineProfessionAudioCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public uint EventId;
            public AudioClip Clip;
        }
        public Entry[] Events;
    }
}
