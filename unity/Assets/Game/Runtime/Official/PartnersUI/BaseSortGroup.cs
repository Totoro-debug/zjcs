// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class BaseSortGroup : MonoBehaviour
{
	public TabGroup ToggleGroup;

	public Action<object, bool, bool> OnOptionChanged;

	public virtual void ResetTabItem(bool needNotify = false)
	{
		ToggleGroup.ResetTabItem(needNotify);
	}
}
public abstract class BaseSortGroup<T> : BaseSortGroup where T : Enum
{
	protected virtual void Awake()
	{
		TabItem[] allTabs = ToggleGroup.GetAllTabs();
		foreach (TabItem tabItem in allTabs)
		{
			BaseSortGroupItem<T> sortItem = ((Component)tabItem).GetComponent<BaseSortGroupItem<T>>();
			T type = sortItem.SortType;
			((Component)tabItem).GetComponent<MultiTextsControler>().SetMultiTextKey(LanguageUtils.GetEnumKey(type));
			sortItem.SetReverseChangeNotifyFunc((bool isReverse) =>
			{
				OnOptionChanged?.Invoke(type, arg2: true, isReverse);
			});
			((UnityEventBase)tabItem.OnValueChanged).RemoveAllListeners();
			((UnityEvent<bool>)tabItem.OnValueChanged).AddListener((UnityAction<bool>)((bool isOn) =>
			{
				sortItem.SetIsReverse(value: false);
				OnOptionChanged?.Invoke(type, isOn, sortItem.IsReverse);
			}));
		}
	}
}

}
