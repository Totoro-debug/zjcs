using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

public class MyPlayableFightCameraPerformance : MyPlayable, IBattleCameraPerformancePlayable
{
    private static readonly Vector3 SwitchPosition = new Vector3(1000f, 0f, 0f);

    public float Duration;
    public bool IsMoveSource;
    public Vector3 MoveOffsetOfSource = Vector3.zero;

    private IBattleCameraPerformanceHost host;
    private Vector3? previousSourcePosition;
    private long inputOperationId;

    public void BindCameraPerformanceHost(IBattleCameraPerformanceHost value) => host = value;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        if (host == null)
            throw new System.InvalidOperationException("Official camera performance host is not bound.");

        previousSourcePosition = null;
        inputOperationId = 0;
        if (myApplyComponent != null)
            myApplyComponent.transform.localPosition = SwitchPosition;
        if (mySource != null && IsMoveSource)
        {
            Transform source = mySource.transform;
            previousSourcePosition = source.localPosition;
            source.localPosition = SwitchPosition + MoveOffsetOfSource;
        }

        host.SwitchInPerformanceCamera();
        inputOperationId = host.BlockGameObjectInput();
    }

    protected override void StopImpl()
    {
        if (myApplyComponent != null)
            myApplyComponent.transform.localPosition = Vector3.zero;
        if (mySource != null && previousSourcePosition.HasValue)
            mySource.transform.localPosition = previousSourcePosition.Value;

        if (host != null)
        {
            host.SwitchOutPerformanceCamera();
            host.UnblockGameObjectInput(inputOperationId);
        }

        previousSourcePosition = null;
        inputOperationId = 0;
        base.StopImpl();
    }

    public override float GetDuration() => Duration;
}
