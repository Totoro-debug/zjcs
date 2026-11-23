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
public readonly struct HomeNavigationAutoConnectResult
{
	public readonly int FromNodeIndex;

	public readonly float RouteEdgeFromCost;

	public readonly int RouteEdgeFromNodeIndex;

	public readonly float RouteEdgeToCost;

	public readonly int RouteEdgeToNodeIndex;

	public readonly EHomeNavigationAutoConnectTargetKind TargetKind;

	public readonly float UnitPointCost;

	public readonly int UnitPointNodeIndex;

	private HomeNavigationAutoConnectResult(int fromNodeIndex, EHomeNavigationAutoConnectTargetKind targetKind, int unitPointNodeIndex, float unitPointCost, int routeEdgeFromNodeIndex, float routeEdgeFromCost, int routeEdgeToNodeIndex, float routeEdgeToCost)
	{
		FromNodeIndex = fromNodeIndex;
		RouteEdgeFromCost = routeEdgeFromCost;
		RouteEdgeFromNodeIndex = routeEdgeFromNodeIndex;
		RouteEdgeToCost = routeEdgeToCost;
		RouteEdgeToNodeIndex = routeEdgeToNodeIndex;
		TargetKind = targetKind;
		UnitPointCost = unitPointCost;
		UnitPointNodeIndex = unitPointNodeIndex;
	}

	public static HomeNavigationAutoConnectResult CreateUnitPoint(int fromNodeIndex, int unitPointNodeIndex, float cost)
	{
		return new HomeNavigationAutoConnectResult(fromNodeIndex, EHomeNavigationAutoConnectTargetKind.UnitPoint, unitPointNodeIndex, cost, -1, 0f, -1, 0f);
	}

	public static HomeNavigationAutoConnectResult CreateRouteEdge(int fromNodeIndex, int routeEdgeFromNodeIndex, float routeEdgeFromCost, int routeEdgeToNodeIndex, float routeEdgeToCost)
	{
		return new HomeNavigationAutoConnectResult(fromNodeIndex, EHomeNavigationAutoConnectTargetKind.RouteEdge, -1, 0f, routeEdgeFromNodeIndex, routeEdgeFromCost, routeEdgeToNodeIndex, routeEdgeToCost);
	}
}
}
