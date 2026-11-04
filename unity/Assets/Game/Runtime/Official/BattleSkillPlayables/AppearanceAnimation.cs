using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;

// Original AppearanceSystem.Runtime.AppearanceAnimation. The SDK state is
// exposed through the same read-only state port as the other appearance views.
[RequireComponent(typeof(SkeletonAnimation))]
public class AppearanceAnimation : AppearanceRenderer, IAppearanceAnimComponent
{
    private SkeletonAnimation mSkeletonAnimation;
    public SkeletonAnimation SkeletonAnimation => mSkeletonAnimation
        ? mSkeletonAnimation : (mSkeletonAnimation = GetComponent<SkeletonAnimation>());
    public IProfessionAppearanceAnimationState AnimState => SkeletonAnimation.state == null
        ? null : new AnimationState(SkeletonAnimation.state);
    public void PlayAnimation(string animationName, bool loop)
        => BattleSpineCloneAnimation.PlayAnimation(SkeletonAnimation, animationName, loop);

    private sealed class AnimationState : IProfessionAppearanceAnimationState
    {
        private readonly Spine.AnimationState state;
        public AnimationState(Spine.AnimationState state) { this.state = state; }
        public IProfessionAppearanceTrack GetCurrent(int track)
        {
            var entry = state?.GetCurrent(track);
            return entry == null ? null : new Track(entry);
        }
    }
    private sealed class Track : IProfessionAppearanceTrack
    {
        private readonly Spine.TrackEntry track;
        public Track(Spine.TrackEntry track) { this.track = track; }
        public IProfessionAppearanceAnimation Animation => track.Animation == null
            ? null : new Animation(track.Animation);
    }
    private sealed class Animation : IProfessionAppearanceAnimation
    {
        private readonly Spine.Animation animation;
        public Animation(Spine.Animation animation) { this.animation = animation; }
        public string Name => animation.Name;
    }
}
