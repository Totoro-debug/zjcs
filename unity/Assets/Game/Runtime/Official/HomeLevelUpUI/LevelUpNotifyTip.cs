// Migrated selected official client; formula, resources and window service ports only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeLevelUpUI
{

public class LevelUpNotifyTip : UIBase
{
    public IHomeLevelUpServices Services { private get; set; }
	[Serializable]
	private class PropData
	{
		public PropType PropType;

		public Image PropImage;

		public PlayerPropItem PropItem;
	}

	public const float kLevelUpNotifyShowTime = 2.2f;

	[SerializeField]
	private MultiLanguageText mToLevelText;

	[SerializeField]
	private List<PropData> mPropData;

	private int mFromLevel;

	private int mToLevel;

	private HomeWindowResourceBag mDisposableBag;



	public void Init(int fromLevel, int toLevel)
	{
		InitWrapper(() =>
		{
			mFromLevel = fromLevel;
			mToLevel = toLevel;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new HomeWindowResourceBag();
		mToLevelText.SetKey("{0}", mToLevel);
		Dictionary<PropType, long> props = Services.CalcCharacterBattleProps(mFromLevel);
		Dictionary<PropType, long> props2 = Services.CalcCharacterBattleProps(mToLevel);
		foreach (PropData mPropDatum in mPropData)
		{
			long rawProp = Services.GetRawProp(props, mPropDatum.PropType);
			long rawProp2 = Services.GetRawProp(props2, mPropDatum.PropType);
			if (rawProp == rawProp2)
			{
				((Component)mPropDatum.PropItem).gameObject.SetActive(false);
				continue;
			}
			((Component)mPropDatum.PropItem).gameObject.SetActive(true);
			ProfessionPropertyDefinition propCfgInfo = Services.GetProperty(mPropDatum.PropType);
			mPropDatum.PropImage.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>("Assets/Image/" + propCfgInfo.SmallIcon)).Result;
			mPropDatum.PropItem.Init(mPropDatum.PropType, rawProp, rawProp2);
		}
		if (PopupWindow.State == PopupWindow.EState.Show)
		{
			OnShow(PopupWindow);
		}
		else
		{
			PopupWindow.OnShow += new Action<PopupWindow>(OnShow);
		}
	}

	protected override void OnDisableAndInited()
	{
		PopupWindow.OnShow -= new Action<PopupWindow>(OnShow);
		mDisposableBag.Dispose();
		base.OnDisableAndInited();
	}

	private void OnShow(PopupWindow popupWindow)
	{
		if (!((Object)(object)((Component)popupWindow).gameObject != (Object)(object)((Component)this).gameObject))
		{
			Services.Close(((Component)this).gameObject);
		}
	}

	public static bool TryPopup(IHomeLevelUpServices services, int startLevel)
	{
		int currentLevel = services.CurrentLevel;
		if (startLevel == currentLevel)
		{
			return false;
		}
		services.Popup((LevelUpNotifyTip go) =>
		{
			go.Init(startLevel, currentLevel);
		});
		return true;
	}
}

}
