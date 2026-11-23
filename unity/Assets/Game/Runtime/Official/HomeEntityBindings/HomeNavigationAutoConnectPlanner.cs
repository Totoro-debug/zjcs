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
public static class HomeNavigationAutoConnectPlanner
{
	private readonly struct NearestRouteEdgeResult
	{
		public NearestRouteEdgeResult(HomeNavigationAutoConnectEdge edge, float projectT, float distanceSqr)
		{
			DistanceSqr = distanceSqr;
			Edge = edge;
			ProjectT = projectT;
		}

		public readonly float DistanceSqr;

		public readonly HomeNavigationAutoConnectEdge Edge;

		public readonly float ProjectT;
	}

	private readonly struct AutoConnectTarget
	{
		public AutoConnectTarget(int fromNodeIndex, float distanceSqr, HomeNavigationAutoConnectResult result)
		{
			DistanceSqr = distanceSqr;
			FromNodeIndex = fromNodeIndex;
			Result = result;
		}

		public readonly float DistanceSqr;

		public readonly int FromNodeIndex;

		public readonly HomeNavigationAutoConnectResult Result;
	}

	public static HomeNavigationAutoConnectPlan Build(List<HomeNavigationAutoConnectNode> nodes, List<HomeNavigationAutoConnectEdge> edges)
	{
		HomeNavigationAutoConnectPlan homeNavigationAutoConnectPlan = new HomeNavigationAutoConnectPlan();
		Dictionary<int, HomeNavigationAutoConnectNode> nodeMap = BuildNodeMap(nodes);
		List<int> list = CollectPendingUnitPointNodeIndexes(nodes);
		HashSet<int> hashSet = BuildRouteReachableNodeIndexes(nodes, edges);
		List<int> routeNodeIndexes = CollectRouteNodeIndexes(nodes);
		List<HomeNavigationAutoConnectEdge> routeEdges = CollectRouteNodeToRouteNodeEdges(edges);
		AutoConnectTarget target;
		while (list.Count > 0 && TryFindNearestAutoConnectTarget(nodeMap, list, hashSet, routeNodeIndexes, routeEdges, out target))
		{
			homeNavigationAutoConnectPlan.Results.Add(target.Result);
			list.Remove(target.FromNodeIndex);
			hashSet.Add(target.FromNodeIndex);
		}
		homeNavigationAutoConnectPlan.UnconnectedNodeIndexes.AddRange(list);
		return homeNavigationAutoConnectPlan;
	}

	private static Dictionary<int, HomeNavigationAutoConnectNode> BuildNodeMap(List<HomeNavigationAutoConnectNode> nodes)
	{
		Dictionary<int, HomeNavigationAutoConnectNode> dictionary = new Dictionary<int, HomeNavigationAutoConnectNode>();
		foreach (HomeNavigationAutoConnectNode node in nodes)
		{
			dictionary[node.NodeIndex] = node;
		}
		return dictionary;
	}

	private static List<int> CollectPendingUnitPointNodeIndexes(List<HomeNavigationAutoConnectNode> nodes)
	{
		List<int> list = new List<int>();
		foreach (HomeNavigationAutoConnectNode node in nodes)
		{
			if (node.NodeKind == EHomeSpaceRouteEndpointKind.UnitPoint && node.AutoConnectToRoute && !node.HasAnyEdge)
			{
				list.Add(node.NodeIndex);
			}
		}
		return list;
	}

	private static HashSet<int> BuildRouteReachableNodeIndexes(List<HomeNavigationAutoConnectNode> nodes, List<HomeNavigationAutoConnectEdge> edges)
	{
		HashSet<int> hashSet = new HashSet<int>();
		Queue<int> queue = new Queue<int>();
		Dictionary<int, List<int>> dictionary = BuildAdjacency(edges);
		foreach (HomeNavigationAutoConnectNode node in nodes)
		{
			if (node.NodeKind == EHomeSpaceRouteEndpointKind.RouteNode && hashSet.Add(node.NodeIndex))
			{
				queue.Enqueue(node.NodeIndex);
			}
		}
		while (queue.Count > 0)
		{
			int key = queue.Dequeue();
			if (!dictionary.TryGetValue(key, out var value))
			{
				continue;
			}
			foreach (int item in value)
			{
				if (hashSet.Add(item))
				{
					queue.Enqueue(item);
				}
			}
		}
		return hashSet;
	}

	private static Dictionary<int, List<int>> BuildAdjacency(List<HomeNavigationAutoConnectEdge> edges)
	{
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		foreach (HomeNavigationAutoConnectEdge edge in edges)
		{
			if (!dictionary.TryGetValue(edge.FromNodeIndex, out var value))
			{
				value = new List<int>();
				dictionary[edge.FromNodeIndex] = value;
			}
			value.Add(edge.ToNodeIndex);
		}
		return dictionary;
	}

	private static List<int> CollectRouteNodeIndexes(List<HomeNavigationAutoConnectNode> nodes)
	{
		List<int> list = new List<int>();
		foreach (HomeNavigationAutoConnectNode node in nodes)
		{
			if (node.NodeKind == EHomeSpaceRouteEndpointKind.RouteNode)
			{
				list.Add(node.NodeIndex);
			}
		}
		return list;
	}

	private static List<HomeNavigationAutoConnectEdge> CollectRouteNodeToRouteNodeEdges(List<HomeNavigationAutoConnectEdge> edges)
	{
		List<HomeNavigationAutoConnectEdge> list = new List<HomeNavigationAutoConnectEdge>();
		foreach (HomeNavigationAutoConnectEdge edge in edges)
		{
			if (edge.IsRouteNodeToRouteNode)
			{
				list.Add(edge);
			}
		}
		return list;
	}

	private static bool TryFindNearestAutoConnectTarget(Dictionary<int, HomeNavigationAutoConnectNode> nodeMap, List<int> pendingNodeIndexes, HashSet<int> routeReachableNodeIndexes, List<int> routeNodeIndexes, List<HomeNavigationAutoConnectEdge> routeEdges, out AutoConnectTarget target)
	{
		target = default;
		bool flag = false;
		foreach (int pendingNodeIndex in pendingNodeIndexes)
		{
			if (nodeMap.TryGetValue(pendingNodeIndex, out var value) && TryFindAutoConnectTargetForUnitPoint(nodeMap, value, routeReachableNodeIndexes, routeNodeIndexes, routeEdges, out var target2) && (!flag || !(target2.DistanceSqr >= target.DistanceSqr)))
			{
				target = target2;
				flag = true;
			}
		}
		return flag;
	}

	private static bool TryFindAutoConnectTargetForUnitPoint(Dictionary<int, HomeNavigationAutoConnectNode> nodeMap, HomeNavigationAutoConnectNode unitPointNode, HashSet<int> routeReachableNodeIndexes, List<int> routeNodeIndexes, List<HomeNavigationAutoConnectEdge> routeEdges, out AutoConnectTarget target)
	{
		target = default;
		bool flag = FindNearestRouteNodeToRouteNodeEdge(nodeMap, routeEdges, unitPointNode.WorldPosition, out var nearest);
		bool flag2 = FindNearestRouteNode(nodeMap, routeNodeIndexes, unitPointNode.WorldPosition, out var nearestDistanceSqr);
		bool flag3 = FindNearestReachableUnitPoint(nodeMap, routeReachableNodeIndexes, unitPointNode, out var nearestNode, out var nearestDistanceSqr2);
		if (flag3 && (!flag2 || nearestDistanceSqr2 < nearestDistanceSqr))
		{
			float cost = Vector3.Distance(unitPointNode.WorldPosition, nearestNode.WorldPosition);
			HomeNavigationAutoConnectResult result = HomeNavigationAutoConnectResult.CreateUnitPoint(unitPointNode.NodeIndex, nearestNode.NodeIndex, cost);
			target = new AutoConnectTarget(unitPointNode.NodeIndex, nearestDistanceSqr2, result);
			return true;
		}
		if (flag)
		{
			float routeEdgeFromCost = nearest.Edge.Cost * nearest.ProjectT;
			float routeEdgeToCost = nearest.Edge.Cost * (1f - nearest.ProjectT);
			HomeNavigationAutoConnectResult result2 = HomeNavigationAutoConnectResult.CreateRouteEdge(unitPointNode.NodeIndex, nearest.Edge.FromNodeIndex, routeEdgeFromCost, nearest.Edge.ToNodeIndex, routeEdgeToCost);
			target = new AutoConnectTarget(unitPointNode.NodeIndex, nearest.DistanceSqr, result2);
			return true;
		}
		if (flag3)
		{
			float cost2 = Vector3.Distance(unitPointNode.WorldPosition, nearestNode.WorldPosition);
			HomeNavigationAutoConnectResult result3 = HomeNavigationAutoConnectResult.CreateUnitPoint(unitPointNode.NodeIndex, nearestNode.NodeIndex, cost2);
			target = new AutoConnectTarget(unitPointNode.NodeIndex, nearestDistanceSqr2, result3);
			return true;
		}
		return false;
	}

	private static bool FindNearestRouteNode(Dictionary<int, HomeNavigationAutoConnectNode> nodeMap, List<int> routeNodeIndexes, Vector3 worldPosition, out float nearestDistanceSqr)
	{
		nearestDistanceSqr = float.MaxValue;
		bool result = false;
		foreach (int routeNodeIndex in routeNodeIndexes)
		{
			if (nodeMap.TryGetValue(routeNodeIndex, out var value))
			{
				Vector3 val = worldPosition - value.WorldPosition;
				float sqrMagnitude = val.sqrMagnitude;
				if (!(sqrMagnitude >= nearestDistanceSqr))
				{
					nearestDistanceSqr = sqrMagnitude;
					result = true;
				}
			}
		}
		return result;
	}

	private static bool FindNearestReachableUnitPoint(Dictionary<int, HomeNavigationAutoConnectNode> nodeMap, HashSet<int> routeReachableNodeIndexes, HomeNavigationAutoConnectNode fromNode, out HomeNavigationAutoConnectNode nearestNode, out float nearestDistanceSqr)
	{
		nearestNode = default;
		nearestDistanceSqr = float.MaxValue;
		bool result = false;
		foreach (int routeReachableNodeIndex in routeReachableNodeIndexes)
		{
			if (nodeMap.TryGetValue(routeReachableNodeIndex, out var value) && value.NodeKind == EHomeSpaceRouteEndpointKind.UnitPoint && value.NodeIndex != fromNode.NodeIndex)
			{
				Vector3 val = fromNode.WorldPosition - value.WorldPosition;
				float sqrMagnitude = val.sqrMagnitude;
				if (!(sqrMagnitude >= nearestDistanceSqr))
				{
					nearestDistanceSqr = sqrMagnitude;
					nearestNode = value;
					result = true;
				}
			}
		}
		return result;
	}

	private static bool FindNearestRouteNodeToRouteNodeEdge(Dictionary<int, HomeNavigationAutoConnectNode> nodeMap, List<HomeNavigationAutoConnectEdge> routeEdges, Vector3 worldPosition, out NearestRouteEdgeResult nearest)
	{
		nearest = default;
		bool result = false;
		float num = float.MaxValue;
		foreach (HomeNavigationAutoConnectEdge routeEdge in routeEdges)
		{
			if (!nodeMap.TryGetValue(routeEdge.FromNodeIndex, out var value) || !nodeMap.TryGetValue(routeEdge.ToNodeIndex, out var value2))
			{
				continue;
			}
			Vector3 val = value2.WorldPosition - value.WorldPosition;
			float sqrMagnitude = val.sqrMagnitude;
			if (!(sqrMagnitude <= 0f))
			{
				float num2 = Mathf.Clamp01(Vector3.Dot(worldPosition - value.WorldPosition, val) / sqrMagnitude);
				Vector3 val2 = value.WorldPosition + val * num2;
				Vector3 val3 = worldPosition - val2;
				float sqrMagnitude2 = val3.sqrMagnitude;
				if (!(sqrMagnitude2 >= num))
				{
					num = sqrMagnitude2;
					nearest = new NearestRouteEdgeResult(routeEdge, num2, sqrMagnitude2);
					result = true;
				}
			}
		}
		return result;
	}
}
}
