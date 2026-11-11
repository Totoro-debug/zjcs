// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;

public class FunctionEntryBannerDotItem : MonoBehaviour
{
	[SerializeField]
	private WorldStateSwitcher<bool> mSelectedStates;

	public void SetSelected(bool isSelect)
	{
		mSelectedStates.SetState(isSelect);
	}
}
}
