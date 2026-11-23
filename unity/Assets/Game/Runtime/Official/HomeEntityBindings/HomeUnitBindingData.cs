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
public class HomeUnitBindingData
{
	public string UnitInstanceId;

	[HomeReferenceSelector(HomeReferenceKind.SpaceId, true, false)]
	public string SpaceId;

	public HomeUnitDefinitionData UnitDefinition = new HomeUnitDefinitionData();

	public bool TryBuildRuntimeBinding(Transform rootTransform)
	{
		foreach (HomePointDefinition pointDefinition in UnitDefinition.PointDefinitions)
		{
			if (!string.IsNullOrEmpty(pointDefinition.PointId))
			{
				pointDefinition.WorldPosition = BuildWorldPose(rootTransform, pointDefinition.LocalPosition);
			}
		}
		return true;
	}

	public HomeUnitPointKey BuildUnitPointKey(string pointId)
	{
		return new HomeUnitPointKey(UnitInstanceId, pointId);
	}

	private static Vector3 BuildWorldPose(Transform rootTransform, Vector3 localPosition)
	{
		if (!((Object)(object)rootTransform != (Object)null))
		{
			return localPosition;
		}
		return rootTransform.TransformPoint(localPosition);
	}
}
}
