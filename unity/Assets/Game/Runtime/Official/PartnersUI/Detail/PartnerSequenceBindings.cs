using System;

namespace Zjcs.Official.PartnersUI
{
    // Only the original application's SDK call boundary. Native actor bindings
    // supply sampled animation state; no SDK implementation is copied here.
    public interface IPartnerSequenceAnimation
    {
        string Name { get; }
        float Duration { get; }
    }
    public interface IPartnerSequenceTrack
    {
        IPartnerSequenceAnimation Animation { get; }
        bool Loop { get; }
        float MixTime { get; set; }
        float MixDuration { get; set; }
    }
    public interface IPartnerSequenceAnimationState
    {
        event Action<IPartnerSequenceTrack> Start;
        event Action<IPartnerSequenceTrack> Complete;
        IPartnerSequenceTrack GetCurrent(int track);
        IPartnerSequenceTrack SetAnimation(int track, IPartnerSequenceAnimation animation, bool loop);
        void ClearTrack(int track);
    }
    public interface IPartnerSequenceSkeleton
    {
        void SetToSetupPose();
    }
    public interface IPartnerSequenceGraphic
    {
        IPartnerSequenceAnimationState AnimationState { get; }
        IPartnerSequenceSkeleton Skeleton { get; }
        event Action<IPartnerSequenceGraphic> OnAnimationRebuild;
        IPartnerSequenceAnimation GetOrLoadAnimation(string name);
        void UpdateAnimation();
    }
}
