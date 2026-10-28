using System;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionFollowerBone
    {
        bool Active { get; }
        float WorldX { get; }
        float WorldY { get; }
        float WorldRotationX { get; }
        float ScaleX { get; }
        float ScaleY { get; }
        float A { get; }
        float B { get; }
        float C { get; }
        float D { get; }
    }
    public interface IProfessionFollowerSlot:IProfessionSkinClipSlot
    {
        IProfessionFollowerBone Bone { get; }
        int Index { get; }
        float zOrder { get; }
    }
    public interface IProfessionFollowerSkeleton
    {
        IProfessionFollowerBone FindBone(string name);
        IProfessionFollowerSlot FindSlot(string name);
        object GetAttachment(int slotIndex,string name);
        IProfessionFollowerSlot[] DrawOrder { get; }
    }
    public interface IProfessionFollowerAnimation
    {
        IProfessionFollowerSkeleton Skeleton { get; }
        event Action<IProfessionFollowerAnimation> OnAnimationRebuild;
    }
    public interface IProfessionFollowerGraphic:IProfessionFollowerAnimation
    {
        bool UseCustomZSpacing { get; }
        int UpdateWhenInvisible { get; }
        int UpdateMode { get; }
        float ZSpacing { get; }
    }
    public interface IProfessionFollowerRenderer:IProfessionFollowerAnimation
    {
        bool UseCustomZSpacing { get; }
        int UpdateWhenInvisible { get; }
        int UpdateMode { get; }
        float ZSpacing { get; }
    }
    public interface IProfessionPartAnimatedSkeleton
    {
        string[] AnimationNames { get; }
        void PlayAnimation(string name,bool loop);
    }
}
