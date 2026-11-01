using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Ports for the original application handlers; no animation SDK implementation.
    public delegate void ProfessionPartSharedMaterial(Material[] sharedMaterials);

    public interface IProfessionAppearancePartSeparator
    {
        event ProfessionPartSharedMaterial OnGetSharedMaterial;
        void ReInitPartRenderers(int sortingLayer, int extraParts, int sortingOrderIncrement, int sortingOrder);
        void ClearSharedMaterials();
    }

    public interface IProfessionAppearancePartRenderer
    {
        GameObject GameObject { get; }
        IProfessionSkinClipSkeleton Skeleton { get; }
        List<IProfessionSkinClipSlot> SeparatorSlots { get; }
        IProfessionAppearancePartSeparator Separator { get; }
        IProfessionAppearancePartSeparator AddSeparator(int sortingLayer, int extraParts, int sortingOrderIncrement, int sortingOrder);
        void LateUpdateMesh();
    }

    // Identity is the original animation object, not its name or Unity clip.
    public delegate void ProfessionPartClipApplied(object animation, string name, int layer, float weight,
        float time, float lastTime, bool playsBackward);

    public interface IProfessionAppearancePartMecanim
    {
        event ProfessionPartClipApplied OnClipApplied;
    }
}
