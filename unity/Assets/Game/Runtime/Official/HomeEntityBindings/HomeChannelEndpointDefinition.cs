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
public class HomeChannelEndpointDefinition
{
	[HomeReferenceSelector(HomeReferenceKind.SpaceId, true, false)]
	public string SpaceId;

	[HomeReferenceSelector(HomeReferenceKind.RouteNodeId, true, false)]
	[FormerlySerializedAs("EntryAnchorId")]
	public string EntryRouteNodeId;

	[HomeReferenceSelector(HomeReferenceKind.RouteNodeId, true, false)]
	[FormerlySerializedAs("ExitAnchorId")]
	public string ExitRouteNodeId;
}
}
