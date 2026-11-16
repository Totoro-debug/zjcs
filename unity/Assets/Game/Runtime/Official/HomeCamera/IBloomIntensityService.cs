using System;
using UnityEngine.Rendering.Universal;

namespace Zjcs.Official.HomeCamera
{
public interface IBloomIntensityService
{
	EBloomIntensityLevel BloomIntensityLevel { get; }

	bool BloomEnabled { get; }

	event Action OnBloomChange;

	void ChangeBloomIntensityLevel(EBloomIntensityLevel level);

	bool ModifyBloomIntensity(Bloom bloom, float newValue, out float oldValue);

	void RegisterVolume(BloomIntensityVolumeRefreshTrigger trigger);

	void UnregisterVolume(BloomIntensityVolumeRefreshTrigger trigger);
}
}
