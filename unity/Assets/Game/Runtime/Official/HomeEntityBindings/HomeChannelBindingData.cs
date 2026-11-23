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
public class HomeChannelBindingData
{
	public string ChannelId;

	public HomeChannelEndpointDefinition SideA = new HomeChannelEndpointDefinition();

	public HomeChannelEndpointDefinition SideB = new HomeChannelEndpointDefinition();

	public AssetGuidProxy<ScriptableObject> TraversalActionGraphAToB;

	public AssetGuidProxy<ScriptableObject> TraversalActionGraphBToA;

	public List<HomeChannelTraversalActionOverride> TraversalActionOverrides = new List<HomeChannelTraversalActionOverride>();

	public List<HomeChannelAnchorBinding> AnchorBindings = new List<HomeChannelAnchorBinding>();

	[NonSerialized]
	public HomeRuntimeChannelLink RuntimeAToB;

	[NonSerialized]
	public HomeRuntimeChannelLink RuntimeBToA;

	public bool BuildRuntimeCache(Transform rootTransform, HomeRuntimeBindingSnapshot snapshot)
	{
		foreach (HomeChannelAnchorBinding anchorBinding in AnchorBindings)
		{
			if (!string.IsNullOrEmpty(anchorBinding.AnchorId))
			{
				anchorBinding.WorldPosition = rootTransform.TransformPoint(anchorBinding.LocalPosition);
			}
		}
		RuntimeAToB = BuildChannelLink(snapshot, ChannelId, SideA, SideB, TraversalActionGraphAToB, isAToBDirection: true);
		RuntimeBToA = BuildChannelLink(snapshot, ChannelId, SideB, SideA, TraversalActionGraphBToA, isAToBDirection: false);
		return true;
	}

	public bool TryResolveAnchorPose(Transform rootTransform, string anchorId, out Vector3 worldPosition)
	{
		worldPosition = default;
		if (string.IsNullOrEmpty(anchorId))
		{
			return false;
		}
		foreach (HomeChannelAnchorBinding anchorBinding in AnchorBindings)
		{
			if (string.Equals(anchorBinding.AnchorId, anchorId))
			{
				worldPosition = (((Object)(object)rootTransform != (Object)null) ? rootTransform.TransformPoint(anchorBinding.LocalPosition) : anchorBinding.LocalPosition);
				return true;
			}
		}
		return false;
	}

	private HomeRuntimeChannelLink BuildChannelLink(HomeRuntimeBindingSnapshot snapshot, string channelId, HomeChannelEndpointDefinition fromEndpoint, HomeChannelEndpointDefinition toEndpoint, AssetGuidProxy<ScriptableObject> traversalActionGraphProxy, bool isAToBDirection)
	{
		HomeSpaceBindingData homeSpaceBindingData = snapshot.FindSpace(fromEndpoint.SpaceId);
		HomeSpaceBindingData homeSpaceBindingData2 = snapshot.FindSpace(toEndpoint.SpaceId);
		if (homeSpaceBindingData == null || homeSpaceBindingData2 == null)
		{
			return null;
		}
		HomeRuntimeSpaceRouteNode routeNode;
		return new HomeRuntimeChannelLink
		{
			ChannelId = channelId,
			FromSpaceId = fromEndpoint.SpaceId,
			ToSpaceId = toEndpoint.SpaceId,
			FromRouteNodeIndex = -1,
			FromGraphNodeIndex = -1,
			ToRouteNodeIndex = -1,
			ToGraphNodeIndex = -1,
			ExitRouteNodeId = toEndpoint.ExitRouteNodeId,
			EntrySorting = (homeSpaceBindingData.RuntimeRoute.TryGetBaseRouteNode(fromEndpoint.EntryRouteNodeId, out routeNode) ? routeNode.Sorting : default(HomeSortingDefinition)),
			ExitSorting = (homeSpaceBindingData2.RuntimeRoute.TryGetBaseRouteNode(toEndpoint.ExitRouteNodeId, out routeNode) ? routeNode.Sorting : default(HomeSortingDefinition)),
			TraversalActionGraphProxy = traversalActionGraphProxy,
			TraversalActionOverrides = TraversalActionOverrides,
			IsAToBDirection = isAToBDirection
		};
	}
}
}
