// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;

public class FunctionEntryBigBtnItem : MonoBehaviour
{
	[SerializeField]
	private WorldStateSwitcher<bool> mEmptyStates;

	public void SetEmpty(bool isEmpty)
	{
		mEmptyStates.SetState(isEmpty);
	}
}
}
