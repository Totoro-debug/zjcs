using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class WorldCameraDragController : WorldCameraBaseController
{
	public class OverrideViewData
	{
		public float DefaultViewSize;
		public float SoftViewSizeMin;
		public float SoftViewSizeMax;
		public float ZoomViewSizeMin;
		public float ZoomViewSizeMax;
	}
	private OverrideViewData mOverrideViewData;
	private Vector3 mOriginPos;
	private Vector2 mViewMoveRangeSize;
	private Bounds mView;
	private Vector3 mLastTargetPos;
	public new IMapRootManagerProxy MapRootManagerProxy { get; set; }
	public override float DefaultViewSize
	{
		get
		{
			return mOverrideViewData?.DefaultViewSize ?? base.DefaultViewSize;
		}
		set
		{
			base.DefaultViewSize = value;
		}
	}
	public override float SoftViewSizeMin => mOverrideViewData?.SoftViewSizeMin ?? base.SoftViewSizeMin;
	public override float SoftViewSizeMax => mOverrideViewData?.SoftViewSizeMax ?? base.SoftViewSizeMax;
	public override float ZoomViewSizeMin => mOverrideViewData?.ZoomViewSizeMin ?? base.ZoomViewSizeMin;
	public override float ZoomViewSizeMax => mOverrideViewData?.ZoomViewSizeMax ?? base.ZoomViewSizeMax;
	protected override void OnInit()
	{
		base.OnInit();
		GridGroundComponentInfo info = MapRootManagerProxy.GridGround.Info;
		mOriginPos = info.OriginPos;
		mViewMoveRangeSize = new Vector2((float)info.Size.x, (float)info.Size.y);
		Bounds val = default;
		val.center = CameraVectorCast.Convert(mViewMoveRangeSize) / 2f + mOriginPos;
		val.size = CameraVectorCast.Convert(mViewMoveRangeSize);
		Bounds borderBounds = val;
		mMonoProxy.DragController.SetBorderBounds(borderBounds);
	}
	protected override void OnLateUpdate()
	{
		Vector3 position = ((Component)mTarget).transform.position;
		Vector3 val = position - mLastTargetPos;
		mLastTargetPos = position;
		if (val != Vector3.zero)
		{
			UpdateDragBorder();
		}
		base.OnLateUpdate();
	}
	public override void SetViewPos(Vector3 pos, bool immediately)
	{
		Vector3 pos2 = mMonoProxy.DragController.Data.ClampPos(pos);
		base.SetViewPos(pos2, immediately);
	}
	public void SetOverrideViewData(OverrideViewData overrideViewData)
	{
		if (overrideViewData == null || overrideViewData.DefaultViewSize <= 0f)
		{
			mOverrideViewData = null;
			return;
		}
		mBaseSetting.GetViewSize(overrideViewData.DefaultViewSize, GmCameraControlData.GmControl, out var softViewSizeMin, out var softViewSizeMax, out var zoomViewSizeMin, out var zoomViewSizeMax);
		if (overrideViewData.SoftViewSizeMin <= 0f)
		{
			overrideViewData.SoftViewSizeMin = softViewSizeMin;
		}
		if (overrideViewData.SoftViewSizeMax <= 0f)
		{
			overrideViewData.SoftViewSizeMax = softViewSizeMax;
		}
		if (overrideViewData.ZoomViewSizeMin <= 0f)
		{
			overrideViewData.ZoomViewSizeMin = zoomViewSizeMin;
		}
		if (overrideViewData.ZoomViewSizeMax <= 0f)
		{
			overrideViewData.ZoomViewSizeMax = zoomViewSizeMax;
		}
		mOverrideViewData = overrideViewData;
	}
	protected override void OnViewSizeChanged()
	{
		base.OnViewSizeChanged();
		if (mEnable)
		{
			UpdateDragBorder();
		}
	}
	private void UpdateDragBorder()
	{
		if (!((Object)(object)mMonoProxy.VirCamera == (Object)null))
		{
			mView = CameraUtils.GetOrthographicCameraViewBounds(mMonoProxy.VirCamera);
			Vector3 size = mView.size;
			IgnoreUIDragController dragController = mMonoProxy.DragController;
			dragController.ScreenOffsetScale = new Vector2(size.x / (float)Screen.width, size.y / (float)Screen.height);
			dragController.Data.SetTargetBounds(mView);
		}
	}
}
}
