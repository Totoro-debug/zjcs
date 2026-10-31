// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseFilterGroup : MonoBehaviour
{
	public Transform ItemContainer;

	public HashSet<object> mDefaultSelectedOptions;

	protected List<SwitchShowBtn> mOptionBtns = new List<SwitchShowBtn>();

	public Action<object, bool> OnOptionChanged;

	public HashSet<object> DefaultSelectedOptions
	{
		get
		{
			if (mDefaultSelectedOptions == null)
			{
				mDefaultSelectedOptions = new HashSet<object>();
			}
			return mDefaultSelectedOptions;
		}
	}

	public abstract CommonFilterType FilterGroupType { get; }

	public abstract void ResetAllStates();

	public abstract void SelectedState(HashSet<object> selectedOptions);
}

}
