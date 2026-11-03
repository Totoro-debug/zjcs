using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrapplingLineCtrl : MonoBehaviour
{
	private enum EPlayType
	{
		None,
		Runtime,
		EditorPreview
	}

	[SerializeField]
	private LineRenderer mLineRenderer;

	[SerializeField]
	private int mLinkDensity = 3;

	[SerializeField]
	private float mLinkLength = 0.1f;

	[SerializeField]
	private int mStiffness = 10;

	[Range(0f, 180f)]
	[SerializeField]
	private float mDiffusionAngle = 90f;

	[SerializeField]
	private float mDiffusionStrength = 100f;

	[SerializeField]
	private Vector3 mSourceOffset;

	[SerializeField]
	private Vector3 mTargetOffset;

	private float mOutDuration;

	private float mStandDuration;

	private float mBackDuration;

	private GrapplingLine mGrapplingLine;

	private float mCurrentStateDuration;

	private float mStateTimeStamp;

	private EPlayType mPlayType;

	public void Play(float outDuration, float standDuration, float backDuration, Vector3 startPos, Vector3 targetPos)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		mOutDuration = outDuration;
		mStandDuration = standDuration;
		mBackDuration = backDuration;
		ResetGrapplingLine();
		mGrapplingLine = CreateGrapplingLine();
		mGrapplingLine.HookOnStateUpdate(OnStateUpdate);
		mGrapplingLine.Play(startPos + mSourceOffset, targetPos + mTargetOffset);
		mPlayType = EPlayType.Runtime;
	}

	public void Stop()
	{
		ResetGrapplingLine();
		mPlayType = EPlayType.None;
	}

	public void Render()
	{
		if (mPlayType != EPlayType.None)
		{
			mGrapplingLine.Render();
		}
	}

	public void FixOriginPosition(Vector3 pos)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (mPlayType != EPlayType.None)
		{
			mGrapplingLine.SetOrigin(pos + mSourceOffset);
		}
	}

	private void FixedUpdate()
	{
		if (mPlayType != EPlayType.None)
		{
			float process = (Time.time - mStateTimeStamp) / mCurrentStateDuration;
			mGrapplingLine.PhysicalFixedUpdate(process, Time.fixedDeltaTime);
		}
	}

	private void OnStateUpdate(GrapplingLineState state)
	{
		switch (state)
		{
		case GrapplingLineState.Grappling:
			mCurrentStateDuration = mOutDuration;
			break;
		case GrapplingLineState.Holding:
			mCurrentStateDuration = mStandDuration;
			break;
		case GrapplingLineState.Dragging:
			mCurrentStateDuration = mBackDuration;
			break;
		case GrapplingLineState.Done:
			mCurrentStateDuration = 0f;
			Stop();
			break;
		}
		mStateTimeStamp = Time.time;
	}

	private void ResetGrapplingLine()
	{
		if (mGrapplingLine != null)
		{
			mGrapplingLine.UnhookOnStateUpdate(OnStateUpdate);
			mGrapplingLine = null;
		}
	}

	private GrapplingLine CreateGrapplingLine()
	{
		return new GrapplingLine(grapplingLineSetting: new GrapplingLineSetting(mLinkDensity, mLinkLength, mStiffness, mDiffusionAngle, mDiffusionStrength), lineRenderer: mLineRenderer);
	}
}
