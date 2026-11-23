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
public class AppearancePartSide
{
	public AppearancePartSideType SideType;

	public string SkeletonAnimName;

	public GameObject[] GameObjects;

	public bool CheckActive(AppearancePartSideType curSide)
	{
		return SideType == curSide;
	}

	public void UpdateState(bool active)
	{
		if (GameObjects != null)
		{
			GameObject[] gameObjects = GameObjects;
			for (int i = 0; i < gameObjects.Length; i++)
			{
				gameObjects[i].SetActive(active);
			}
		}
	}
}

}
