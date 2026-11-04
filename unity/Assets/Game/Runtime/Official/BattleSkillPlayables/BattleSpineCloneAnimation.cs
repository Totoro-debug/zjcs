using System;
using Spine.Unity;
using UnityEngine;

// AppearanceAnimUtils.PlayAnimation(SkeletonAnimation), SetAnimation and
// UpdateAnimation, preserving the original zero-mix and immediate pose update.
// Split clips remain an explicit local-resource dependency.
public static class BattleSpineCloneAnimation
{
    public interface ISplitAnimationLoader
    {
        bool TryLoad(SkeletonAnimation skeleton, string name, out Spine.Animation animation);
    }

    public static void PlayAnimation(SkeletonAnimation skeleton, string name, bool loop)
    {
        if (skeleton == null)
        {
            Debug.LogError("skeletonAnimation为空！Anim:" + name);
            return;
        }
        if (skeleton.skeletonDataAsset == null) return;
        var state = skeleton.AnimationState;
        if (state == null)
        {
            skeleton.loop = loop;
            skeleton.AnimationName = name;
            return;
        }
        var data = skeleton.skeletonDataAsset.GetSkeletonData(true);
        var animation = data?.FindAnimation(name);
        if (animation == null)
        {
            var loader = skeleton.GetComponent<ISplitAnimationLoader>();
            if (loader == null)
                throw new InvalidOperationException("Bind the original split-animation loader for clone clip: " + name);
            if (!loader.TryLoad(skeleton, name, out animation))
            {
                Debug.LogWarning("不存在指定的动画:" + name);
                return;
            }
        }
        var entry = state.SetAnimation(0, animation, loop);
        entry.MixDuration = 0f;
        entry.MixTime = 0f;
        if (skeleton.TryGetComponent<SkeletonMecanim>(out var mecanim)) mecanim.ApplyAnimation();
        else if (skeleton.TryGetComponent<SkeletonAnimation>(out var animated)) animated.ApplyAnimation();
        skeleton.LateUpdateMesh();
    }
}
