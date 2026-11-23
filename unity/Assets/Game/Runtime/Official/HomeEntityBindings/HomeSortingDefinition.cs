// Selected original home binding implementation; see the source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityBindings
{
using Object = UnityEngine.Object;
[Serializable]
public struct HomeSortingDefinition
{
	public string SortingLayerName;

	public int SortingOrder;

	public readonly bool TryBuildRenderState(out HomeActorRenderState state)
	{
		state = default;
		if (string.IsNullOrEmpty(SortingLayerName) || string.Equals(SortingLayerName, "Default"))
		{
			return false;
		}
		state = new HomeActorRenderState
		{
			SortingLayerName = SortingLayerName,
			SortingOrder = SortingOrder
		};
		return true;
	}
}
}
