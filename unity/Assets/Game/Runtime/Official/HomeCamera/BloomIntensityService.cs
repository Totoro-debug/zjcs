using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace Zjcs.Official.HomeCamera
{
public class BloomIntensityService : IBloomIntensityService
{
	private const float kPercentBase = 100f;
	private readonly IPersistentState<PerformanceSettingState> mLegacyPerformanceSettingState;
	private readonly IPersistentState<BloomIntensitySettingState> mPersistentState;
	private readonly List<BloomIntensityVolumeRefreshTrigger> mBloomIntensityVolumeTriggers = new List<BloomIntensityVolumeRefreshTrigger>();
	private readonly PerformanceLevel mPerformanceLevel;
	private EBloomIntensityLevel mBloomIntensityLevel = EBloomIntensityLevel.Full;
	private float mIntensityScale = 1f;
	public EBloomIntensityLevel BloomIntensityLevel => mBloomIntensityLevel;
	public bool BloomEnabled => BloomIntensityLevel != EBloomIntensityLevel.Zero;
	public event Action OnBloomChange;
	public BloomIntensityService(IPersistentState<BloomIntensitySettingState> persistentState, IPersistentState<PerformanceSettingState> legacyPerformanceSettingState, IDevice device)
	{
		mPersistentState = persistentState;
		mLegacyPerformanceSettingState = legacyPerformanceSettingState;

		if (persistentState == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		else if (device == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		else
		{
			mPerformanceLevel = device.GetPerformanceLevel();
			InitializeBloomIntensityLevel();
		}
	}
	public void ChangeBloomIntensityLevel(EBloomIntensityLevel level)
	{

		if (mPersistentState == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		else if (!Enum.IsDefined(typeof(EBloomIntensityLevel), level))
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		else
		{
			if (BloomIntensityLevel == level)
			{
				return;
			}
			bool bloomEnabled = BloomEnabled;
			mBloomIntensityLevel = level;
			mPersistentState.State.BloomIntensityLevel = mBloomIntensityLevel;
			CameraAsync.Observe(mPersistentState.WriteStateAsync());
			RefreshBloomIntensity();
			if (bloomEnabled == BloomEnabled)
			{
				return;
			}
			try
			{
				OnBloomChange?.Invoke();
			}
			catch (Exception exception)
			{

				Debug.LogException(exception);
			}
		}
	}
	public bool ModifyBloomIntensity(Bloom bloom, float newValue, out float oldValue)
	{
		oldValue = -1f;

		if ((Object)(object)bloom == (Object)null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return false;
		}
		if (!((VolumeParameter)bloom.intensity).overrideState)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return false;
		}
		if (!TryGetBloomIntensityVolumeTrigger(bloom, out var result))
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return false;
		}
		oldValue = result.BaseIntensity;
		ApplyBloomIntensity(result.Bloom, newValue);
		return true;
	}
	public void RegisterVolume(BloomIntensityVolumeRefreshTrigger trigger)
	{
		if ((Object)(object)trigger == (Object)null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		else if (trigger.IsInitialized)
		{
			if (!mBloomIntensityVolumeTriggers.Contains(trigger))
			{
				mBloomIntensityVolumeTriggers.Add(trigger);
			}
			ApplyBloomIntensity(trigger);
		}
	}
	public void UnregisterVolume(BloomIntensityVolumeRefreshTrigger trigger)
	{
		if (!((Object)(object)trigger == (Object)null))
		{
			mBloomIntensityVolumeTriggers.Remove(trigger);
		}
	}
	public void Initialize()
	{
		RefreshBloomIntensity();
		ScanExistingVolumes();
	}
	private void InitializeBloomIntensityLevel()
	{

		if (mPersistentState == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return;
		}
		if (mLegacyPerformanceSettingState == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return;
		}
		EBloomIntensityLevel? bloomIntensityLevel = mPersistentState.State.BloomIntensityLevel;
		if (bloomIntensityLevel.HasValue && Enum.IsDefined(typeof(EBloomIntensityLevel), bloomIntensityLevel.Value))
		{
			mBloomIntensityLevel = bloomIntensityLevel.Value;
			return;
		}
		if (bloomIntensityLevel.HasValue)
		{

			Debug.Log("BloomIntensityService: " + nameof(BloomIntensityService));
		}
		bool? bloomEnabled = mLegacyPerformanceSettingState.State.BloomEnabled;
		if (bloomEnabled.HasValue)
		{
			mBloomIntensityLevel = (bloomEnabled.Value ? EBloomIntensityLevel.Full : EBloomIntensityLevel.Zero);
			mPersistentState.State.BloomIntensityLevel = mBloomIntensityLevel;
			CameraAsync.Observe(mPersistentState.WriteStateAsync());
		}
		else
		{
			mBloomIntensityLevel = (GetDefaultBloomEnable() ? EBloomIntensityLevel.Full : EBloomIntensityLevel.Zero);
		}
	}
	private void RefreshBloomIntensity()
	{

		Debug.Log("BloomIntensityService: " + nameof(BloomIntensityService));
		if (mPersistentState == null)
		{

			Debug.LogError("BloomIntensityService: " + nameof(BloomIntensityService));
			return;
		}
		mIntensityScale = (float)BloomIntensityLevel / 100f;
		for (int num = mBloomIntensityVolumeTriggers.Count - 1; num >= 0; num--)
		{
			BloomIntensityVolumeRefreshTrigger bloomIntensityVolumeRefreshTrigger = mBloomIntensityVolumeTriggers[num];
			if ((Object)(object)bloomIntensityVolumeRefreshTrigger == (Object)null)
			{
				mBloomIntensityVolumeTriggers.RemoveAt(num);
			}
			else
			{
				ApplyBloomIntensity(bloomIntensityVolumeRefreshTrigger);
			}
		}
	}
	private void ScanExistingVolumes()
	{
		BloomIntensityVolumeRefreshTrigger[] array = Resources.FindObjectsOfTypeAll<BloomIntensityVolumeRefreshTrigger>();
		foreach (BloomIntensityVolumeRefreshTrigger bloomIntensityVolumeRefreshTrigger in array)
		{
			if (!((Object)(object)bloomIntensityVolumeRefreshTrigger == (Object)null))
			{
				Scene scene = ((Component)bloomIntensityVolumeRefreshTrigger).gameObject.scene;
				if (scene.IsValid() && bloomIntensityVolumeRefreshTrigger.IsInitialized)
				{
					RegisterVolume(bloomIntensityVolumeRefreshTrigger);
				}
			}
		}
	}
	private bool GetDefaultBloomEnable()
	{
		return mPerformanceLevel >= PerformanceLevel.Medium;
	}
	private void ApplyBloomIntensity(BloomIntensityVolumeRefreshTrigger trigger)
	{
		if (!((Object)(object)trigger == (Object)null))
		{
			ApplyBloomIntensity(trigger.Bloom, trigger.BaseIntensity);
		}
	}
	private void ApplyBloomIntensity(Bloom bloom, float baseIntensity)
	{
		if (!((Object)(object)bloom == (Object)null) && ((VolumeParameter)bloom.intensity).overrideState)
		{
			((VolumeParameter<float>)(object)bloom.intensity).value = baseIntensity * mIntensityScale;
		}
	}
	private bool TryGetBloomIntensityVolumeTrigger(Bloom bloom, out BloomIntensityVolumeRefreshTrigger result)
	{
		foreach (BloomIntensityVolumeRefreshTrigger mBloomIntensityVolumeTrigger in mBloomIntensityVolumeTriggers)
		{
			if (!((Object)(object)mBloomIntensityVolumeTrigger == (Object)null) && mBloomIntensityVolumeTrigger.Bloom == bloom)
			{
				result = mBloomIntensityVolumeTrigger;
				return true;
			}
		}
		result = null;
		return false;
	}
}
}
