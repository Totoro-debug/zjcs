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
public sealed class HomeUnitDefinitionData
{
	public List<HomePointDefinition> PointDefinitions = new List<HomePointDefinition>();

	public bool SupportsBehaviourTag(string behaviourTag)
	{
		if (string.IsNullOrEmpty(behaviourTag))
		{
			return false;
		}
		foreach (HomePointDefinition pointDefinition in PointDefinitions)
		{
			if (pointDefinition != null && pointDefinition.SupportsBehaviourTag(behaviourTag))
			{
				return true;
			}
		}
		return false;
	}

	public bool TryGetSupportsBehaviourPoint(string behaviourTag, out HomePointDefinition point)
	{
		point = null;
		if (string.IsNullOrEmpty(behaviourTag))
		{
			return false;
		}
		foreach (HomePointDefinition pointDefinition in PointDefinitions)
		{
			if (pointDefinition.SupportsBehaviourTag(behaviourTag))
			{
				point = pointDefinition;
				return true;
			}
		}
		return false;
	}

	public bool TryGetPointDefinition(string pointId, out HomePointDefinition definition)
	{
		definition = null;
		foreach (HomePointDefinition pointDefinition in PointDefinitions)
		{
			if (pointDefinition != null && string.Equals(pointDefinition.PointId, pointId))
			{
				definition = pointDefinition;
				return true;
			}
		}
		return false;
	}
}
}
