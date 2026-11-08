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
            public uint[] TargetIds;
            public uint[] StopTargetIds;
            public float[] StopSeconds;
            public int[] StopCurves;
            public float FadeInSeconds;
            public int FadeInCurve;
        }
        public Entry[] Events;
    }
}
