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
public sealed class HomeRuntimeChannelLink
{
	public string ChannelId;

	public string FromSpaceId;

	public string ToSpaceId;

	public int FromRouteNodeIndex;

	public int FromGraphNodeIndex;

	public int ToRouteNodeIndex;

	public int ToGraphNodeIndex;

	public string ExitRouteNodeId;

	public Vector3 ExitRouteNodeWorldPosition;

	public HomeSortingDefinition EntrySorting;

	public HomeSortingDefinition ExitSorting;

	[JsonIgnore]
	public AssetGuidProxy<ScriptableObject> TraversalActionGraphProxy;

	public bool IsAToBDirection;

	[JsonIgnore]
	public IReadOnlyList<HomeChannelTraversalActionOverride> TraversalActionOverrides;

	public AssetGuidProxy<ScriptableObject> ResolveTraversalActionGraphProxy(HomeCharacterType actorType)
	{
		if (TraversalActionOverrides != null)
		{
			foreach (HomeChannelTraversalActionOverride traversalActionOverride in TraversalActionOverrides)
			{
				if (traversalActionOverride.ActorType == actorType)
				{
					AssetGuidProxy<ScriptableObject> assetGuidProxy = (IsAToBDirection ? traversalActionOverride.TraversalActionGraphAToB : traversalActionOverride.TraversalActionGraphBToA);
					if (assetGuidProxy != null && assetGuidProxy.IsValid)
					{
						return assetGuidProxy;
					}
					break;
				}
			}
		}
		return TraversalActionGraphProxy;
	}
}
}
