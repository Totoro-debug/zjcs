using System;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

[MyPlayableApply(new Type[] { typeof(SkeletonRenderer) })]
public class MyPlayableSpineClone : MyPlayable
{
    [Tooltip("单位：秒，-1表示无限持续")]
    public float Duration;
    public string AnimName;
    public bool AnimLoop;
    public SkeletonAnimation SkeletonAnimation;
    public PlayableSpinePlayerCloneInit CloneInit;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        var renderer = myApplyComponent as SkeletonRenderer;
        if (renderer == null) return;
        CloneInit.Init(renderer);
        SkeletonAnimation.gameObject.SetActive(true);
        BattleSpineCloneAnimation.PlayAnimation(SkeletonAnimation, AnimName, AnimLoop);
    }

    public override float GetDuration() => Duration;

    protected override void StopImpl()
    {
        CloneInit.Clear();
        SkeletonAnimation.gameObject.SetActive(false);
        base.StopImpl();
    }
}
