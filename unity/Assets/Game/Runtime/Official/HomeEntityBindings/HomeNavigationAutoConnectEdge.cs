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
public readonly struct HomeNavigationAutoConnectEdge
{
	public HomeNavigationAutoConnectEdge(int fromNodeIndex, int toNodeIndex, float cost, bool isRouteNodeToRouteNode)
	{
		Cost = cost;
		FromNodeIndex = fromNodeIndex;
		IsRouteNodeToRouteNode = isRouteNodeToRouteNode;
		ToNodeIndex = toNodeIndex;
	}

	public readonly float Cost;

	public readonly int FromNodeIndex;

	public readonly bool IsRouteNodeToRouteNode;

	public readonly int ToNodeIndex;
}
}
