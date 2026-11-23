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
public sealed class HomeNavigationAutoConnectPlan
{
	public readonly List<HomeNavigationAutoConnectResult> Results = new List<HomeNavigationAutoConnectResult>();

	public readonly List<int> UnconnectedNodeIndexes = new List<int>();
}
}
