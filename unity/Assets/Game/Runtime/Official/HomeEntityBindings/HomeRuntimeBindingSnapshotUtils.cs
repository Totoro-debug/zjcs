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
public static class HomeRuntimeBindingSnapshotUtils
{
	public static HomeSpaceBindingData FindSpace(this HomeRuntimeBindingSnapshot snapshot, string spaceId)
	{
		if (string.IsNullOrEmpty(spaceId))
		{
			return null;
		}
		foreach (HomeSpaceBindingData space in snapshot.Spaces)
		{
			if (string.Equals(space.SpaceId, spaceId, StringComparison.Ordinal))
			{
				return space;
			}
		}
		return null;
	}





	public static bool TryResolveChannelAnchorWorldPosition(this HomeRuntimeBindingSnapshot snapshot, string anchorId, out Vector3 worldPosition)
	{
		worldPosition = default;
		if (string.IsNullOrEmpty(anchorId))
		{
			return false;
		}
		foreach (HomeChannelBindingData channel in snapshot.Channels)
		{
			foreach (HomeChannelAnchorBinding anchorBinding in channel.AnchorBindings)
			{
				if (string.Equals(anchorBinding.AnchorId, anchorId, StringComparison.Ordinal))
				{
					worldPosition = anchorBinding.WorldPosition;
					return true;
				}
			}
		}
		return false;
	}

	public static bool TryResolvePointWorldPosition(this HomeRuntimeBindingSnapshot snapshot, string spaceId, string pointId, string expectedUnitId, out Vector3 worldPosition)
	{
		worldPosition = default;
		HomeSpaceBindingData homeSpaceBindingData = snapshot.FindSpace(spaceId);
		if (homeSpaceBindingData == null)
		{
			return false;
		}
		foreach (HomeUnitBindingData runtimeUnitBinding in homeSpaceBindingData.RuntimeUnitBindings)
		{
			if (!string.IsNullOrEmpty(expectedUnitId) && !string.Equals(runtimeUnitBinding.UnitInstanceId, expectedUnitId, StringComparison.Ordinal))
			{
				continue;
			}
			foreach (HomePointDefinition pointDefinition in runtimeUnitBinding.UnitDefinition.PointDefinitions)
			{
				if (string.Equals(pointDefinition.PointId, pointId, StringComparison.Ordinal))
				{
					worldPosition = pointDefinition.WorldPosition;
					return true;
				}
			}
		}
		return false;
	}

	public static HomeRuntimeSpaceRoute TryGetSpaceRoute(this HomeRuntimeBindingSnapshot snapshot, string spaceId)
	{
		return snapshot.FindSpace(spaceId)?.RuntimeRoute;
	}

	public static bool TryGetSpaceRouteNode(this HomeRuntimeBindingSnapshot snapshot, string spaceId, string routeNodeId, out HomeRuntimeSpaceRouteNode routeNode)
	{
		HomeSpaceBindingData homeSpaceBindingData = snapshot.FindSpace(spaceId);
		if (homeSpaceBindingData == null || !homeSpaceBindingData.RuntimeRoute.TryGetBaseRouteNode(routeNodeId, out routeNode))
		{

			Debug.LogError("Official HomeRuntimeBindingSnapshotUtils.TryGetSpaceRouteNode");
			routeNode = default;
			return false;
		}
		return true;
	}

	public static HomeRuntimeSpaceNavigationGraph TryGetSpaceNavigationGraph(this HomeRuntimeBindingSnapshot snapshot, string spaceId)
	{
		return snapshot.TryGetSpaceRoute(spaceId)?.NavigationGraph;
	}

	public static int TryFindRouteNavigationNodeIndex(this HomeRuntimeBindingSnapshot snapshot, string spaceId, string nodeId)
	{
		HomeRuntimeSpaceNavigationGraph homeRuntimeSpaceNavigationGraph = snapshot.TryGetSpaceNavigationGraph(spaceId);
		if (homeRuntimeSpaceNavigationGraph == null)
		{
			return -1;
		}
		return CollectionExtensions.GetValueOrDefault<string, int>((IReadOnlyDictionary<string, int>)homeRuntimeSpaceNavigationGraph.RouteNodeIndexMap, nodeId, -1);
	}

	public static int TryFindUnitPointNavigationNodeIndex(this HomeRuntimeBindingSnapshot snapshot, string spaceId, string unitId, string pointId)
	{
		HomeRuntimeSpaceNavigationGraph homeRuntimeSpaceNavigationGraph = snapshot.TryGetSpaceNavigationGraph(spaceId);
		if (homeRuntimeSpaceNavigationGraph == null)
		{
			return -1;
		}
		HomeUnitPointKey homeUnitPointKey = new HomeUnitPointKey(unitId, pointId);
		return CollectionExtensions.GetValueOrDefault<HomeUnitPointKey, int>((IReadOnlyDictionary<HomeUnitPointKey, int>)homeRuntimeSpaceNavigationGraph.UnitPointIndexMap, homeUnitPointKey, -1);
	}
}
}
