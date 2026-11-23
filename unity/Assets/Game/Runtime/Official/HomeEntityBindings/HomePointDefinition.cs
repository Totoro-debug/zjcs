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
public sealed class HomePointDefinition
{
	public string PointId;

	public List<HomePointBehaviourActionBinding> BehaviourActionBindings = new List<HomePointBehaviourActionBinding>();

	public Vector3 LocalPosition;

	[NonSerialized]
	public Vector3 WorldPosition;

	public HomeSortingDefinition Sorting;

	public bool IsRouteNode;

	public bool SupportsBehaviourTag(string behaviourTag)
	{
		AssetGuidProxy<ScriptableObject> actionGraphProxy;
		return TryGetActionGraphProxy(behaviourTag, out actionGraphProxy);
	}

	public bool TryGetActionGraphProxy(string behaviourId, out AssetGuidProxy<ScriptableObject> actionGraphProxy)
	{
		actionGraphProxy = null;
		if (string.IsNullOrEmpty(behaviourId) || BehaviourActionBindings == null)
		{
			return false;
		}
		foreach (HomePointBehaviourActionBinding behaviourActionBinding in BehaviourActionBindings)
		{
			if (behaviourActionBinding != null && string.Equals(behaviourActionBinding.BehaviourId, behaviourId, StringComparison.Ordinal))
			{
				if (behaviourActionBinding.ActionGraphProxy != null && !string.IsNullOrEmpty(behaviourActionBinding.ActionGraphProxy.AssetGuid))
				{
					actionGraphProxy = behaviourActionBinding.ActionGraphProxy;
					return true;
				}
				return false;
			}
		}
		return false;
	}
}
}
