using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class WorldCameraComplexController : WorldCameraBaseController
{
	private Vector3 mMoveRangeSize;
	private Vector2 mPreTargetPos;
	private Vector3 mViewRangeCenterOffset;
	[NonSerialized]
	public Vector2 ViewMoveRangePadding;
	private WorldCameraComplexSetting mComplexSetting;
	private Bounds ViewBounds
	{
		get
		{
			return DragController.Data.TargetBounds;
		}
	}
	private IgnoreUIDragController DragController => mMonoProxy.DragController;
	protected override void OnInit()
	{
		base.OnInit();
		mComplexSetting = mCameraControlSettings.Value.WorldCameraControlSetting.ComplexSetting;
		Bounds orthographicCameraViewBounds = CameraUtils.GetOrthographicCameraViewBounds(mMonoProxy.VirCamera);
		Vector3 val = new Vector3(0f, mComplexSetting.ViewSafeAreaPaddingY.x + mComplexSetting.ViewSafeAreaPaddingY.y);
		mViewRangeCenterOffset = CameraVectorCast.Convert(new Vector2(0f, (mComplexSetting.ViewSafeAreaPaddingY.x - mComplexSetting.ViewSafeAreaPaddingY.y) / 2f));
		Vector3 val2 = orthographicCameraViewBounds.size - val - Vector3.one;
		ViewMoveRangePadding = new Vector2(Mathf.Min(val2.x, mComplexSetting.ViewMoveRangePadding.x), Mathf.Min(val2.y, mComplexSetting.ViewMoveRangePadding.y));
	}
	protected override void OnEnter()
	{
		base.OnEnter();
		mPreTargetPos = CameraVectorCast.Convert(mTarget.position);
		DragController.Data.UpdateMode = DragControllerData.UpdateType.ManualUpdate;
		DragController.Data.DragUpdateMode = DragControllerData.DragUpdateType.ManualUpdate;
	}
	protected override void OnExit()
	{
		DragController.Data.UpdateMode = DragControllerData.UpdateType.LateUpdate;
		DragController.Data.DragUpdateMode = DragControllerData.DragUpdateType.OnDragUpdate;
		base.OnExit();
	}
	protected override void OnLateUpdate()
	{
		Vector2 val = CameraVectorCast.Convert(mTarget.position);
		Vector2 delta = val - mPreTargetPos;
		mPreTargetPos = val;
		if (delta == Vector2.zero)
		{
			DragController.ManualUpdateDrag();
			DragController.ManualUpdate();
			UpdateCameraFrame();
			return;
		}
		Bounds val2 = new Bounds(mTarget.position + mViewRangeCenterOffset, mMoveRangeSize);
		UpdateDragBorder();
		CalcViewMoveDis(val2, CameraVectorCast.Convert(((Component)mMonoProxy.CameraFollow).transform.position), val, ref delta);
		bool flag = false;
		if (delta != Vector2.zero)
		{
			if (BoundsContains2D(ViewBounds, val2))
			{
				flag = true;
				SetViewDamping(mComplexSetting.FollowDamping);
			}
			TranslateViewPosition(CameraVectorCast.Convert(delta));
		}
		UpdateDragPosLimit();
		if (mDragging)
		{
			DragController.ManualUpdateDrag();
		}
		DragController.ManualUpdate();
		UpdateCameraFrame();
		if (flag)
		{
			SetViewDamping(mBaseSetting.DefaultFollowDamping);
		}
	}
	public override void SetViewPos(Vector3 pos, bool immediately)
	{
		pos = ClampPos(pos);
		base.SetViewPos(pos, immediately);
	}
	private void TranslateViewPosition(Vector3 move)
	{
		Vector3 pos = ((Component)mMonoProxy.CameraFollow).transform.position + move;
		SetViewPos(pos, immediately: false);
	}
	private void CalcViewMoveDis(Bounds border, Vector2 viewPos, Vector2 curPos, ref Vector2 delta)
	{
		if (delta.x > 0f)
		{
			float target = Mathf.Min(border.center.x + border.extents.x, viewPos.x);
			delta.x = CalcOneDimensionDelta(curPos.x, target, delta.x, mComplexSetting.ViewChaseSpeed.x, positive: true);
		}
		else if (delta.x < 0f)
		{
			float target2 = Mathf.Max(border.center.x - border.extents.x, viewPos.x);
			delta.x = CalcOneDimensionDelta(curPos.x, target2, delta.x, mComplexSetting.ViewChaseSpeed.x, positive: false);
		}
		if (delta.y > 0f)
		{
			float target3 = Mathf.Min(border.center.y + border.extents.y, viewPos.y);
			delta.y = CalcOneDimensionDelta(curPos.y, target3, delta.y, mComplexSetting.ViewChaseSpeed.y, positive: true);
		}
		else if (delta.y < 0f)
		{
			float target4 = Mathf.Max(border.center.y - border.extents.y, viewPos.y);
			delta.y = CalcOneDimensionDelta(curPos.y, target4, delta.y, mComplexSetting.ViewChaseSpeed.y, positive: false);
		}
	}
	private float CalcOneDimensionDelta(float cur, float target, float delta, float chaseSpeed, bool positive)
	{
		if (positive ? (cur <= target) : (cur >= target))
		{
			return 0f;
		}
		float num = cur - target - delta;
		if (Mathf.Abs(num) > 0.01f)
		{
			num *= Time.deltaTime * chaseSpeed;
		}
		return delta + num;
	}
	protected override void OnViewSizeChanged()
	{
		base.OnViewSizeChanged();
		if (mEnable)
		{
			float viewSize = GetViewSize();
			Bounds orthographicCameraViewBounds = CameraUtils.GetOrthographicCameraViewBounds(mMonoProxy.VirCamera);
			UpdateDragTarget(orthographicCameraViewBounds);
			float num = viewSize / DefaultViewSize;
			Bounds viewBounds = ViewBounds;
			mMoveRangeSize = viewBounds.size - CameraVectorCast.Convert(ViewMoveRangePadding) * num;
			UpdateDragBorder();
			UpdateDragPosLimit();
			KeepFingerDragRelativePos(CameraVectorCast.Convert(orthographicCameraViewBounds.size));
		}
	}
	private void UpdateDragTarget(Bounds bounds)
	{
		DragController.Data.SetTargetBounds(bounds);
	}
	private void UpdateDragBorder()
	{
		Bounds borderBounds = default;
		borderBounds.center = mTarget.position + mViewRangeCenterOffset;
		Bounds viewBounds = ViewBounds;
		borderBounds.size = viewBounds.size * 2f - mMoveRangeSize;
		DragController.SetBorderBounds(borderBounds);
	}
	private void UpdateDragPosLimit()
	{
		if (!mComplexSetting.AllowViewMissTarget)
		{
			Vector3 position = ((Component)mTarget).transform.position;
			Bounds viewBounds = ViewBounds;
			Vector3 val = viewBounds.size / 2f;
			Vector2 elasticitySpace = mComplexSetting.ElasticitySpace;
			Vector2 limitX = new Vector2(position.x - val.x - elasticitySpace.x, position.x + val.x + elasticitySpace.x);
			Vector2 limitY = new Vector2(position.y - val.y - elasticitySpace.y, position.y + val.y + elasticitySpace.y);
			DragController.Data.SetCustomLimit(limitX, limitY);
		}
	}
	private void KeepFingerDragRelativePos(Vector2 viewSize)
	{
		DragController.ScreenOffsetScale = new Vector2(viewSize.x / (float)Screen.width, viewSize.y / (float)Screen.height);
	}
	private new Vector3 ClampPos(Vector3 pos)
	{
		Bounds borderBounds = DragController.Data.BorderBounds;
		Bounds targetBounds = DragController.Data.TargetBounds;
		Vector3 val = borderBounds.min + targetBounds.extents;
		Vector3 val2 = borderBounds.max - targetBounds.extents;
		float num = Mathf.Clamp(pos.x, val.x, val2.x);
		float num2 = Mathf.Clamp(pos.y, val.y, val2.y);
		float num3 = Mathf.Clamp(pos.z, val.z, val2.z);
		return new Vector3(num, num2, num3);
	}
	private bool BoundsContains2D(Bounds outBounds, Bounds inBounds)
	{
		Vector3 val = outBounds.min - inBounds.min;
		Vector3 val2 = outBounds.max - inBounds.max;
		if (val.x <= 0f && val.y <= 0f && val2.x >= 0f)
		{
			return val2.y >= 0f;
		}
		return false;
	}
}
}
