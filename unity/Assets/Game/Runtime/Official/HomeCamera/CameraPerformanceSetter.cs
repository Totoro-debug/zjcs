using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeCamera
{
[RequireComponent(typeof(Camera))]
public class CameraPerformanceSetter : MonoBehaviour
{
	private Camera mCamera;
	private UniversalAdditionalCameraData mCameraData;
	public IBloomIntensityService BloomIntensityService { get; set; }
	private void Awake()
	{
		mCamera = ((Component)this).GetComponent<Camera>();
		mCameraData = ((Component)mCamera).GetComponent<UniversalAdditionalCameraData>();
		RefreshBloomSetting();
		BloomIntensityService.OnBloomChange += BloomIntensityService_OnBloomChange;
	}
	private void OnDestroy()
	{
		BloomIntensityService.OnBloomChange -= BloomIntensityService_OnBloomChange;
	}
	private void BloomIntensityService_OnBloomChange()
	{
		RefreshBloomSetting();
	}
	private void RefreshBloomSetting()
	{
		mCameraData.renderPostProcessing = BloomIntensityService.BloomEnabled;
	}
}
}
