using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.World
{
    public sealed class MainlineGroundSkeletonData : ScriptableObject
    {
        public TextAsset Payload;
        public Material Material;
        private ProfessionNativeSkeletonData data;
        public ProfessionNativeSkeletonData Load()
        {
            if (!Payload || !Material) throw new System.IO.InvalidDataException("Original world skeleton payload/material is unbound.");
            return data ?? (data = ProfessionNativeSkeletonData.LoadWorld(Payload.bytes));
        }
    }
}
