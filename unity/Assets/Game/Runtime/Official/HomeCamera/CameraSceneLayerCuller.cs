using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeCamera
{
[RequireComponent(typeof(Camera))]
public class CameraSceneLayerCuller : MonoBehaviour
{
	public class CameraAttrForwarder : IDisposable
	{
		private CameraAttrs mLastAttrs;
		private CameraSceneLayerCuller mCuller;
		private bool mHasDispose;
		public string ForwarderName { get; private set; }
		public CameraAttrForwarder(CameraSceneLayerCuller culler, CameraAttrs defaultAttr, string forwarderName)
		{
			ForwarderName = forwarderName;
			mCuller = culler;
			mLastAttrs = defaultAttr;
			mHasDispose = false;
		}
		private CameraAttrForwarder()
		{
		}
		public void RecordAttr(CameraAttrs attr)
		{
			if (mHasDispose)
			{

				Debug.LogError("CameraSceneLayerCuller");
			}
			else
			{
				mLastAttrs = attr;
			}
		}
		public void Dispose()
		{
			if (mHasDispose)
			{

				Debug.LogError("CameraSceneLayerCuller");
			}
			else
			{
				mHasDispose = true;
				mCuller.SetCamAttrs(mLastAttrs);
				mCuller.ClearForwarder();
				mCuller = null;
			}
		}
	}
	[SerializeField]
	[HideInInspector]
	private Camera mCamera;
	[SerializeField]
	private LayerMask mFullScreenUICullingMask;
	public ICameraWindowEvents WindowEvents { get; set; }
	public ICameraSceneEvents SceneEvents { get; set; }
	private int mFullScreenWindowsCounter;
	private LayerMask mCacheCameraLayer;
	private CameraAttrForwarder mCameraAttrForward;
	private bool mIsActive;
	public ICameraPerformanceSetting PerformanceSetting { get; set; }
	public IDisposable TakeOverCameraAttrs(string whoTakeOver)
	{
		if (mCameraAttrForward != null)
		{

			Debug.LogError("CameraSceneLayerCuller");
			return null;
		}
		mCameraAttrForward = new CameraAttrForwarder(this, new CameraAttrs
		{
			CullingMaskValue = mCamera.cullingMask,
			EnableRenderPostProcessing = CameraExtensions.GetUniversalAdditionalCameraData(mCamera).renderPostProcessing
		}, whoTakeOver);
		return mCameraAttrForward;
	}
	public void SetCamAttrs(CameraAttrs param)
	{
		mCamera.cullingMask = param.CullingMaskValue;
		CameraExtensions.GetUniversalAdditionalCameraData(mCamera).renderPostProcessing = param.EnableRenderPostProcessing;
	}
	private void ClearForwarder()
	{
		mCameraAttrForward = null;
	}
	private void Reset()
	{
		mCamera = ((Component)this).GetComponent<Camera>();
	}
	private void Start()
	{
		WindowEvents.OnWindowOpened += new Action<GameObject>(OnWindowsPopup);
		WindowEvents.OnWindowClosing += new Action<GameObject>(OnWindowsClosing);
		SceneEvents.OnPreLoadScene += new Action<CameraSceneData>(OnScenceLoad);
		mFullScreenWindowsCounter = 0;

		if (!((Component)mCamera).CompareTag("MainCamera"))
		{

			Debug.LogWarning("CameraSceneLayerCuller");
		}
		if ((Object)(object)mCamera != (Object)null && mCamera.cullingMask == 0)
		{

			Debug.LogError("CameraSceneLayerCuller");
		}
	}
	private void OnDestroy()
	{
		if (WindowEvents != null)
		{
			WindowEvents.OnWindowOpened -= new Action<GameObject>(OnWindowsPopup);
			WindowEvents.OnWindowClosing -= new Action<GameObject>(OnWindowsClosing);
		}
		if (SceneEvents != null)
		{
			SceneEvents.OnPreLoadScene -= new Action<CameraSceneData>(OnScenceLoad);
		}
	}
	private void OnEnable()
	{
		mIsActive = true;
	}
	private void OnDisable()
	{
		if (mFullScreenWindowsCounter > 0)
		{
			ResumeCamera();
			mFullScreenWindowsCounter = 0;
		}
		mIsActive = false;
	}
	private void OnWindowsPopup(GameObject go)
	{
		ICameraPopup popupWindow = default;
		if (mIsActive && WindowEvents.TryGetPopup(go, out popupWindow) && popupWindow.FullScreenDisplay && go.activeInHierarchy)
		{
			if (mFullScreenWindowsCounter == 0)
			{
				PauseCamera();
			}
			mFullScreenWindowsCounter++;
		}
	}
	private void OnWindowsClosing(GameObject go)
	{
		ICameraPopup popupWindow = default;
		if (mFullScreenWindowsCounter != 0 && mIsActive && WindowEvents.TryGetPopup(go, out popupWindow) && popupWindow.FullScreenDisplay)
		{
			mFullScreenWindowsCounter--;
			if (mFullScreenWindowsCounter == 0)
			{
				ResumeCamera();
			}
		}
	}
	private void PauseCamera()
	{
		mCacheCameraLayer = mCamera.cullingMask;
		if (mCamera.cullingMask == 0 && mCameraAttrForward != null)
		{

			Debug.LogError("CameraSceneLayerCuller");
		}
		if (mCameraAttrForward != null)
		{
			mCameraAttrForward.RecordAttr(new CameraAttrs
			{
				CullingMaskValue = mFullScreenUICullingMask,
				EnableRenderPostProcessing = false
			});
		}
		else
		{
			mCamera.cullingMask = mFullScreenUICullingMask;
			CameraExtensions.GetUniversalAdditionalCameraData(mCamera).renderPostProcessing = false;
		}
	}
	private void ResumeCamera()
	{
		if (mCameraAttrForward != null)
		{
			mCameraAttrForward.RecordAttr(new CameraAttrs
			{
				CullingMaskValue = mCacheCameraLayer,
				EnableRenderPostProcessing = PerformanceSetting.BloomEnabled
			});
		}
		else
		{
			mCamera.cullingMask = mCacheCameraLayer;
			CameraExtensions.GetUniversalAdditionalCameraData(mCamera).renderPostProcessing = PerformanceSetting.BloomEnabled;
		}
	}
	private void OnScenceLoad(CameraSceneData sceneData)
	{
		if (sceneData.SceneType == SceneType.Fight && mFullScreenWindowsCounter > 0)
		{
			ResumeCamera();
			mFullScreenWindowsCounter = 0;
		}
	}
}
}
