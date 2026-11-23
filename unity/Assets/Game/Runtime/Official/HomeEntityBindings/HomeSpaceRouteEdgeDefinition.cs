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
public sealed class HomeSpaceRouteEdgeDefinition
{
	public HomeSpaceRouteEndpointDefinition EndpointA = new HomeSpaceRouteEndpointDefinition();

	public HomeSpaceRouteEndpointDefinition EndpointB = new HomeSpaceRouteEndpointDefinition();

	public float Cost = 1f;
}
}
