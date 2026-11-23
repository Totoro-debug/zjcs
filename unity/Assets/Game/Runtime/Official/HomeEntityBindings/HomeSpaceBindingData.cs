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
public class HomeSpaceBindingData
{
	public string SpaceId;

	public HomeSpaceRouteDefinition SpaceRoute = new HomeSpaceRouteDefinition();

	[NonSerialized]
	public Transform RuntimeRootTransform;

	[NonSerialized]
	public HomeRuntimeSpaceRoute RuntimeRoute;

	[NonSerialized]
	public List<HomeUnitBindingData> RuntimeUnitBindings = new List<HomeUnitBindingData>();

	public bool BuildRuntimeCache(Transform rootTransform)
	{
		if (string.IsNullOrEmpty(SpaceId))
		{
			return false;
		}
		RuntimeRootTransform = rootTransform;
		RuntimeUnitBindings.Clear();
		RuntimeRoute = new HomeRuntimeSpaceRoute
		{
			SpaceId = SpaceId
		};
		RuntimeRoute.NavigationGraph.SpaceId = SpaceId;
		for (int i = 0; i < SpaceRoute.Nodes.Count; i++)
		{
			HomeSpaceRouteNodeDefinition homeSpaceRouteNodeDefinition = SpaceRoute.Nodes[i];
			Vector3 worldPosition = rootTransform.TransformPoint(homeSpaceRouteNodeDefinition.LocalPosition);
			RuntimeRoute.Nodes.Add(new HomeRuntimeSpaceRouteNode
			{
				SpaceId = SpaceId,
				WorldPosition = worldPosition,
				RouteNodeId = homeSpaceRouteNodeDefinition.NodeId,
				Sorting = homeSpaceRouteNodeDefinition.Sorting
			});
			RuntimeRoute.BaseRouteNodeIndexMap[homeSpaceRouteNodeDefinition.NodeId] = i;
			RegisterRouteNodeInNavigationGraph(RuntimeRoute.NavigationGraph, homeSpaceRouteNodeDefinition.NodeId, worldPosition, homeSpaceRouteNodeDefinition.Sorting);
		}
		return true;
	}

	public bool HasRuntimeRoutes()
	{
		if (RuntimeRoute != null)
		{
			return RuntimeRoute.Nodes.Count >= 2;
		}
		return false;
	}

	private void RegisterRouteNodeInNavigationGraph(HomeRuntimeSpaceNavigationGraph graph, string routeNodeId, Vector3 worldPosition, HomeSortingDefinition sorting)
	{
		int count = graph.Nodes.Count;
		graph.Nodes.Add(new HomeRuntimeSpaceNavigationNode
		{
			NodeIndex = count,
			NodeKind = EHomeSpaceRouteEndpointKind.RouteNode,
			SpaceId = SpaceId,
			WorldPosition = worldPosition,
			RouteNodeId = routeNodeId,
			Sorting = sorting
		});
		graph.RouteNodeIndexMap[routeNodeId] = count;
		graph.Adjacency[count] = new List<int>();
	}
}
}
