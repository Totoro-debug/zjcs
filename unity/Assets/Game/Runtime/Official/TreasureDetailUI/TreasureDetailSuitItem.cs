// Selected original suit UI; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using PropType = System.Int32;
using Object = UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

public class TreasureDetailSuitItem : MonoBehaviour
{
	[SerializeField]
	private List<WorldMultiLanguageText> mTitles;

	[SerializeField]
	private WorldStateSwitcher<bool> mActiveStateSwitcher;

	[SerializeField]
	private Button mButton;

	public Button Button => mButton;

	public void Show(int treasureClassId, bool actived)
	{
		foreach (WorldMultiLanguageText mTitle in mTitles)
		{
			mTitle.SetKey(TreasureSuitLanguage.GetItemNameKey(treasureClassId));
		}
		mActiveStateSwitcher.SetState(actived);
		((Component)this).gameObject.SetActive(true);
	}
}

}
