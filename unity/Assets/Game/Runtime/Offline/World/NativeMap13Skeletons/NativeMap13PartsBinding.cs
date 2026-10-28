using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Explicit original separator targets, in their serialized order. Native
    // MeshRenderers retain the original flags, materials and transforms.
    public sealed class NativeMap13PartsBinding : MonoBehaviour
    {
        public string SourceKey;
        public MeshRenderer SourceRenderer;
        public MeshRenderer[] Renderers;
        public string[] SeparatorSlots;
        public bool CopyPropertyBlock, CopyMeshRendererFlags, OptimizeRender;
        internal event Action<bool> SeparationChanged;
        private void OnEnable() { SeparationChanged?.Invoke(true); }
        private void OnDisable() { SeparationChanged?.Invoke(false); }
    }
}
