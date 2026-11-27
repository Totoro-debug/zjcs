// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {

using UnityEngine;

public class ItemIconTreasureQualityEffect : MonoBehaviour
{
	[SerializeField]
	private WorldStateSwitcher<Quality> mQualityEffectStates;

	public void SetState(Quality quality)
	{
		mQualityEffectStates.SetState(quality);
	}
}

}
