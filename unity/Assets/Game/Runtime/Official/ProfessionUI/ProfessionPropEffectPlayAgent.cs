// Complete original common promotion control; explicit local material services.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.ProfessionUI {
using Object=UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class ProfessionPropEffectPlayAgent : ProfessionPropEffectPlayAgentBase
{
	[SerializeField]
	private List<ProfessionPlayerPropItem> mPropItems;

	protected override IEnumerable<ProfessionPlayerPropItem> GetPropItems()
	{
		return mPropItems;
	}
}

}
