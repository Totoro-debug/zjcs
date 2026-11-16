// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;
using UnityEngine.Events;
using UnityEngine.UI;

public class OptionTalkDialogItem : OptionBaseItem
{
	public Button Btn;

	public OptionTalkItemNormalText NormalText;

	public OptionTalkItemRemainTimeText RemainTimeText;

	public Image TypeImage;

	public OptionTalkItemDot Dot;

	public Color mNormalColor = Color.white;

	public Color mDarkColor = Color.black;

	private DialogOptionDataBase mData;

	private int mIndex;

	private Action<int> mOnClicked;

	private IStoryOptionSpriteLease mSpriteLease;
	public IStoryOptionServices Services { get; set; }

	public override void Init(DialogOptionDataBase data, int index, Action<int> onClicked)
	{
		InitWrapper(() =>
		{
			mData = data;
			mIndex = index;
			mOnClicked = onClicked;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		((UnityEvent)Btn.onClick).AddListener((UnityAction)OnBtnClicked);
		RefreshView();
	}

	protected override void OnDisableAndInited()
	{
		if ((Object)(object)Btn != (Object)null)
		{
			((UnityEvent)Btn.onClick).RemoveListener((UnityAction)OnBtnClicked);
		}
		mSpriteLease?.Dispose();
		mSpriteLease = null;
		base.OnDisableAndInited();
	}

	private void OnBtnClicked()
	{
		mOnClicked?.Invoke(mIndex);
	}

	private void RefreshView()
	{
		if (NormalText != null) NormalText.Services = Services;
		if (RemainTimeText != null) RemainTimeText.Services = Services;
		OptionTalkItemBaseText optionTalkItemTextCtrl = GetOptionTalkItemTextCtrl(mData);
		if ((Object)null != (Object)(object)optionTalkItemTextCtrl)
		{
			optionTalkItemTextCtrl.RefreshText(mData);
		}
		SwitchTalkItemTextCtrlVisible(optionTalkItemTextCtrl);
		string iconPath = mData.GetIconPath();
		bool flag = (Object)(object)TypeImage != (Object)null && !string.IsNullOrEmpty(iconPath);
		if (flag)
		{
			TypeImage.sprite = (mSpriteLease = Services.LoadSprite(iconPath)).Result;
			((Graphic)TypeImage).color = (mData.IsBlackIcon ? mDarkColor : mNormalColor);
		}
		((Component)TypeImage).gameObject.SetActive(flag);
		Dot.SetContext(mData.DotContext);
	}

	private OptionTalkItemBaseText GetOptionTalkItemTextCtrl(DialogOptionDataBase data)
	{
		EOptionTalkItemTextPresentType textPresentType = data.TextPresentType;
		if (textPresentType != EOptionTalkItemTextPresentType.NormalText && textPresentType == EOptionTalkItemTextPresentType.RemainTimeText)
		{
			return RemainTimeText;
		}
		return NormalText;
	}

	private void SwitchTalkItemTextCtrlVisible(OptionTalkItemBaseText currentItemText)
	{
		if ((Object)null != (Object)(object)NormalText)
		{
			((Component)NormalText).gameObject.SetActive((Object)(object)NormalText == (Object)(object)currentItemText);
		}
		if ((Object)null != (Object)(object)RemainTimeText)
		{
			((Component)RemainTimeText).gameObject.SetActive((Object)(object)RemainTimeText == (Object)(object)currentItemText);
		}
	}

	public OptionTalkDialogItem()
	{
	}
}

}
