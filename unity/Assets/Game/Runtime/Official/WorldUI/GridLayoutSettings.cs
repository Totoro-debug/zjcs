// Migrated from the pinned official Framework assembly; only decompiler syntax and casts are adapted.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UGUIJszx
{

[CreateAssetMenu(fileName = "GridLayoutSettings", menuName = "UI/GridLayoutSettings")]
public class GridLayoutSettings : ScriptableObject
{
	public List<GridLayoutSetting> settings = new List<GridLayoutSetting>();

	private void OnValidate()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		GameObject[] rootGameObjects = activeScene.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			GridLayoutGroupJSZX[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<GridLayoutGroupJSZX>();
			foreach (GridLayoutGroupJSZX gridLayoutGroupJSZX in componentsInChildren)
			{
				if (gridLayoutGroupJSZX.gridLayoutSettings == this)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(gridLayoutGroupJSZX.GetComponent<RectTransform>());
				}
			}
		}
	}
}

}
