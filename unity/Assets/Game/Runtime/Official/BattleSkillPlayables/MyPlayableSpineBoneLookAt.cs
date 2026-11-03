using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

public interface IOfficialSkeletonBonePositionOverride
{
    bool GetBonePos(out Vector2 position);
}

[MyPlayableApply(new[] { typeof(Transform) })]
public class MyPlayableSpineBoneLookAt : MyPlayableLookAt
{
    private bool useStartPosition;
    private Vector2 startPosition;

    protected override void PlayImpl()
    {
        useStartPosition = false;
        if (myApplyComponent is Transform applyTransform
            && applyTransform.TryGetComponent(out IOfficialSkeletonBonePositionOverride boneOverride))
            useStartPosition = boneOverride.GetBonePos(out startPosition);
        base.PlayImpl();
    }

    protected override Vector2 GetMyPos() => useStartPosition ? startPosition : base.GetMyPos();
}
