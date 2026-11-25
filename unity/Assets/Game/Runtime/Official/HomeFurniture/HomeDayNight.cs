// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

[Serializable]
public class HomeDayNight : MonoBehaviour
{
	public WorldStateSwitcher<bool> DayNightSwitcher;

	private Action<bool> mOnDayNightChange;
	private bool mSubscribed;

	public IHomeDayNightServices EnvironmentServices { get; set; }

	public void Bind(IHomeDayNightServices services)
	{
		if (EnvironmentServices == services)
		{
			if (EnvironmentServices != null)
			{
				OnDayNightChange(EnvironmentServices.IsPlayableDay);
			}
			return;
		}
		Unsubscribe();
		EnvironmentServices = services;
		if (isActiveAndEnabled)
		{
			SubscribeAndSync();
		}
	}

	private void SubscribeAndSync()
	{
		if (EnvironmentServices == null)
		{
			return;
		}
		if (!mSubscribed)
		{
			EnvironmentServices.OnDayNightChange += mOnDayNightChange;
			mSubscribed = true;
		}
		OnDayNightChange(EnvironmentServices.IsPlayableDay);
	}

	private void Unsubscribe()
	{
		if (!mSubscribed || EnvironmentServices == null)
		{
			mSubscribed = false;
			return;
		}
		EnvironmentServices.OnDayNightChange -= mOnDayNightChange;
		mSubscribed = false;
	}

	private void Awake()
	{
		mOnDayNightChange = OnDayNightChange;
	}

	private void OnEnable()
	{
		SubscribeAndSync();
	}

	private void OnDisable()
	{
		Unsubscribe();
	}

	private void OnDayNightChange(bool isDay)
	{
		DayNightSwitcher?.SetState(isDay);
	}
}

}
