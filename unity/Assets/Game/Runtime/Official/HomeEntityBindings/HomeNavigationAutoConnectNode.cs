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
public readonly struct HomeNavigationAutoConnectNode
{
	public HomeNavigationAutoConnectNode(int nodeIndex, EHomeSpaceRouteEndpointKind nodeKind, Vector3 worldPosition, bool autoConnectToRoute, bool hasAnyEdge)
	{
		AutoConnectToRoute = autoConnectToRoute;
		HasAnyEdge = hasAnyEdge;
		NodeIndex = nodeIndex;
		NodeKind = nodeKind;
		WorldPosition = worldPosition;
	}

	public readonly bool AutoConnectToRoute;

	public readonly bool HasAnyEdge;

	public readonly int NodeIndex;

	public readonly EHomeSpaceRouteEndpointKind NodeKind;

	public readonly Vector3 WorldPosition;
}
}
