using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeCamera
{
[RequireComponent(typeof(Volume))]
[DisallowMultipleComponent]
public class BloomIntensityVolumeRefreshTrigger : MonoBehaviour
{
	private Volume mVolume;
	private Bloom mBloom;
	public IBloomIntensityService BloomIntensityService { get; set; }
	public Volume Volume => mVolume;
	public Bloom Bloom => mBloom;
	public float BaseIntensity { get; private set; }
	public bool IsInitialized
	{
		get
		{
			if ((Object)(object)mVolume != (Object)null && (Object)(object)mBloom != (Object)null)
			{
				return ((VolumeParameter)mBloom.intensity).overrideState;
			}
			return false;
		}
	}
	private void Awake()
	{
		mVolume = ((Component)this).GetComponent<Volume>();
		if ((Object)(object)mVolume == (Object)null)
		{
			Debug.LogError("BloomIntensityVolumeRefreshTrigger: Volume missing on " + name);
		}
		else
		{
			VolumeProfile profile = mVolume.profile;
			if (!((Object)(object)profile == (Object)null) && profile.TryGet<Bloom>(out mBloom) && !((Object)(object)mBloom == (Object)null) && ((VolumeParameter)mBloom.intensity).overrideState)
			{
				BaseIntensity = ((VolumeParameter<float>)(object)mBloom.intensity).value;
				BloomIntensityService.RegisterVolume(this);
			}
		}
	}
	private void OnDestroy()
	{
		if (BloomIntensityService != null)
		{
			BloomIntensityService.UnregisterVolume(this);
		}
	}
}
}
