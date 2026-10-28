using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[DefaultExecutionOrder(-1)]
public class CameraControlBrain : MonoBehaviour
{
	private enum InitType
	{
		Once,
		MultiTimes
	}
	public CameraMonoProxy MonoProxy;
	[SerializeField]
	private InitType mInitType;
	private ICameraController mLastCameraController;
	private ICameraController mCameraController;
	private ICameraControllerDriven mCameraDriven;
	private readonly Dictionary<Type, ICameraController> mControllerDic = new Dictionary<Type, ICameraController>();
	private readonly List<ICameraController> mMonoControllersCache = new List<ICameraController>();
	public ICameraController CameraController => mCameraController;
	public Vector3 InitialViewPos { get; private set; }
	public Action<object> InjectDependencies { private get; set; }
	public ICameraControlManager CameraControlManager { get; set; }
	public event Action<Vector3> OnViewPosUpdated;
	public event Action<float> OnViewSizeUpdated;
	private void Awake()
	{
		if (mInitType == InitType.Once)
		{
			CameraControlManager.Init(this);
		}
	}
	private void OnEnable()
	{
		if (mInitType == InitType.MultiTimes)
		{
			CameraControlManager.Init(this);
		}
	}
	private void OnDestroy()
	{
		ClearController();
	}
	private void Update()
	{
		mCameraDriven?.DriveUpdate();
	}
	private void LateUpdate()
	{
		mCameraDriven?.DriveLateUpdate();
	}
	public void Init(Transform target)
	{
		MonoProxy.Target = target;
		InitialViewPos = ((Component)MonoProxy.CameraFollow).transform.position;
		if ((Object)(object)MonoProxy.CinemachineBrain == (Object)null)
		{
			Camera mainCamera = CameraUtils.GetMainCamera();
			MonoProxy.CinemachineBrain = ((Component)mainCamera).GetComponent<CinemachineBrain>();
		}
	}
	public void ClearController()
	{
		if (mCameraController != null)
		{
			mCameraDriven?.DriveExit();
			mCameraController.OnViewSizeUpdated = null;
			mCameraController.OnPosUpdated = null;
			mLastCameraController = mCameraController;
		}
	}
	public void SetController<T>(bool immediately) where T : ICameraController, new()
	{
		ApplyController(GetController<T>(), immediately);
	}
	private void ApplyController(ICameraController cameraController, bool immediately)
	{
		if (cameraController != null && mCameraController != cameraController)
		{
			ClearController();
			mCameraController = cameraController;
			cameraController.OnPosUpdated = OnCameraPosUpdate;
			cameraController.OnViewSizeUpdated = OnCameraDisUpdated;
			mCameraDriven = cameraController as ICameraControllerDriven;
			mCameraDriven?.DriveEnter(immediately);
			Debug.Log("CurController:" + cameraController.GetType());
		}
	}
	private void OnCameraPosUpdate(Vector3 pos)
	{
		OnViewPosUpdated?.Invoke(pos);
	}
	private void OnCameraDisUpdated(float distance)
	{
		OnViewSizeUpdated?.Invoke(distance);
	}
	public T GetController<T>() where T : ICameraController, new()
	{
		if (!mControllerDic.TryGetValue(typeof(T), out var value))
		{
			if (TryGetMonoController<T>(out var t))
			{
				value = t;
			}
			if (value == null)
			{
				if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
				{
					Debug.LogWarning(name + " missing camera MonoBehaviour: " + typeof(T).Name);
					return default;
				}
				value = new T();
				InjectDependencies(value);
				if (value is ICameraControllerDriven cameraControllerDriven)
				{
					cameraControllerDriven.Init(MonoProxy);
				}
			}
			mControllerDic.Add(typeof(T), value);
		}
		return (T)value;
	}
	private bool TryGetMonoController<T>(out T t) where T : ICameraController
	{
		mMonoControllersCache.Clear();
		((Component)this).GetComponents<ICameraController>(mMonoControllersCache);
		foreach (ICameraController item in mMonoControllersCache)
		{
			if (item is T val)
			{
				t = val;
				return true;
			}
		}
		t = default;
		return false;
	}
	public Vector3 GetViewPos()
	{
		return mCameraController.GetViewPos();
	}
	public void SetViewPos(Vector3 pos, bool immediately = false)
	{
		mCameraController.SetViewPos(pos, immediately);
	}
	public float GetViewSize()
	{
		return mCameraController.GetViewSize();
	}
	public void SetViewSize(float viewSize)
	{
		mCameraController.SetViewSize(viewSize, isZoom: false);
	}
	public void EnterStoryController()
	{
		SetController<CameraStoryController>(immediately: false);
	}
	public void ExitStoryController()
	{
		ExitController<CameraStoryController>(immediately: false);
	}
	public void ExitController<T>(bool immediately) where T : CameraBaseController
	{
		if (mCameraController == null || !(typeof(T) != mCameraController.GetType()))
		{
			ApplyController(mLastCameraController, immediately);
		}
	}
}
}
