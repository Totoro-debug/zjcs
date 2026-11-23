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
public sealed class HomeSpaceRouteEndpointDefinition
{
	public EHomeSpaceRouteEndpointKind EndpointKind = EHomeSpaceRouteEndpointKind.RouteNode;

	[HomeReferenceSelector(HomeReferenceKind.RouteNodeId, true, false)]
	public string RouteNodeId;

	[HomeReferenceSelector(HomeReferenceKind.UnitInstanceId, true, false)]
	public string UnitInstanceId;

	[HomeReferenceSelector(HomeReferenceKind.UnitPointId, true, false)]
	public string PointId;

	public bool TryBuildRouteNodeKey(out string routeNodeId)
	{
		routeNodeId = string.Empty;
		if (EndpointKind != EHomeSpaceRouteEndpointKind.RouteNode || string.IsNullOrEmpty(RouteNodeId))
		{
			return false;
		}
		routeNodeId = RouteNodeId;
		return true;
	}

	public bool TryBuildUnitPointKey(out HomeUnitPointKey unitPointKey)
	{
		unitPointKey = new HomeUnitPointKey(UnitInstanceId, PointId);
		if (EndpointKind != EHomeSpaceRouteEndpointKind.UnitPoint)
		{
			return false;
		}
		return unitPointKey.IsValid();
	}
}
}
