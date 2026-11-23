// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public class HomeEntity
{
	public string PrefabPath;

	public GameObject GameObject;

	public IHomeEntityPrefabHandle Handle;

	public void Release()
	{
		if (!((Object)(object)GameObject == (Object)null))
		{
			GameObject.SetActive(false);
			Object.Destroy((Object)(object)GameObject);
			GameObject = null;
			Handle.Release();
			Handle = null;
		}
	}
}
}
