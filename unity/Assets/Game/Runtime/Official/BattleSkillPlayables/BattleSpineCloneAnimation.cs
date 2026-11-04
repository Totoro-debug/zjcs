using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

// Native endpoint for AppearanceAnimUtils.PlayAnimation(SkeletonAnimation).
// Its implementation must resolve the official split clip, select track zero
// with zero mix, and immediately apply animation and update the mesh, retaining
// the original null-data and not-yet-initialized state branches.
public interface IBattleSpineCloneAnimationBackend : IAppearanceAnimComponent { }

public static class BattleSpineCloneAnimation
{
    public static IBattleSpineCloneAnimationBackend Backend(Component skeleton)
    {
        var backend = skeleton as IBattleSpineCloneAnimationBackend;
        if (backend == null && skeleton != null) backend = skeleton.GetComponent<IBattleSpineCloneAnimationBackend>();
        return backend ?? throw new InvalidOperationException("Bind the native original clone animation backend.");
    }

    public static void PlayAnimation(Component skeleton, string name, bool loop)
    {
        if (skeleton == null)
        {
            Debug.LogError("skeletonAnimation为空！Anim:" + name);
            return;
        }
        Backend(skeleton).PlayAnimation(name, loop);
    }
}
