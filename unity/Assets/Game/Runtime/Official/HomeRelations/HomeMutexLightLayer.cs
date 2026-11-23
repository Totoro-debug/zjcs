// Selected original home overlap/light relation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeRelations
{
using Object = UnityEngine.Object;
[ExecuteAlways]
[RequireComponent(typeof(Light2D))]
public class HomeMutexLightLayer : HomeMutex
{
	[SerializeField]
	private Light2D mLight;

	private int[] mDefaultLayers;

	[SerializeField]
	private SortingLayerMask mLayersToRemove;

	private static readonly FieldInfo sApplyToSortingLayersField = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);

	public bool Changed { get; private set; }

	private static int[] GetApplyToSortingLayers(Light2D light)
	{
		return sApplyToSortingLayersField?.GetValue(light) as int[];
	}

	private static void SetApplyToSortingLayers(Light2D light, int[] layers)
	{
		sApplyToSortingLayersField?.SetValue(light, layers);
	}

	private void Awake()
	{
		if (mLayersToRemove.LayerIDs == null || mLayersToRemove.LayerIDs.Length == 0)
		{

			Debug.LogError("Official HomeMutexLightLayer.Awake", ((Component)this).gameObject);
		}
		else
		{
			mDefaultLayers = GetApplyToSortingLayers(mLight);
		}
	}

	public override void OnChange()
	{
		if (Changed)
		{
			return;
		}
		int[] applyToSortingLayers = GetApplyToSortingLayers(mLight);
		if (applyToSortingLayers == null)
		{
			return;
		}
		List<int> list = new List<int>(applyToSortingLayers);
		int[] layerIDs = mLayersToRemove.LayerIDs;
		foreach (int layerID in layerIDs)
		{
			list.RemoveAll((int id) => id == layerID);
		}
		SetApplyToSortingLayers(mLight, list.ToArray());
		Changed = true;
	}

	public override void OnRestore()
	{
		if (Changed)
		{
			if (mDefaultLayers != null)
			{
				SetApplyToSortingLayers(mLight, mDefaultLayers);
			}
			Changed = false;
		}
	}
}
}
