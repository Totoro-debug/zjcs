using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Native backend for the original prefab's whole-material override fields.
    // This does not use or reproduce the Spine runtime.
    public sealed class BattleNativeMonsterMaterialOverrides : MonoBehaviour
    {
        [Serializable] public sealed class MaterialOverride
        {
            public bool overrideDisabled;
            public Material originalMaterial, replacementMaterial;
        }
        [Serializable] public sealed class SlotMaterialOverride
        { public string slotName; public Material material; }
        public BattleNativeMonsterRenderer skeletonRenderer;
        public SlotMaterialOverride[] customSlotMaterials = Array.Empty<SlotMaterialOverride>();
        public MaterialOverride[] customMaterialOverrides = Array.Empty<MaterialOverride>();
        public IEnumerable<Material> SerializedMaterials
        {
            get
            {
                foreach (var item in customMaterialOverrides)
                { yield return item.originalMaterial; yield return item.replacementMaterial; }
            }
        }
        private void OnEnable()
        {
            if (!skeletonRenderer) throw new InvalidOperationException("Original material-override renderer is unbound.");
            if (customSlotMaterials.Length != 0)
                throw new NotSupportedException("Bind original per-slot overrides before using this prefab.");
            foreach (var item in customMaterialOverrides)
                if (!item.overrideDisabled)
                    skeletonRenderer.CustomMaterialOverride[item.originalMaterial] = item.replacementMaterial;
        }
        private void OnDisable()
        {
            if (!skeletonRenderer) return;
            foreach (var item in customMaterialOverrides)
                if (!item.overrideDisabled && skeletonRenderer.CustomMaterialOverride.TryGetValue(item.originalMaterial, out var value)
                    && value == item.replacementMaterial)
                    skeletonRenderer.CustomMaterialOverride.Remove(item.originalMaterial);
        }
    }
}
