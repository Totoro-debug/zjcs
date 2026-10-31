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
using UnityEngine.UI;

public class BaseSortGroupItem<T> : MonoBehaviour where T : Enum
{
	public T SortType;

	public bool IsReverse;

	public Zjcs.Official.WorldUI.WorldStateSwitcher<bool> ReverseStateSwitch;

	public Button ReverseBtn;

	private Action<bool> mReverseChangeNotifyFunc;

	private void Awake()
	{
		if ((Object)(object)ReverseBtn != (Object)null)
		{
			((UnityEvent)ReverseBtn.onClick).AddListener((UnityAction)(() =>
			{
				SetIsReverseWithNotify(!IsReverse);
			}));
		}
	}

	public void SetIsReverseWithNotify(bool value)
	{
		IsReverse = value;
		ReverseStateSwitch.SetState(IsReverse);
		if (mReverseChangeNotifyFunc != null)
		{
			mReverseChangeNotifyFunc(IsReverse);
		}
	}

	public void SetIsReverse(bool value)
	{
		IsReverse = value;
		ReverseStateSwitch.SetState(IsReverse);
	}

	public void SetReverseChangeNotifyFunc(Action<bool> func)
	{
		mReverseChangeNotifyFunc = func;
	}
}

}
