using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionAppearanceSkin
    {
        void Clear();
        void AddSkin(IProfessionAppearanceSkin skin);
        object GetAttachment(int slot, string name);
    }
    public interface IProfessionAppearanceSkeletonData { IProfessionAppearanceSkin FindSkin(string name); }
    public interface IProfessionAppearanceSkeleton
    {
        IProfessionAppearanceSkeletonData Data { get; }
        IProfessionAppearanceSkin Skin { get; }
        int SlotCount { get; }
        string SetupAttachmentName(int slot);
        void SetSlotAttachment(int slot, object attachment);
        void SetSkin(IProfessionAppearanceSkin skin);
        void SetToSetupPose();
        void UpdateCache();
    }
    public interface IProfessionAppearanceSkeletonComponent { IProfessionAppearanceSkeleton Skeleton { get; } }
    public interface IProfessionAppearanceSpineBindings
    {
        IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner);
        IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet);
        IProfessionAppearanceSkin CreateSkin(string name);
    }
    public static class ProfessionAppearanceSpineBindings
    {
        public static IProfessionAppearanceSpineBindings Current { get; set; }
        public static IProfessionAppearanceSpineBindings Required => Current ??
            throw new InvalidOperationException("Original appearance skeleton/skin graph is unbound.");
    }
}
