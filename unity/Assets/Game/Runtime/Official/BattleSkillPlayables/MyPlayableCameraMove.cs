using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

public enum ECameraOffsetGroup
{
    One,
    Two,
    Three,
    S
}

public class MyPlayableCameraMove : MyPlayable, IBattleCameraPlayable
{
    public ECameraOffsetGroup CameraGroup;
    public Vector3 Offset;
    public AnimationCurve X;
    public AnimationCurve Y;
    public AnimationCurve Z;
    public float Duration;
    public bool RotBySkill;
    public bool FlipBySourceX;
    public bool FlipByTargetY;

    private IBattleCameraOffsetHost cameraOffsetHost;
    private int offsetIndex = -1;
    private bool flip;
    private Quaternion skillRotation = Quaternion.identity;

    public void BindCameraOffsetHost(IBattleCameraOffsetHost host) => cameraOffsetHost = host;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        if (FlipBySourceX && mySource.transform.localScale.x < 0f) flip = !flip;
        if (FlipByTargetY && myTarget.transform.position.y < mySource.transform.position.y) flip = !flip;
        if (RotBySkill && TryGetParam("Skill") is GameObject skill && skill != null)
            skillRotation = skill.transform.rotation;
        if (cameraOffsetHost != null)
            offsetIndex = cameraOffsetHost.RegisterCameraOffset(Offset, (int)CameraGroup);
    }

    protected override bool PostProcessFrameImpl(float deltaTime)
    {
        if (offsetIndex < 0 || cameraOffsetHost == null) return true;
        float progress = Mathf.Min(1f, Time / Duration);
        Vector3 offset = new Vector3(X.Evaluate(progress) * Offset.x,
            Y.Evaluate(progress) * Offset.y, Z.Evaluate(progress) * Offset.z);
        if (RotBySkill)
        {
            Vector3 rotated = skillRotation * offset;
            rotated.z = offset.z;
            offset = rotated;
        }
        if (flip) offset.x = -offset.x;
        cameraOffsetHost.SetCameraOffset(offsetIndex, offset);
        return false;
    }

    protected override void StopImpl()
    {
        ClearOffset();
        base.StopImpl();
    }

    public override float GetDuration() => Duration < 0f ? -1f : Duration;

    protected override void OnDestroy()
    {
        ClearOffset();
        base.OnDestroy();
    }

    private void ClearOffset()
    {
        if (offsetIndex >= 0) cameraOffsetHost?.UnregisterCameraOffset(offsetIndex);
        offsetIndex = -1;
        flip = false;
        skillRotation = Quaternion.identity;
    }
}
