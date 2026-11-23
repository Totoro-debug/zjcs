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
public sealed class HomeBindingAuthoring : HomeEntityData
{
	public EHomeActorBindingProfile Profile;

	public List<HomeUnitBindingData> Units = new List<HomeUnitBindingData>();

	public List<HomeSpaceBindingData> Spaces = new List<HomeSpaceBindingData>();

	public List<HomeChannelBindingData> Channels = new List<HomeChannelBindingData>();
}
}
