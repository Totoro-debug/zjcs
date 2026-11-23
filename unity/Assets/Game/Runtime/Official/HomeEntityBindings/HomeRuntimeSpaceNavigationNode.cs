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
public sealed class HomeRuntimeSpaceNavigationNode
{
	public int NodeIndex;

	public EHomeSpaceRouteEndpointKind NodeKind;

	public bool AutoConnectToRoute;

	public string PointId;

	public string RouteNodeId;

	public string SpaceId;

	public string UnitInstanceId;

	public HomeUnitPointKey UnitPointKey;

	public Vector3 WorldPosition;

	public HomeSortingDefinition Sorting;
}
}
