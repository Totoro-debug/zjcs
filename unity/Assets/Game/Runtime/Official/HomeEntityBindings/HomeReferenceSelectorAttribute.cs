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
public sealed class HomeReferenceSelectorAttribute : PropertyAttribute
{
	public HomeReferenceKind Kind { get; }

	public bool AllowEmpty { get; }

	public bool AllowCustom { get; }

	public HomeReferenceSelectorAttribute(HomeReferenceKind kind, bool allowEmpty = true, bool allowCustom = false)
	{
		Kind = kind;
		AllowEmpty = allowEmpty;
		AllowCustom = allowCustom;
	}
}
}
