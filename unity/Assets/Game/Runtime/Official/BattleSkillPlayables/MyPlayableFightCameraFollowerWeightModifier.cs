using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

public class MyPlayableFightCameraFollowerWeightModifier : MyPlayable, IBattleCameraPlayable
{
    [Range(0f, 100f)] public float Weight = 1f;
    public float Duration;
    public bool ResetAtStop = true;
    public bool AutoAdd;

    private IBattleCameraOffsetHost cameraOffsetHost;
    private bool changed, added;
    private float previousWeight = -1f;
    private Transform follower;

    public void BindCameraOffsetHost(IBattleCameraOffsetHost host) => cameraOffsetHost = host;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        if (myApplyComponent == null) return;
        changed = false;
        previousWeight = -1f;
        follower = myApplyComponent.transform;
        added = false;
        ChangeWeight();
    }

    protected override void StopImpl()
    {
        if (myApplyComponent != null)
        {
            if (ResetAtStop) ResetWeight();
            changed = false;
            previousWeight = -1f;
            follower = null;
            added = false;
        }
        base.StopImpl();
    }

    public override float GetDuration() => Duration;

    private void ChangeWeight()
    {
        if (cameraOffsetHost == null || follower == null) return;
        changed = cameraOffsetHost.TryChangeFollowerWeight(follower, Weight, out previousWeight);
        if (!changed && AutoAdd) added = changed = cameraOffsetHost.AddExternalFollower(follower, Weight);
    }

    private void ResetWeight()
    {
        if (cameraOffsetHost == null || follower == null || !changed) return;
        if (added) cameraOffsetHost.RemoveExternalFollower(follower);
        else if (previousWeight >= 0f)
            cameraOffsetHost.TryChangeFollowerWeight(follower, previousWeight, out _);
    }
}
