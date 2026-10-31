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

public abstract class BaseSwitchGroup : MonoBehaviour
{
	public Transform ItemContainer;

	public TabGroup ToggleGroup;

	public Action<object> OnOptionChanged;

	public abstract CommonSwitchType SwitchGroupType { get; }

	public virtual void Reset(bool needNotify = false)
	{
		ToggleGroup.ResetTabItem(needNotify);
	}
}
public abstract class BaseSwitchGroup<T> : BaseSwitchGroup where T : Enum
{
	protected void SetupAllItems()
	{
		TabItem[] allTabs = ToggleGroup.GetAllTabs();
		foreach (TabItem tabItem in allTabs)
		{
			T type = ((Component)tabItem).GetComponent<BaseSwitchGroupItem<T>>().SwitchType;
			((UnityEventBase)tabItem.OnValueChanged).RemoveAllListeners();
			((UnityEvent<bool>)tabItem.OnValueChanged).AddListener((UnityAction<bool>)((bool isOn) =>
			{
				OnOptionChanged?.Invoke(type);
			}));
		}
	}

	public void SetupDefaultItem(T defaultSelectedType)
	{
		TabItem[] allTabs = ToggleGroup.GetAllTabs();
		foreach (TabItem obj in allTabs)
		{
			T switchType = ((Component)obj).GetComponent<BaseSwitchGroupItem<T>>().SwitchType;
			obj.SetIsOnWithoutNotify(switchType.Equals(defaultSelectedType));
		}
	}
}

}
