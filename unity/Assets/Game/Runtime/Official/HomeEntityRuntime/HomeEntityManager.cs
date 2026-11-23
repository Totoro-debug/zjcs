// Selected original home entity registration/timeline implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityRuntime
{
using Object = UnityEngine.Object;
public class HomeEntityManager : IHomeEntityManager, IDisposable
{
	private HomeEntityManagerMono mEntityManagerMono;

	public HomeEntityManagerMono Mono => mEntityManagerMono;

	public IHomeEntityDataAgent EntityDataAgent => mEntityManagerMono;

	public void BindMono(HomeEntityManagerMono mono)
	{
		mEntityManagerMono = mono;
	}

	public GameObject GetFurnitureGameObject(FurnitureBindInfo bindInfo)
	{

		if ((Object)(object)mEntityManagerMono == (Object)null)
		{

			Debug.LogError("Official HomeEntityManager.GetFurnitureGameObject");
			return null;
		}
		GameObject orCreateEntity = mEntityManagerMono.GetOrCreateEntity(bindInfo.FurnitureType);
		if ((Object)(object)orCreateEntity == (Object)null)
		{

			Debug.LogError("Official HomeEntityManager.GetFurnitureGameObject");
			return null;
		}
		if (!string.IsNullOrEmpty(bindInfo.Key))
		{
			return FindKeyGameObject(orCreateEntity, bindInfo.Key);
		}
		return orCreateEntity;
	}

	public T GetFurnitureComponent<T>(FurnitureBindInfo bindInfo) where T : Component
	{
		GameObject furnitureGameObject = GetFurnitureGameObject(bindInfo);
		if ((Object)(object)furnitureGameObject == (Object)null)
		{
			return default;
		}
		T component = furnitureGameObject.GetComponent<T>();
		if ((Object)(object)component == (Object)null)
		{
			IFurnitureProxy<T> component2 = furnitureGameObject.GetComponent<IFurnitureProxy<T>>();
			if (component2 != null)
			{
				component = component2.GetComponent(bindInfo.FurnitureType);
			}
		}
		return component;
	}

	public GameObject GetThemeGameObject(ThemeBindInfo bindInfo)
	{
		if ((Object)(object)mEntityManagerMono == (Object)null)
		{
			return null;
		}
		GameObject orCreateEntity = mEntityManagerMono.GetOrCreateEntity(bindInfo.ThemeType);
		if ((Object)(object)orCreateEntity == (Object)null)
		{
			return null;
		}
		if (!string.IsNullOrEmpty(bindInfo.Key))
		{
			return FindKeyGameObject(orCreateEntity, bindInfo.Key);
		}
		return orCreateEntity;
	}

	private GameObject FindKeyGameObject(GameObject target, string key)
	{
		HomeEntityNodeRegister homeEntityNodeRegister = default;
		if (target.TryGetComponent<HomeEntityNodeRegister>(out homeEntityNodeRegister))
		{
			return homeEntityNodeRegister.GetGameObject(key);
		}
		return null;
	}

	public void Dispose()
	{
		mEntityManagerMono = null;
	}
}
}
