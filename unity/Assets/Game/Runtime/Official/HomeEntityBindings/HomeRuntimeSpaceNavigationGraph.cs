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
public sealed class HomeRuntimeSpaceNavigationGraph
{
	public Dictionary<int, List<int>> Adjacency = new Dictionary<int, List<int>>();

	public List<HomeRuntimeSpaceNavigationEdge> Edges = new List<HomeRuntimeSpaceNavigationEdge>();

	public List<HomeRuntimeSpaceNavigationNode> Nodes = new List<HomeRuntimeSpaceNavigationNode>();

	public Dictionary<string, int> RouteNodeIndexMap = new Dictionary<string, int>();

	public string SpaceId;

	public Dictionary<HomeUnitPointKey, int> UnitPointIndexMap = new Dictionary<HomeUnitPointKey, int>();

	public bool IsRouteNode(int graphNodeIndex)
	{
		if (graphNodeIndex >= 0 && graphNodeIndex < Nodes.Count)
		{
			return Nodes[graphNodeIndex].NodeKind == EHomeSpaceRouteEndpointKind.RouteNode;
		}
		return false;
	}
}
}
