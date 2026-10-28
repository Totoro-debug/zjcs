using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class WorldCameraLookAheadController : WorldCameraBaseController
{
	private Vector3 mPreTargetPos;
	private Vector2 mDir = Vector2.right;
	[NonSerialized]
	public float Distance;
	protected override void OnInit()
	{
		base.OnInit();
		Distance = mCameraControlSettings.Value.WorldCameraControlSetting.LookAheadSettings.Distance;
	}
	protected override void OnEnter()
	{
		base.OnEnter();
		((Behaviour)mMonoProxy.DragController).enabled = false;
		mPreTargetPos = ((Component)mTarget).transform.position;
	}
	protected override void OnExit()
	{
		base.OnExit();
		((Behaviour)mMonoProxy.DragController).enabled = true;
	}
	protected override void OnLateUpdate()
	{
		if ((Object)(object)mTarget == (Object)null)
		{
			UpdateCameraFrame();
			return;
		}
		Vector3 position = ((Component)mTarget).transform.position;
		Vector3 val = position - mPreTargetPos;
		mPreTargetPos = position;
		if (val == Vector3.zero)
		{
			UpdateCameraFrame();
			return;
		}
		if (val.x < 0f)
		{
			mDir = Vector2.left;
		}
		else if (val.x > 0f)
		{
			mDir = Vector2.right;
		}
		UpdateFollowPos();
		UpdateCameraFrame();
	}
	public void UpdateFollowPos()
	{
		float num = GetViewSize() / DefaultViewSize;
		((Component)mMonoProxy.CameraFollow).transform.position = ((Component)mTarget).transform.position + CameraVectorCast.Convert(mDir) * Distance * num;
	}
	public WorldCameraLookAheadController()
	{
	}
}
}
