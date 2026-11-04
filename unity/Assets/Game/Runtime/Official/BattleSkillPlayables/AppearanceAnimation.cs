using UnityEngine;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;

// Original AppearanceSystem.Runtime.AppearanceAnimation, using the same native
// renderer/application boundary as the other current character appearances.
public class AppearanceAnimation : AppearanceRenderer, IAppearanceAnimComponent
{
    private Component mSkeletonAnimation;
    public Component SkeletonAnimation => mSkeletonAnimation
        ? mSkeletonAnimation : (mSkeletonAnimation = GetComponent<IBattleSpineCloneAnimationBackend>() as Component);
    public IProfessionAppearanceAnimationState AnimState => BattleSpineCloneAnimation.Backend(SkeletonAnimation).AnimState;
    public void PlayAnimation(string animationName, bool loop)
        => BattleSpineCloneAnimation.PlayAnimation(SkeletonAnimation, animationName, loop);
}
