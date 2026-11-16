using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeCamera
{
[RequireComponent(typeof(Camera))]
public class UIBloomSwitch : MonoBehaviour
{
	[SerializeField]
	private Camera mCam;
	[SerializeField]
	private Volume mVolume;
	private int mLayerUI;
	private int mLayerBloom;
	private Action<bool> mBloomActiveChanged;
	public IUIBloomManager UIBloomManager { get; set; }
	private void Reset()
	{
		mCam = ((Component)this).GetComponent<Camera>();
	}
	private void Awake()
	{
		mLayerUI = LayerMask.NameToLayer("UI");
		mLayerBloom = LayerMask.NameToLayer("Bloom");
		mCam.cullingMask = (1 << mLayerUI) | (1 << mLayerBloom);
		if (CameraExtensions.GetUniversalAdditionalCameraData(mCam).renderPostProcessing)
		{
			Debug.LogWarning((object)"UI Bloom is enabled unintendedly");
		}
		mBloomActiveChanged = OnActiveChanged;
	}
	private void OnEnable()
	{
		bool bloomActive = UIBloomManager.BloomActive;
		UIBloomManager.RegisterActiveListener(mBloomActiveChanged);
		OnActiveChanged(bloomActive);
	}
	private void OnDisable()
	{
		UIBloomManager.UnregisterActiveListener(mBloomActiveChanged);
	}
	private void Activate()
	{
		CameraExtensions.GetUniversalAdditionalCameraData(mCam).renderPostProcessing = true;
		((Component)mVolume).gameObject.SetActive(true);
		CameraExtensions.SetVolumeFrameworkUpdateMode(mCam, (VolumeFrameworkUpdateMode)2);
	}
	private void Deactivate()
	{
		CameraExtensions.GetUniversalAdditionalCameraData(mCam).renderPostProcessing = false;
		((Component)mVolume).gameObject.SetActive(false);
		CameraExtensions.SetVolumeFrameworkUpdateMode(mCam, (VolumeFrameworkUpdateMode)1);
	}
	private void OnActiveChanged(bool isActive)
	{
		if (isActive)
		{
			Activate();
		}
		else
		{
			Deactivate();
		}
	}
}
}
