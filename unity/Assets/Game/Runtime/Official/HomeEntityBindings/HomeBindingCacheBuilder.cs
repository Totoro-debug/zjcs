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
public sealed class HomeBindingCacheBuilder
{
	private sealed class UnitSpaceOverrideProfileMap
	{
		public readonly Dictionary<string, string> UnitInstanceIdSpaceDict = new Dictionary<string, string>();
	}

	private const float kMinimumAutoConnectEdgeCost = 0.001f;

	private readonly List<HomeBindingAuthoring> mBindingAuthoringCache = new List<HomeBindingAuthoring>();

	private readonly Dictionary<EHomeActorBindingProfile, List<HomeBindingAuthoring>> mProfileDict = new Dictionary<EHomeActorBindingProfile, List<HomeBindingAuthoring>>();

	private readonly List<HomeUnitSpaceOverrideData> mUnitSpaceOverrideCache = new List<HomeUnitSpaceOverrideData>();

	private readonly Dictionary<EHomeActorBindingProfile, UnitSpaceOverrideProfileMap> mUnitSpaceOverrideDict = new Dictionary<EHomeActorBindingProfile, UnitSpaceOverrideProfileMap>();

	public HomeRuntimeBindingSnapshot BuildSnapshot(IHomeEntityManager homeEntityManager)
	{
		IHomeEntityDataAgent entityDataAgent = homeEntityManager.EntityDataAgent;
		mBindingAuthoringCache.Clear();
		entityDataAgent.GetDatas(mBindingAuthoringCache);
		return BuildSnapshotForAuthorings(mBindingAuthoringCache, new UnitSpaceOverrideProfileMap());
	}

	public void BuildSnapshots(IHomeEntityManager homeEntityManager, Dictionary<EHomeActorBindingProfile, HomeRuntimeBindingSnapshot> snapshots)
	{
		snapshots.Clear();
		IHomeEntityDataAgent entityDataAgent = homeEntityManager.EntityDataAgent;
		mBindingAuthoringCache.Clear();
		entityDataAgent.GetDatas(mBindingAuthoringCache);
		mUnitSpaceOverrideCache.Clear();
		entityDataAgent.GetDatas(mUnitSpaceOverrideCache);
		BuildUnitSpaceOverrideProfileMaps();
		foreach (KeyValuePair<EHomeActorBindingProfile, List<HomeBindingAuthoring>> item in mProfileDict)
		{
			item.Value.Clear();
		}
		foreach (HomeBindingAuthoring item2 in mBindingAuthoringCache)
		{
			if (!mProfileDict.TryGetValue(item2.Profile, out var value))
			{
				value = new List<HomeBindingAuthoring>();
				mProfileDict[item2.Profile] = value;
			}
			value.Add(item2);
		}
		foreach (KeyValuePair<EHomeActorBindingProfile, List<HomeBindingAuthoring>> item3 in mProfileDict)
		{
			if (item3.Value.Count != 0)
			{
				snapshots[item3.Key] = BuildSnapshotForAuthorings(item3.Value, mUnitSpaceOverrideDict[item3.Key]);
			}
		}
		foreach (KeyValuePair<EHomeActorBindingProfile, List<HomeBindingAuthoring>> item4 in mProfileDict)
		{
			item4.Value.Clear();
		}
	}

	private void BuildUnitSpaceOverrideProfileMaps()
	{
		mUnitSpaceOverrideDict.Clear();
		EHomeActorBindingProfile[] array = (EHomeActorBindingProfile[])Enum.GetValues(typeof(EHomeActorBindingProfile));
		foreach (EHomeActorBindingProfile key in array)
		{
			mUnitSpaceOverrideDict.Add(key, new UnitSpaceOverrideProfileMap());
		}
		foreach (HomeUnitSpaceOverrideData item in mUnitSpaceOverrideCache)
		{
			foreach (HomeUnitSpaceOverrideProfileData profile in item.Profiles)
			{
				BuildUnitSpaceOverrideProfileMap(profile, mUnitSpaceOverrideDict[profile.Profile]);
			}
		}
	}

	private static void BuildUnitSpaceOverrideProfileMap(HomeUnitSpaceOverrideProfileData profileData, UnitSpaceOverrideProfileMap profileMap)
	{
		foreach (HomeUnitSpaceOverrideEntry unitOverride in profileData.UnitOverrides)
		{
			if (!profileMap.UnitInstanceIdSpaceDict.TryAdd(unitOverride.UnitInstanceId, unitOverride.SpaceId))
			{

				Debug.LogError("Official HomeBindingCacheBuilder.BuildUnitSpaceOverrideProfileMap");
			}
		}
	}

	private HomeRuntimeBindingSnapshot BuildSnapshotForAuthorings(List<HomeBindingAuthoring> authorings, UnitSpaceOverrideProfileMap overrideProfileMap)
	{
		HomeRuntimeBindingSnapshot homeRuntimeBindingSnapshot = new HomeRuntimeBindingSnapshot();
		foreach (HomeBindingAuthoring authoring in authorings)
		{
			BuildSpaceCaches(authoring, homeRuntimeBindingSnapshot);
		}
		Dictionary<string, string> effectiveSpaceIdMap = BuildUnitEffectiveSpaceIdMap(authorings, homeRuntimeBindingSnapshot, overrideProfileMap);
		foreach (HomeBindingAuthoring authoring2 in authorings)
		{
			BuildUnitCaches(authoring2, homeRuntimeBindingSnapshot, effectiveSpaceIdMap);
		}
		AttachUnitBindingsToSpace(homeRuntimeBindingSnapshot);
		foreach (HomeSpaceBindingData space in homeRuntimeBindingSnapshot.Spaces)
		{
			BuildNavigationGraphEdges(space);
		}
		foreach (HomeBindingAuthoring authoring3 in authorings)
		{
			BuildChannelCaches(authoring3, homeRuntimeBindingSnapshot);
		}
		BuildSemanticLocationIndexes(homeRuntimeBindingSnapshot);
		return homeRuntimeBindingSnapshot;
	}

	private void BuildSpaceCaches(HomeBindingAuthoring authoring, HomeRuntimeBindingSnapshot snapshot)
	{
		foreach (HomeSpaceBindingData space in authoring.Spaces)
		{
			if (space.BuildRuntimeCache(((Component)authoring).transform))
			{
				snapshot.Spaces.Add(space);
			}
		}
	}

	private static void BuildUnitCaches(HomeBindingAuthoring authoring, HomeRuntimeBindingSnapshot snapshot, Dictionary<string, string> effectiveSpaceIdMap)
	{
		foreach (HomeUnitBindingData unit in authoring.Units)
		{
			if (effectiveSpaceIdMap.TryGetValue(unit.UnitInstanceId, out var value) && unit.TryBuildRuntimeBinding(((Component)authoring).transform))
			{
				snapshot.UnitBindings.Add(unit);
				snapshot.UnitEffectiveSpaceIdIndex.Add(unit.UnitInstanceId, value);
			}
		}
	}

	private static Dictionary<string, string> BuildUnitEffectiveSpaceIdMap(List<HomeBindingAuthoring> authorings, HomeRuntimeBindingSnapshot snapshot, UnitSpaceOverrideProfileMap overrideProfileMap)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (HomeBindingAuthoring authoring in authorings)
		{
			foreach (HomeUnitBindingData unit in authoring.Units)
			{
				string value = unit.SpaceId;
				if (overrideProfileMap.UnitInstanceIdSpaceDict.TryGetValue(unit.UnitInstanceId, out var value2))
				{
					if (snapshot.FindSpace(value2) == null)
					{

						Debug.LogError("Official HomeBindingCacheBuilder.BuildUnitEffectiveSpaceIdMap");

						Debug.LogError("Official HomeBindingCacheBuilder.BuildUnitEffectiveSpaceIdMap");
						continue;
					}
					value = value2;
				}
				dictionary.Add(unit.UnitInstanceId, value);
			}
		}
		return dictionary;
	}

	private static void AttachUnitBindingsToSpace(HomeRuntimeBindingSnapshot snapshot)
	{
		foreach (HomeUnitBindingData unitBinding in snapshot.UnitBindings)
		{
			AttachUnitBindingToSpace(snapshot, unitBinding, snapshot.GetUnitEffectiveSpaceId(unitBinding.UnitInstanceId));
		}
	}

	private static void AttachUnitBindingToSpace(HomeRuntimeBindingSnapshot snapshot, HomeUnitBindingData unitBinding, string effectiveSpaceId)
	{
		if (string.IsNullOrEmpty(effectiveSpaceId))
		{
			WarnUnitPointSpaceMissing(unitBinding);
			return;
		}
		HomeSpaceBindingData homeSpaceBindingData = snapshot.FindSpace(effectiveSpaceId);
		if (homeSpaceBindingData == null)
		{
			WarnUnitPointSpaceNotFound(unitBinding, effectiveSpaceId);
			return;
		}
		homeSpaceBindingData.RuntimeUnitBindings.Add(unitBinding);
		RegisterUnitPointsInNavigationGraph(homeSpaceBindingData, unitBinding);
	}

	private static void RegisterUnitPointsInNavigationGraph(HomeSpaceBindingData space, HomeUnitBindingData unitBinding)
	{
		HomeRuntimeSpaceNavigationGraph navigationGraph = space.RuntimeRoute.NavigationGraph;
		foreach (HomePointDefinition pointDefinition in unitBinding.UnitDefinition.PointDefinitions)
		{
			if (string.IsNullOrEmpty(pointDefinition.PointId))
			{
				continue;
			}
			HomeUnitPointKey homeUnitPointKey = unitBinding.BuildUnitPointKey(pointDefinition.PointId);
			if (!homeUnitPointKey.IsValid())
			{

				Debug.LogError("Official HomeBindingCacheBuilder.RegisterUnitPointsInNavigationGraph");
			}
			else
			{
				int count = navigationGraph.Nodes.Count;
				navigationGraph.Nodes.Add(new HomeRuntimeSpaceNavigationNode
				{
					NodeIndex = count,
					NodeKind = EHomeSpaceRouteEndpointKind.UnitPoint,
					SpaceId = space.SpaceId,
					WorldPosition = pointDefinition.WorldPosition,
					AutoConnectToRoute = pointDefinition.IsRouteNode,
					UnitInstanceId = unitBinding.UnitInstanceId,
					PointId = pointDefinition.PointId,
					UnitPointKey = homeUnitPointKey,
					Sorting = pointDefinition.Sorting
				});
				navigationGraph.UnitPointIndexMap[homeUnitPointKey] = count;
				navigationGraph.Adjacency[count] = new List<int>();
			}
		}
	}

	private static void WarnUnitPointSpaceMissing(HomeUnitBindingData unitBinding)
	{
		foreach (HomePointDefinition pointDefinition in unitBinding.UnitDefinition.PointDefinitions)
		{
			if (!string.IsNullOrEmpty(pointDefinition.PointId))
			{

				Debug.LogWarning("Official HomeBindingCacheBuilder.WarnUnitPointSpaceMissing");
			}
		}
	}

	private static void WarnUnitPointSpaceNotFound(HomeUnitBindingData unitBinding, string effectiveSpaceId)
	{
		foreach (HomePointDefinition pointDefinition in unitBinding.UnitDefinition.PointDefinitions)
		{
			if (!string.IsNullOrEmpty(pointDefinition.PointId))
			{

				Debug.LogWarning("Official HomeBindingCacheBuilder.WarnUnitPointSpaceNotFound");
			}
		}
	}

	private static void BuildNavigationGraphEdges(HomeSpaceBindingData space)
	{
		if (space.RuntimeRoute == null)
		{
			return;
		}
		HomeRuntimeSpaceNavigationGraph navigationGraph = space.RuntimeRoute.NavigationGraph;
		navigationGraph.Edges.Clear();
		foreach (HomeRuntimeSpaceNavigationNode node in navigationGraph.Nodes)
		{
			if (!navigationGraph.Adjacency.TryGetValue(node.NodeIndex, out var value))
			{
				value = new List<int>();
				navigationGraph.Adjacency[node.NodeIndex] = value;
			}
			else
			{
				value.Clear();
			}
		}
		for (int i = 0; i < space.SpaceRoute.Edges.Count; i++)
		{
			HomeSpaceRouteEdgeDefinition homeSpaceRouteEdgeDefinition = space.SpaceRoute.Edges[i];

			int nodeIndex;
			string debugText;
			int nodeIndex2;
			string debugText2;
			if (homeSpaceRouteEdgeDefinition.Cost <= 0f)
			{

				Debug.LogError("Official HomeBindingCacheBuilder.BuildNavigationGraphEdges");
			}
			else if (!TryResolveNavigationGraphEndpoint(navigationGraph, homeSpaceRouteEdgeDefinition.EndpointA, out nodeIndex, out debugText))
			{

				Debug.LogError("Official HomeBindingCacheBuilder.BuildNavigationGraphEdges");
			}
			else if (!TryResolveNavigationGraphEndpoint(navigationGraph, homeSpaceRouteEdgeDefinition.EndpointB, out nodeIndex2, out debugText2))
			{

				Debug.LogError("Official HomeBindingCacheBuilder.BuildNavigationGraphEdges");
			}
			else
			{
				AddRuntimeNavigationGraphEdge(navigationGraph, nodeIndex, nodeIndex2, homeSpaceRouteEdgeDefinition.Cost, i);
				AddRuntimeNavigationGraphEdge(navigationGraph, nodeIndex2, nodeIndex, homeSpaceRouteEdgeDefinition.Cost, i);
			}
		}
		BuildAutoConnectUnitPointEdges(space);
	}

	private static void BuildAutoConnectUnitPointEdges(HomeSpaceBindingData space)
	{
		HomeRuntimeSpaceNavigationGraph navigationGraph = space.RuntimeRoute.NavigationGraph;
		HomeNavigationAutoConnectPlan homeNavigationAutoConnectPlan = HomeNavigationAutoConnectPlanner.Build(BuildAutoConnectNodes(navigationGraph), BuildAutoConnectEdges(navigationGraph));
		foreach (HomeNavigationAutoConnectResult result in homeNavigationAutoConnectPlan.Results)
		{
			if (result.TargetKind == EHomeNavigationAutoConnectTargetKind.UnitPoint)
			{
				AddGeneratedBidirectionalEdge(navigationGraph, result.FromNodeIndex, result.UnitPointNodeIndex, result.UnitPointCost);
				continue;
			}
			AddGeneratedBidirectionalEdge(navigationGraph, result.FromNodeIndex, result.RouteEdgeFromNodeIndex, result.RouteEdgeFromCost);
			AddGeneratedBidirectionalEdge(navigationGraph, result.FromNodeIndex, result.RouteEdgeToNodeIndex, result.RouteEdgeToCost);
		}
		foreach (int unconnectedNodeIndex in homeNavigationAutoConnectPlan.UnconnectedNodeIndexes)
		{
			if (TryGetGraphNode(navigationGraph, unconnectedNodeIndex, out var node))
			{

				Debug.LogWarning("Official HomeBindingCacheBuilder.BuildAutoConnectUnitPointEdges");
			}
		}
	}

	private static List<HomeNavigationAutoConnectNode> BuildAutoConnectNodes(HomeRuntimeSpaceNavigationGraph graph)
	{
		List<HomeNavigationAutoConnectNode> list = new List<HomeNavigationAutoConnectNode>();
		foreach (HomeRuntimeSpaceNavigationNode node in graph.Nodes)
		{
			list.Add(new HomeNavigationAutoConnectNode(node.NodeIndex, node.NodeKind, node.WorldPosition, node.AutoConnectToRoute, HasAnyRuntimeEdge(graph, node.NodeIndex)));
		}
		return list;
	}

	private static List<HomeNavigationAutoConnectEdge> BuildAutoConnectEdges(HomeRuntimeSpaceNavigationGraph graph)
	{
		List<HomeNavigationAutoConnectEdge> list = new List<HomeNavigationAutoConnectEdge>();
		foreach (HomeRuntimeSpaceNavigationEdge edge in graph.Edges)
		{
			if (TryGetGraphNode(graph, edge.FromNodeIndex, out var node) && TryGetGraphNode(graph, edge.ToNodeIndex, out var node2))
			{
				bool isRouteNodeToRouteNode = node.NodeKind == EHomeSpaceRouteEndpointKind.RouteNode && node2.NodeKind == EHomeSpaceRouteEndpointKind.RouteNode;
				list.Add(new HomeNavigationAutoConnectEdge(edge.FromNodeIndex, edge.ToNodeIndex, edge.Cost, isRouteNodeToRouteNode));
			}
		}
		return list;
	}

	private static bool HasAnyRuntimeEdge(HomeRuntimeSpaceNavigationGraph graph, int nodeIndex)
	{
		if (graph.Adjacency.TryGetValue(nodeIndex, out var value))
		{
			return value.Count > 0;
		}
		return false;
	}

	private static bool TryGetGraphNode(HomeRuntimeSpaceNavigationGraph graph, int nodeIndex, out HomeRuntimeSpaceNavigationNode node)
	{
		if (nodeIndex >= 0 && nodeIndex < graph.Nodes.Count)
		{
			node = graph.Nodes[nodeIndex];
			return true;
		}
		node = null;
		return false;
	}

	private static void AddGeneratedBidirectionalEdge(HomeRuntimeSpaceNavigationGraph graph, int fromNodeIndex, int toNodeIndex, float cost)
	{
		float cost2 = Mathf.Max(cost, 0.001f);
		AddRuntimeNavigationGraphEdge(graph, fromNodeIndex, toNodeIndex, cost2, -1);
		AddRuntimeNavigationGraphEdge(graph, toNodeIndex, fromNodeIndex, cost2, -1);
	}

	private static void AddRuntimeNavigationGraphEdge(HomeRuntimeSpaceNavigationGraph graph, int fromNodeIndex, int toNodeIndex, float cost, int sourceDefinitionIndex)
	{
		int count = graph.Edges.Count;
		graph.Edges.Add(new HomeRuntimeSpaceNavigationEdge
		{
			EdgeIndex = count,
			FromNodeIndex = fromNodeIndex,
			ToNodeIndex = toNodeIndex,
			Cost = cost,
			SourceDefinitionIndex = sourceDefinitionIndex
		});
		if (!graph.Adjacency.TryGetValue(fromNodeIndex, out var value))
		{
			value = new List<int>();
			graph.Adjacency[fromNodeIndex] = value;
		}
		value.Add(count);
	}

	private static bool TryResolveNavigationGraphEndpoint(HomeRuntimeSpaceNavigationGraph graph, HomeSpaceRouteEndpointDefinition endpoint, out int nodeIndex, out string debugText)
	{
		nodeIndex = -1;
		debugText = BuildNavigationGraphEndpointDebugText(endpoint);
		if (endpoint == null)
		{
			return false;
		}
		if (endpoint.TryBuildRouteNodeKey(out var routeNodeId))
		{
			return graph.RouteNodeIndexMap.TryGetValue(routeNodeId, out nodeIndex);
		}
		if (endpoint.TryBuildUnitPointKey(out var unitPointKey))
		{
			return graph.UnitPointIndexMap.TryGetValue(unitPointKey, out nodeIndex);
		}
		return false;
	}

	private static string BuildNavigationGraphEndpointDebugText(HomeSpaceRouteEndpointDefinition endpoint)
	{
		if (endpoint == null)
		{
			return "null";
		}
		return endpoint.EndpointKind switch
		{
			EHomeSpaceRouteEndpointKind.RouteNode => "RouteNode('" + endpoint.RouteNodeId + "')",
			EHomeSpaceRouteEndpointKind.UnitPoint => "UnitPoint('" + endpoint.UnitInstanceId + "/" + endpoint.PointId + "')",
			_ => $"UnknownKind({endpoint.EndpointKind})",
		};
	}

	private void BuildChannelCaches(HomeBindingAuthoring authoring, HomeRuntimeBindingSnapshot snapshot)
	{
		foreach (HomeChannelBindingData channel in authoring.Channels)
		{
			if (channel.BuildRuntimeCache(((Component)authoring).transform, snapshot))
			{
				snapshot.Channels.Add(channel);
				FillChannelLinkRouteNodeIndex(snapshot, channel);
			}
		}
	}

	private static void FillChannelLinkRouteNodeIndex(HomeRuntimeBindingSnapshot snapshot, HomeChannelBindingData channel)
	{
		if (channel.RuntimeAToB != null)
		{
			FillLinkRouteNodeIndex(snapshot, channel.RuntimeAToB, channel.SideA, channel.SideB);
		}
		if (channel.RuntimeBToA != null)
		{
			FillLinkRouteNodeIndex(snapshot, channel.RuntimeBToA, channel.SideB, channel.SideA);
		}
	}

	private static void FillLinkRouteNodeIndex(HomeRuntimeBindingSnapshot snapshot, HomeRuntimeChannelLink link, HomeChannelEndpointDefinition fromEndpoint, HomeChannelEndpointDefinition toEndpoint)
	{
		HomeSpaceBindingData homeSpaceBindingData = snapshot.FindSpace(fromEndpoint.SpaceId);

		if (homeSpaceBindingData == null || homeSpaceBindingData.RuntimeRoute == null)
		{

			Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
			return;
		}
		if (homeSpaceBindingData.RuntimeRoute.BaseRouteNodeIndexMap.TryGetValue(fromEndpoint.EntryRouteNodeId, out var value))
		{
			link.FromRouteNodeIndex = value;
			if (homeSpaceBindingData.RuntimeRoute.NavigationGraph.RouteNodeIndexMap.TryGetValue(fromEndpoint.EntryRouteNodeId, out var value2))
			{
				link.FromGraphNodeIndex = value2;
			}
			else
			{

				Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
			}
		}
		else
		{

			Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
		}
		HomeSpaceBindingData homeSpaceBindingData2 = snapshot.FindSpace(toEndpoint.SpaceId);
		int value3;
		if (homeSpaceBindingData2 == null || homeSpaceBindingData2.RuntimeRoute == null)
		{

			Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
		}
		else if (homeSpaceBindingData2.RuntimeRoute.BaseRouteNodeIndexMap.TryGetValue(toEndpoint.ExitRouteNodeId, out value3))
		{
			link.ToRouteNodeIndex = value3;
			link.ExitRouteNodeWorldPosition = homeSpaceBindingData2.RuntimeRoute.Nodes[value3].WorldPosition;
			if (homeSpaceBindingData2.RuntimeRoute.NavigationGraph.RouteNodeIndexMap.TryGetValue(toEndpoint.ExitRouteNodeId, out var value4))
			{
				link.ToGraphNodeIndex = value4;
				return;
			}

			Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
		}
		else
		{

			Debug.LogWarning("Official HomeBindingCacheBuilder.FillLinkRouteNodeIndex");
		}
	}

	private static void BuildSemanticLocationIndexes(HomeRuntimeBindingSnapshot snapshot)
	{
		snapshot.AnchorToRouteNodeIndex.Clear();
		snapshot.AnchorToUnitPointIndex.Clear();
		snapshot.AnchorToChannelAnchorIndex.Clear();

		foreach (HomeSpaceBindingData space in snapshot.Spaces)
		{
			HomeRuntimeSpaceRoute runtimeRoute = space.RuntimeRoute;
			if (runtimeRoute == null)
			{
				continue;
			}
			foreach (HomeRuntimeSpaceRouteNode node in runtimeRoute.Nodes)
			{
				if (string.IsNullOrEmpty(node.RouteNodeId))
				{
					continue;
				}
				HomeResolvedLocation value = new HomeResolvedLocation(EHomeResolvedLocationKind.RouteNode, node.SpaceId, string.Empty, string.Empty, node.RouteNodeId, node.WorldPosition, node.Sorting);
				if (snapshot.AnchorToRouteNodeIndex.TryGetValue(node.RouteNodeId, out var value2))
				{

					Debug.LogError("Official HomeBindingCacheBuilder.BuildSemanticLocationIndexes");
				}
				else
				{
					snapshot.AnchorToRouteNodeIndex[node.RouteNodeId] = value;
				}
			}
		}
		foreach (HomeUnitBindingData unitBinding in snapshot.UnitBindings)
		{
			string unitEffectiveSpaceId = snapshot.GetUnitEffectiveSpaceId(unitBinding.UnitInstanceId);
			string unitInstanceId = unitBinding.UnitInstanceId;
			foreach (HomePointDefinition pointDefinition in unitBinding.UnitDefinition.PointDefinitions)
			{
				if (string.IsNullOrEmpty(pointDefinition.PointId))
				{
					continue;
				}
				HomeResolvedLocation value3 = new HomeResolvedLocation(EHomeResolvedLocationKind.UnitPoint, unitEffectiveSpaceId, unitInstanceId, string.Empty, pointDefinition.PointId, pointDefinition.WorldPosition, pointDefinition.Sorting);
				if (snapshot.AnchorToUnitPointIndex.TryGetValue(pointDefinition.PointId, out var value4))
				{

					Debug.LogError("Official HomeBindingCacheBuilder.BuildSemanticLocationIndexes");
				}
				else
				{
					snapshot.AnchorToUnitPointIndex[pointDefinition.PointId] = value3;
				}
			}
		}
		foreach (HomeChannelBindingData channel in snapshot.Channels)
		{
			string channelId = channel.ChannelId;
			foreach (HomeChannelAnchorBinding anchorBinding in channel.AnchorBindings)
			{
				if (string.IsNullOrEmpty(anchorBinding.AnchorId))
				{
					continue;
				}
				HomeResolvedLocation value5 = new HomeResolvedLocation(EHomeResolvedLocationKind.ChannelAnchor, string.Empty, string.Empty, channelId, anchorBinding.AnchorId, anchorBinding.WorldPosition, anchorBinding.Sorting);
				if (snapshot.AnchorToChannelAnchorIndex.TryGetValue(anchorBinding.AnchorId, out var value6))
				{

					Debug.LogError("Official HomeBindingCacheBuilder.BuildSemanticLocationIndexes");
				}
				else
				{
					snapshot.AnchorToChannelAnchorIndex[anchorBinding.AnchorId] = value5;
				}
			}
		}
	}
}
}
