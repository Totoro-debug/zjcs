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
[RequireComponent(typeof(HomeEntityDataProvider))]
public sealed class HomeUnitSpaceOverrideData : HomeEntityData
{
	public List<HomeUnitSpaceOverrideProfileData> Profiles = new List<HomeUnitSpaceOverrideProfileData>();
}
}
