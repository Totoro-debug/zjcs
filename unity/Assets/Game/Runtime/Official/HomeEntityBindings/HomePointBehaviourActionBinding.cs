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
public sealed class HomePointBehaviourActionBinding
{
	[HomeReferenceSelector(HomeReferenceKind.BehaviourId, true, false)]
	public string BehaviourId;

	public AssetGuidProxy<ScriptableObject> ActionGraphProxy;
}
}
