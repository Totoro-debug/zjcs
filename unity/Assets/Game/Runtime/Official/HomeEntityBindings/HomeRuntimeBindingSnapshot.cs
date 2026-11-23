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
public sealed class HomeRuntimeBindingSnapshot
{
	public readonly Dictionary<string, HomeResolvedLocation> AnchorToChannelAnchorIndex = new Dictionary<string, HomeResolvedLocation>();

	public readonly Dictionary<string, HomeResolvedLocation> AnchorToRouteNodeIndex = new Dictionary<string, HomeResolvedLocation>();

	public readonly Dictionary<string, HomeResolvedLocation> AnchorToUnitPointIndex = new Dictionary<string, HomeResolvedLocation>();

	public readonly List<HomeChannelBindingData> Channels = new List<HomeChannelBindingData>();

	public readonly List<HomeSpaceBindingData> Spaces = new List<HomeSpaceBindingData>();

	public readonly List<HomeUnitBindingData> UnitBindings = new List<HomeUnitBindingData>();

	public readonly Dictionary<string, string> UnitEffectiveSpaceIdIndex = new Dictionary<string, string>();

	public string GetUnitEffectiveSpaceId(string unitInstanceId)
	{
		return UnitEffectiveSpaceIdIndex[unitInstanceId];
	}
}
}
