using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionSkinClipBoundingBox
    {
        string Name { get; }
        int WorldVerticesLength { get; }
        void ComputeWorldVertices(IProfessionSkinClipSlot slot, float[] vertices);
    }
    public interface IProfessionSkinClipSlot { object Attachment { get; } }
    public interface IProfessionSkinClipSkeleton { IProfessionSkinClipSlot FindSlot(string name); }
    public interface IProfessionSkinClipGraphic
    {
        Component Instance { get; }
        float MeshScale { get; }
        List<CanvasRenderer> canvasRenderers { get; }
        event Action<IProfessionSkinClipGraphic> OnMeshAndMaterialsUpdated;
    }
    public interface IProfessionSkinClipAppearance
    {
        Action OnSkinUpdate { get; set; }
        IProfessionSkinClipSkeleton Skeleton { get; }
        IProfessionSkinClipGraphic Graphic { get; }
        bool TryGetMaterial(string skinName, out Material material);
    }
    public interface IProfessionSkinClipCharacter { bool TryGetSkinName(SkinType type, out string name); }
    public interface IProfessionAppearanceSkinClipBindings
    {
        Component FindCharacter(Component owner);
        Component FindSpine(Component owner);
        IProfessionSkinClipCharacter GetCharacter(Component character);
        IProfessionSkinClipAppearance GetAppearance(Component spine);
    }
    public static class ProfessionAppearanceSkinClipBindings
    {
        public static IProfessionAppearanceSkinClipBindings Current { get; set; }
        public static IProfessionAppearanceSkinClipBindings Required => Current ??
            throw new InvalidOperationException("Original appearance skin-clip graph is unbound.");
    }
}
