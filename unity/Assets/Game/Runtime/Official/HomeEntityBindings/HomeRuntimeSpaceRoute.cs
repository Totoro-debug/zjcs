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
public sealed class HomeRuntimeSpaceRoute
{
	public Dictionary<string, int> BaseRouteNodeIndexMap = new Dictionary<string, int>();

	public HomeRuntimeSpaceNavigationGraph NavigationGraph = new HomeRuntimeSpaceNavigationGraph();

	public List<HomeRuntimeSpaceRouteNode> Nodes = new List<HomeRuntimeSpaceRouteNode>();

	public string SpaceId;

	public bool TryGetBaseRouteNode(string nodeId, out HomeRuntimeSpaceRouteNode routeNode)
	{
		if (BaseRouteNodeIndexMap.TryGetValue(nodeId, out var value) && value >= 0 && value < Nodes.Count)
		{
			routeNode = Nodes[value];
			return true;
		}
		routeNode = default;
		return false;
	}
}
}
