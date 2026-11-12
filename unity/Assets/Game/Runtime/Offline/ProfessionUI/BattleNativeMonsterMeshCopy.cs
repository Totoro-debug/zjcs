using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Original Spine.Extension.CustomRenderExistingMesh's SkeletonMecanim branch.
    // Its serialized references and LateUpdate material/mesh order are retained.
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class BattleNativeMonsterMeshCopy : MonoBehaviour, IProfessionAppearanceMaterialReplacement
    {
        public MeshRenderer ReferenceRenderer;
        public MonoDictionary<Material, Material> ReplacementMaterialDict;
        private MeshFilter referenceFilter, filter;
        private MeshRenderer renderer;
        private readonly Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
        private bool initialized;
        private Material[] shared = Array.Empty<Material>();
        public IEnumerable<Material> SerializedMaterials
        {
            get { foreach (var pair in ReplacementMaterialDict) { yield return pair.Key; yield return pair.Value; } }
        }
        public void AddRuntimeReplacementMaterial(Material originalMaterial, Material runtimeMaterial)
        {
            EnsureInitialized();
            if (replacements.TryGetValue(originalMaterial, out var value)) replacements[runtimeMaterial] = value;
        }
        public void ResetRuntimeReplacementMaterials()
        {
            replacements.Clear();
            foreach (var pair in ReplacementMaterialDict) replacements[pair.Key] = pair.Value;
            initialized = true;
        }
        private void Awake()
        {
            if (!ReferenceRenderer) ReferenceRenderer = transform.parent.GetComponentInParent<MeshRenderer>();
            if (!ReferenceRenderer || !ReferenceRenderer.GetComponent<BattleNativeMonsterRenderer>())
                throw new InvalidOperationException("Bind the original SkeletonMecanim mesh-copy reference.");
            referenceFilter = ReferenceRenderer.GetComponent<MeshFilter>();
            renderer = GetComponent<MeshRenderer>(); filter = GetComponent<MeshFilter>();
        }
        private void LateUpdate() => Refresh();
        internal void Refresh()
        {
            EnsureInitialized();
            filter.sharedMesh = referenceFilter.sharedMesh;
            var source = ReferenceRenderer.sharedMaterials;
            if (shared.Length != source.Length) shared = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
                if (replacements.TryGetValue(source[i], out var value)) shared[i] = value;
            renderer.sharedMaterials = shared;
        }
        private void EnsureInitialized() { if (!initialized) ResetRuntimeReplacementMaterials(); }
    }
}
