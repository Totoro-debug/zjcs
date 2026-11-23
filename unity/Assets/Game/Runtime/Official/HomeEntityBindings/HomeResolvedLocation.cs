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
public readonly struct HomeResolvedLocation
{
	public HomeResolvedLocation(EHomeResolvedLocationKind kind, string spaceId, string unitId, string channelId, string locationId, Vector3 worldPosition, HomeSortingDefinition sorting)
	{
		Kind = kind;
		SpaceId = spaceId;
		UnitId = unitId;
		ChannelId = channelId;
		LocationId = locationId;
		WorldPosition = worldPosition;
		Sorting = sorting;
	}

	public readonly EHomeResolvedLocationKind Kind;

	public readonly string SpaceId;

	public readonly string UnitId;

	public readonly string ChannelId;

	public readonly string LocationId;

	public readonly Vector3 WorldPosition;

	public readonly HomeSortingDefinition Sorting;
}
}
