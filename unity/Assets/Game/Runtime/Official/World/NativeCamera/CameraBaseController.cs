using System;
using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public abstract class CameraBaseController : ICameraControllerDriven, ICameraController
{
	private float mDefaultViewSize;
	protected float mSoftViewSizeMin;
	protected float mSoftViewSizeMax;
	protected float mZoomViewSizeMin;
	protected float mZoomViewSizeMax;
	protected CameraBaseSetting mBaseSetting;
	protected CameraMonoProxy mMonoProxy;
	protected bool mZooming;
	protected bool mDragging;
	protected Transform mTarget;
	protected bool mEnable;
	private CinemachineTransposer mCinemachineTransposer;
	private bool mZoomInputHooked;
	public virtual float DefaultViewSize
	{
		get
		{
			if (mDefaultViewSize == 0f)
			{
				mDefaultViewSize = GetDefaultViewSize();
			}
			return mDefaultViewSize;
		}
		set
		{
			mDefaultViewSize = value;
			OnViewSizeChanged();
		}
	}
	public Action<Vector3> OnPosUpdated { get; set; }
	public Action<float> OnViewSizeUpdated { get; set; }
	public virtual float SoftViewSizeMin => mSoftViewSizeMin;
	public virtual float SoftViewSizeMax => mSoftViewSizeMax;
	public virtual float ZoomViewSizeMin => mZoomViewSizeMin;
	public virtual float ZoomViewSizeMax => mZoomViewSizeMax;
	public IInputOperationModule InputOperationModule { get; set; }
	public CameraSettingsValue mCameraControlSettings { get; set; }
	public GmCameraControlData GmCameraControlData { get; set; }
	protected virtual bool EnableZoomInput => true;
	protected virtual bool EnableViewSizeLimit => true;
	protected abstract float GetDefaultViewSize();
	public virtual void SetViewPos(Vector3 pos, bool immediately)
	{
		InternalSetViewPos(pos, immediately);
	}
	protected virtual Vector3 ClampPos(Vector3 pos)
	{
		return pos;
	}
	private void InternalSetViewPos(Vector3 pos, bool immediately)
	{
		pos = ClampPos(pos);
		((Component)mMonoProxy.CameraFollow).transform.position = pos;
		if (immediately)
		{
			((CinemachineVirtualCameraBase)mMonoProxy.VirCamera).ForceCameraPosition(pos, Quaternion.identity);
		}
		OnPosUpdated?.Invoke(pos);
	}
	public virtual Vector3 GetViewPos()
	{
		return ((Component)mMonoProxy.CameraFollow).transform.position;
	}
	public virtual void SetViewSize(float viewSize, bool zoom)
	{
		if (EnableViewSizeLimit)
		{
			viewSize = (zoom ? ClampZoomViewSize(viewSize) : ClampStandardViewSize(viewSize));
		}
		mMonoProxy.VirCamera.m_Lens.OrthographicSize = viewSize;
		OnViewSizeChanged();
		OnViewSizeUpdated?.Invoke(viewSize);
	}
	public void SetDefaultViewSize(float viewSize)
	{
		mDefaultViewSize = viewSize;
	}
	public float GetViewSize()
	{
		return mMonoProxy.VirCamera.m_Lens.OrthographicSize;
	}
	protected void SetFollow(Transform follow)
	{
		mMonoProxy.CameraFollow = follow;
		if (mEnable)
		{
			((CinemachineVirtualCameraBase)mMonoProxy.VirCamera).Follow = follow;
		}
	}
	public void SetViewDamping(Vector2 damping)
	{
		if ((Object)(object)mCinemachineTransposer == (Object)null)
		{
			mCinemachineTransposer = mMonoProxy.VirCamera.GetCinemachineComponent<CinemachineTransposer>();
		}
		if ((Object)(object)mCinemachineTransposer != (Object)null)
		{
			mCinemachineTransposer.m_XDamping = damping.x;
			mCinemachineTransposer.m_YDamping = damping.y;
		}
	}
	public Vector2 GetViewDamping()
	{
		if ((Object)(object)mCinemachineTransposer == (Object)null)
		{
			mCinemachineTransposer = mMonoProxy.VirCamera.GetCinemachineComponent<CinemachineTransposer>();
		}
		if ((Object)(object)mCinemachineTransposer != (Object)null)
		{
			return new Vector2(mCinemachineTransposer.m_XDamping, mCinemachineTransposer.m_YDamping);
		}
		return mBaseSetting.DefaultFollowDamping;
	}
	void ICameraControllerDriven.Init(CameraMonoProxy monoProxy)
	{
		mMonoProxy = monoProxy;
		mBaseSetting = monoProxy.GetCameraBaseSetting(mCameraControlSettings.Value);
		mTarget = mMonoProxy.Target;
		OnInit();
	}
	void ICameraControllerDriven.DriveEnter(bool immediately)
	{
		mEnable = true;
		UpdateViewSizeParameters();
		SetFollow(mMonoProxy.CameraFollow);
		SetViewSize(DefaultViewSize, zoom: false);
		SetViewDamping(mBaseSetting.DefaultFollowDamping);
		if (((Object)(object)mTarget != (Object)null) & immediately)
		{
			InternalSetViewPos(mTarget.position, immediately: true);
		}
		if (EnableZoomInput)
		{
			InputOperationModule.HookOnInput<OnGameObjectZoomBeginResult>(OnGameObjectZoomBegin);
			InputOperationModule.HookOnInput<OnGameObjectZoomResult>(OnGameObjectZoom);
			InputOperationModule.HookOnInput<OnGameObjectZoomEndResult>(OnGameObjectZoomEnd);
			mZoomInputHooked = true;
		}
		InputOperationModule.HookOnInput<OnGameObjectDragBeginResult>(OnGameObjectDragBegin);
		InputOperationModule.HookOnInput<OnGameObjectDragEndResult>(OnGameObjectDragEnd);
		OnEnter();
	}
	void ICameraControllerDriven.DriveExit()
	{
		mEnable = false;
		mZooming = false;
		mDragging = false;
		if (mZoomInputHooked)
		{
			InputOperationModule.UnhookOnInput<OnGameObjectZoomBeginResult>(OnGameObjectZoomBegin);
			InputOperationModule.UnhookOnInput<OnGameObjectZoomResult>(OnGameObjectZoom);
			InputOperationModule.UnhookOnInput<OnGameObjectZoomEndResult>(OnGameObjectZoomEnd);
			mZoomInputHooked = false;
		}
		InputOperationModule.UnhookOnInput<OnGameObjectDragBeginResult>(OnGameObjectDragBegin);
		InputOperationModule.UnhookOnInput<OnGameObjectDragEndResult>(OnGameObjectDragEnd);
		OnExit();
	}
	void ICameraControllerDriven.DriveUpdate()
	{
		OnUpdate();
	}
	void ICameraControllerDriven.DriveLateUpdate()
	{
		OnLateUpdate();
	}
	protected virtual void OnInit()
	{
	}
	protected virtual void OnEnter()
	{
	}
	protected virtual void OnExit()
	{
	}
	protected virtual void OnUpdate()
	{
		if (EnableViewSizeLimit && !mZooming)
		{
			float viewSize = GetViewSize();
			CameraBaseSetting cameraBaseSetting = mBaseSetting;
			float num = 0f;
			if (viewSize < SoftViewSizeMin)
			{
				num = Time.deltaTime * 10f * cameraBaseSetting.ViewOverCloseLimitResetSpeed;
			}
			else if (viewSize > SoftViewSizeMax)
			{
				num = (0f - Time.deltaTime) * 10f * cameraBaseSetting.ViewOverLeaveLimitResetSpeed;
			}
			if (num != 0f)
			{
				SetViewSize(viewSize + num, zoom: true);
			}
		}
	}
	protected virtual void OnLateUpdate()
	{
		UpdateCameraFrame();
	}
	protected void UpdateCameraFrame()
	{
		CinemachineBrain cinemachineBrain = mMonoProxy.CinemachineBrain;
		if (!((Object)(object)cinemachineBrain == (Object)null) && (int)cinemachineBrain.m_UpdateMethod == 3)
		{
			cinemachineBrain.ManualUpdate();
		}
	}
	protected virtual void OnViewSizeChanged()
	{
	}
	private void UpdateViewSizeParameters()
	{
		mBaseSetting.GetViewSize(DefaultViewSize, GmCameraControlData.GmControl, out mSoftViewSizeMin, out mSoftViewSizeMax, out mZoomViewSizeMin, out mZoomViewSizeMax);
	}
	private void OnGameObjectDragBegin(OnGameObjectDragBeginResult result)
	{
		mDragging = true;
	}
	private void OnGameObjectDragEnd(OnGameObjectDragEndResult result)
	{
		mDragging = false;
	}
	private void OnGameObjectZoomBegin(OnGameObjectZoomBeginResult result)
	{
		if (EnableZoomInput && mBaseSetting.ZoomFactor != 0f)
		{
			mZooming = true;
		}
	}
	private void OnGameObjectZoom(OnGameObjectZoomResult result)
	{
		CameraBaseSetting cameraBaseSetting = mBaseSetting;
		if (EnableZoomInput && cameraBaseSetting.ZoomFactor != 0f)
		{
			ZoomViewSize(1f + result.ZoomFactor * mBaseSetting.ZoomFactor);
		}
	}
	private void OnGameObjectZoomEnd(OnGameObjectZoomEndResult result)
	{
		if (EnableZoomInput && mBaseSetting.ZoomFactor != 0f)
		{
			mZooming = false;
		}
	}
	private void ZoomViewSize(float scale)
	{
		float viewSize = GetViewSize();
		float num = viewSize * (scale - 1f);
		if (viewSize < SoftViewSizeMin && num < 0f)
		{
			float num2 = (viewSize - ZoomViewSizeMin) / (SoftViewSizeMin - ZoomViewSizeMin);
			num *= num2;
		}
		else if (viewSize > SoftViewSizeMax && num > 0f)
		{
			float num3 = (ZoomViewSizeMax - viewSize) / (ZoomViewSizeMax - SoftViewSizeMax);
			num *= num3;
		}
		SetViewSize(viewSize + num, zoom: true);
	}
	private float ClampStandardViewSize(float viewSize)
	{
		viewSize = Mathf.Clamp(viewSize, SoftViewSizeMin, SoftViewSizeMax);
		return viewSize;
	}
	private float ClampZoomViewSize(float viewSize)
	{
		viewSize = Mathf.Clamp(viewSize, ZoomViewSizeMin, ZoomViewSizeMax);
		return viewSize;
	}
}
}
