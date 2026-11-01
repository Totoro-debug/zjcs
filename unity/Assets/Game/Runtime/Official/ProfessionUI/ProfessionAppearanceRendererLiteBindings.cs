using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Application operations at the SDK boundary; this interface contains no SDK implementation.
    public interface IProfessionAppearanceRendererLite : IProfessionAppearanceSkeletonComponent
    {
        Component Component { get; }
        string SkeletonDataName { get; }
        Dictionary<Material, Material> CustomMaterialOverride { get; }
        int SeparatorSlotCount { get; }
        // Enumerate original skin attachment atlas-page materials, preserving attachment order.
        IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin);
        event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
        void Initialize(bool overwrite, bool quiet);
        // The original utility prefers the Mecanim component, then the animation component,
        // and finally updates the renderer mesh. The endpoint owns those backend calls.
        void UpdateAnimationAndMesh();
    }

    public interface IProfessionAppearanceMaterialReplacement
    {
        void AddRuntimeReplacementMaterial(Material originalMaterial, Material runtimeMaterial);
        void ResetRuntimeReplacementMaterials();
    }
}
