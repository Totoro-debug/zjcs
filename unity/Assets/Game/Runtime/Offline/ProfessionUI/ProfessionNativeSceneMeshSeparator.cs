using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent Unity mesh assignment. Receives completed geometry; it does not
    // sample animations, solve constraints or reproduce an SDK renderer algorithm.
    public sealed class ProfessionNativeSceneMeshSeparator : IProfessionSceneSeparatorBackend, IDisposable
    {
        private readonly List<ProfessionNativeSceneMeshPart> owned = new List<ProfessionNativeSceneMeshPart>();
        private readonly Action enable;
        private bool disposed, enabled;
        public List<IProfessionSceneMeshPart> PartsRenderers { get; } = new List<IProfessionSceneMeshPart>();
        public bool OptimizeRender { get; set; }
        public event ProfessionPartSharedMaterial OnGetSharedMaterial;
        public ProfessionNativeSceneMeshSeparator(Action enable) { this.enable = enable ?? throw new ArgumentNullException(nameof(enable)); }

        public IProfessionSceneMeshPart NewPartsRendererGameObject(Transform parent, string name, int layer)
        {
            CheckLifetime();
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var part = go.AddComponent<ProfessionNativeSceneMeshPart>();
            owned.Add(part);
            return part;
        }
        public void OnEnable() { CheckLifetime(); if (enabled) return; enabled = true; enable(); }
        public void ClearSharedMaterials()
        {
            CheckLifetime();
            foreach (var part in PartsRenderers) if (part.GameObject) part.MeshRenderer.sharedMaterials = Array.Empty<Material>();
        }
        internal void ClearSkeletonGeometry()
        {
            CheckLifetime();
            foreach (var part in owned)
                if (part) { part.Clear(); part.MeshRenderer.sharedMaterials = Array.Empty<Material>(); }
        }

        public void Render(IReadOnlyList<ProfessionNativeMeshBatches.Batch> batches,
            IReadOnlyList<int> separatorDrawIndices, Func<ProfessionNativeMeshBatches.Batch, Material> material)
        {
            CheckLifetime();
            if (!enabled) throw new InvalidOperationException("Enable the original scene separator before assigning its geometry.");
            if (batches == null || separatorDrawIndices == null || material == null) throw new ArgumentNullException();
            if (PartsRenderers.Count < separatorDrawIndices.Count + 1) throw new InvalidOperationException("Original separator parts have not been initialized.");
            for (int i = 0; i < separatorDrawIndices.Count; i++)
                if (separatorDrawIndices[i] < 0 || i > 0 && separatorDrawIndices[i] <= separatorDrawIndices[i - 1])
                    throw new ArgumentException("Original separator draw positions must be ordered and unique.");
            var groups = new List<ProfessionNativeMeshBatches.Batch>[PartsRenderers.Count];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<ProfessionNativeMeshBatches.Batch>();
            int previous = -1;
            foreach (var batch in batches)
            {
                if (batch == null || !batch.Mesh || batch.FirstDrawIndex < previous || batch.LastDrawIndex < batch.FirstDrawIndex)
                    throw new ArgumentException("Original geometry batches must retain draw order.");
                previous = batch.LastDrawIndex;
                int group = 0;
                while (group < separatorDrawIndices.Count && batch.FirstDrawIndex > separatorDrawIndices[group]) group++;
                if (group < separatorDrawIndices.Count && batch.LastDrawIndex > separatorDrawIndices[group])
                    throw new ArgumentException("Build native geometry with every original separator slot before assigning parts.");
                groups[group].Add(batch);
            }
            for (int i = 0; i < groups.Length; i++)
            {
                var part = PartsRenderers[i] as ProfessionNativeSceneMeshPart;
                if (!part) throw new InvalidOperationException("The native separator owns its mesh parts.");
                part.Assign(groups[i], material, materials => OnGetSharedMaterial?.Invoke(materials));
            }
        }
        private void CheckLifetime() { if (disposed) throw new ObjectDisposedException(nameof(ProfessionNativeSceneMeshSeparator)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            OnGetSharedMaterial = null;
            foreach (var part in owned)
                if (part) { part.gameObject.SetActive(false); if (Application.isPlaying) Object.Destroy(part.gameObject); else Object.DestroyImmediate(part.gameObject); }
            owned.Clear(); PartsRenderers.Clear();
        }
    }
}
