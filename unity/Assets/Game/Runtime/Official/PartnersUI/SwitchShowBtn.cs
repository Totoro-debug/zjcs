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
using UnityEngine.Events;
using UnityEngine.UI;

public class SwitchShowBtn : MonoBehaviour
{
	public enum ButtonShowState
	{
		Normal,
		Selected
	}

	public Button Buton;

	public List<GameObject> SwitchShowList;

	public List<MultiLanguageText> MultiTextList;

	public ButtonShowState ShowState;

	protected Action mInnerOnClickAction;

	public bool IsSelected => ShowState == ButtonShowState.Selected;

	protected virtual void Awake()
	{
		((UnityEvent)Buton.onClick).AddListener((UnityAction)FireClickEvent);
		SwitchShow(ShowState);
	}

	public virtual void FireClickEvent()
	{
		SwitchShow((!IsSelected) ? ButtonShowState.Selected : ButtonShowState.Normal);
		mInnerOnClickAction?.Invoke();
	}

	public void SetBtnTextKey(string key)
	{
		for (int i = 0; i < MultiTextList.Count; i++)
		{
			MultiTextList[i].SetKey(key);
		}
	}

	public void SwitchShow(ButtonShowState state)
	{
		ShowState = state;
		for (int i = 0; i < SwitchShowList.Count; i++)
		{
			SwitchShowList[i].SetActive(i == (int)state);
		}
	}

	public void SwitchShowNormal()
	{
		SwitchShow(ButtonShowState.Normal);
	}

	public void SwitchShowSelected()
	{
		SwitchShow(ButtonShowState.Selected);
	}

	public void SetListener(Action action)
	{
		mInnerOnClickAction = action;
	}
}

}
