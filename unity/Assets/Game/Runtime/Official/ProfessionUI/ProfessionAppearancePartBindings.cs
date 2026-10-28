using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionAppearancePartAsset:IProfessionAssetOperation<GameObject>
    {
        void Release(bool tryUnloadAllAssets);
    }
    public interface IProfessionAppearancePartFollower
    {
        string SlotName { get; }
        bool ZFront { get; }
        void Initialize();
    }
    public interface IProfessionAppearanceUnloadable
    {
        void MarkUnloadTextureImmediately();
    }
    public readonly struct ProfessionAppearancePartInstruction
    {
        public readonly int endSlot;
        public ProfessionAppearancePartInstruction(int endSlot){this.endSlot=endSlot;}
    }
    public interface IProfessionAppearancePartSkeleton:IProfessionSkinClipSkeleton
    {
        IProfessionSkinClipSlot[] DrawOrder { get; }
    }
    public interface IProfessionAppearancePartGraphic
    {
        IProfessionAppearancePartSkeleton Skeleton { get; }
        List<IProfessionSkinClipSlot> separatorSlots { get; }
        List<ProfessionAppearancePartInstruction> separatorPartInstructions { get; }
        List<Transform> SeparatorParts { get; }
        bool enableSeparatorSlots { get; set; }
        void PrepareInstructionsAndRenderers(bool isOnValidate);
    }
    public interface IProfessionAppearancePartBindings
    {
        AppearanceConfig Config { get; }
        bool HasEffect(string skinName);
        IProfessionAppearancePartAsset LoadPrefab(string path);
        GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    public static class ProfessionAppearancePartBindings
    {
        public static IProfessionAppearancePartBindings Current { get; set; }
        public static IProfessionAppearancePartBindings Required => Current ??
            throw new InvalidOperationException("Original UI appearance part resources and configuration are unbound.");
    }
}
