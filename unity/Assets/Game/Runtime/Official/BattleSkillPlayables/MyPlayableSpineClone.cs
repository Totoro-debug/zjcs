using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;

[MyPlayableApply(new Type[] { typeof(IProfessionFollowerRenderer) })]
public class MyPlayableSpineClone : MyPlayable
{
    [Tooltip("单位：秒，-1表示无限持续")]
    public float Duration;
    public string AnimName;
    public bool AnimLoop;
    public Component SkeletonAnimation;
    public PlayableSpinePlayerCloneInit CloneInit;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        var renderer = myApplyComponent is IProfessionFollowerRenderer ? myApplyComponent : null;
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
