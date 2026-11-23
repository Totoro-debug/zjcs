// Selected original home model light/clock implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Spine;
using Spine.Unity;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.ProfessionUI;
using IHomeEntityManager = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;
namespace Zjcs.Official.HomeModelEnvironment
{
using Object = UnityEngine.Object;
public class HomeLightController : MonoBehaviour
{
	[Serializable]
	private class HomeLightObj
	{
		public string Name;

		public GameObject GameObject;

		[NonSerialized]
		public bool IsOn;
	}

	[Serializable]
	private struct HomeLightTransition
	{
		public HomeLightObj HomeLightObj;

		public bool IsOn;
	}

	[SerializeField]
	private float mLightTranslateDuration;

	[SerializeField]
	private List<HomeLightObj> mLightObjs;

	[SerializeField]
	private Light2D[] mIntensityControlLights;

	[SerializeField]
	private MonoDictionary<string, FurnitureBindInfo> mFurnitureLightDict;

	private HomeEntityRuntimeLifetime mLifetime;

	private readonly Queue<HomeLightTransition> mLightTransitions = new Queue<HomeLightTransition>();

	private float[] mLightsOriginalIntensity;

	private float mTimer;

	private bool mStarted;
	public IHomeModelEnvironmentServices Services { get; set; }

	public IHomeEntityManager HomeEntityManager { get; set; }

	private void Awake()
	{
		mLifetime = new HomeEntityRuntimeLifetime();
		mLifetime.Add(Services.SubscribePlayerBehaviour(OnNotify));
		mLightsOriginalIntensity = new float[mIntensityControlLights.Length];
		int i = 0;
		for (int num = mIntensityControlLights.Length; i < num; i++)
		{
			mLightsOriginalIntensity[i] = mIntensityControlLights[i].intensity;
		}
		foreach (HomeLightObj mLightObj in mLightObjs)
		{
			mLightObj.GameObject.SetActive(mLightObj.IsOn);
		}
	}

	private void Start()
	{
		InitFurnitureLights();
		mStarted = true;
		UpdateLights(Services.CurrentPlayerBehaviour, immediately: true);
	}

	private void OnEnable()
	{
		if (mStarted)
		{
			UpdateLights(Services.CurrentPlayerBehaviour, immediately: true);
		}
	}

	private void OnDestroy()
	{
		mLifetime.Dispose();
	}

	private void InitFurnitureLights()
	{
		string text = default;
		FurnitureBindInfo furnitureBindInfo = default;
		foreach (KeyValuePair<string, FurnitureBindInfo> item in mFurnitureLightDict)
		{
			item.Deconstruct(out text, out furnitureBindInfo);
			string name = text;
			FurnitureBindInfo bindInfo = furnitureBindInfo;
			GameObject furnitureGameObject = HomeEntityManager.GetFurnitureGameObject(bindInfo);
			if ((Object)(object)furnitureGameObject != (Object)null)
			{
				mLightObjs.Add(new HomeLightObj
				{
					Name = name,
					GameObject = furnitureGameObject
				});
			}
		}
	}

	private void OnNotify(PlayerBehaviourNotifyData data)
	{
		if (!Services.IsHomeTimeLimit && (data.ActTiming == BehaviourActTiming.EnterEnd || data.ActTiming == BehaviourActTiming.ExitEnd))
		{
			UpdateLights(data.BehaviourId, data.Immediately);
		}
	}

	private void UpdateLights(string behaviourType, bool immediately)
	{
		HomeDayTime homeDayTime = Services.HomeDayTime;
		IEnumerable<HomeLight> homeLightStatus = Services.LightConfiguration.GetHomeLightStatus(homeDayTime, behaviourType);
		if (homeLightStatus != null)
		{
			SetTransitions(homeLightStatus, immediately);
		}
	}

	private void Update()
	{
		if (mLightTransitions.Count != 0)
		{
			mTimer -= Services.PlayableDeltaTime;
			if (mTimer <= 0f)
			{
				DoHomeLightTransition(mLightTransitions.Dequeue());
				mTimer = mLightTranslateDuration;
				UpdateLightsIntensity();
			}
		}
	}

	private void SetTransitions(IEnumerable<HomeLight> homeLights, bool immediately)
	{
		ClearTransitions();
		foreach (HomeLight homeLight in homeLights)
		{
			foreach (HomeLightObj mLightObj in mLightObjs)
			{
				if (string.Equals(mLightObj.Name, homeLight.Name) && mLightObj.IsOn != homeLight.IsOn)
				{
					mLightTransitions.Enqueue(new HomeLightTransition
					{
						HomeLightObj = mLightObj,
						IsOn = homeLight.IsOn
					});
				}
			}
		}
		if (immediately)
		{
			ClearTransitions();
		}
		else
		{
			mTimer = 0f;
		}
	}

	private void ClearTransitions()
	{
		while (mLightTransitions.Count > 0)
		{
			DoHomeLightTransition(mLightTransitions.Dequeue());
		}
		mLightTransitions.Clear();
		mTimer = 0f;
		UpdateLightsIntensity();
	}

	private void UpdateLightsIntensity()
	{
		int count = mLightObjs.Count;
		int num = 0;
		foreach (HomeLightObj mLightObj in mLightObjs)
		{
			if (mLightObj.IsOn)
			{
				num++;
			}
		}
		float num2 = 1f * (float)num / (float)count;
		int i = 0;
		for (int num3 = mIntensityControlLights.Length; i < num3; i++)
		{
			float num4 = mLightsOriginalIntensity[i] * num2;
			mIntensityControlLights[i].intensity = num4;
			((Component)mIntensityControlLights[i]).gameObject.SetActive(num4 > 0f);
		}
	}

	private void DoHomeLightTransition(HomeLightTransition transition)
	{
		HomeLightObj homeLightObj = transition.HomeLightObj;
		GameObject val = homeLightObj.GameObject;
		if ((Object)(object)val == (Object)null && mFurnitureLightDict.TryGetValue(homeLightObj.Name, out var value))
		{
			val = HomeEntityManager.GetFurnitureGameObject(value);
		}
		if ((Object)(object)val != (Object)null)
		{
			val.SetActive(transition.IsOn);
		}
		homeLightObj.IsOn = transition.IsOn;
	}
}
}
