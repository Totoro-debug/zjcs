// Migrated selected official client; see partner-gift-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using LanguageUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipNpcInfo;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemIconNpcGift : ItemIcon
{
	[SerializeField]
	private GameObject mPreferenceGo;

	[SerializeField]
	private GameObject mSelectedGo;

	private Button mReduceBtn;

	private Action<ItemIcon> mClickReduceBtnAction;

	private void OnClickReduceBtn()
	{
		mClickReduceBtnAction?.Invoke(this);
	}

	public override void Clear()
	{
		base.Clear();
		mPreferenceGo.SetActive(false);
		if ((Object)null != (Object)(object)mReduceBtn)
		{
			((UnityEvent)mReduceBtn.onClick).RemoveListener((UnityAction)OnClickReduceBtn);
			mReduceBtn = null;
		}
	}

	public override ItemIcon SetAttachment(ItemIconAttachmentType attachmentType, bool show)
	{
		base.SetAttachment(attachmentType, show);
		if (attachmentType == ItemIconAttachmentType.ReduceTip)
		{
			AttachReduceClickEvent();
		}
		return this;
	}

	public void SetReduceBtnClickListener(Action<ItemIcon> action)
	{
		mClickReduceBtnAction = action;
	}

	public void SetPreferenceGoActive(bool value)
	{
		mPreferenceGo.SetActive(value);
	}

	public void SetSelectedGoActive(bool value)
	{
		mSelectedGo.SetActive(value);
	}

	private void AttachReduceClickEvent()
	{
		if ((Object)(object)mReduceBtn != (Object)null)
		{
			return;
		}
		ItemIconAttachmentCell attachment = GetAttachment(ItemIconAttachmentType.ReduceTip);
		if ((Object)(object)attachment == (Object)null)
		{
			return;
		}
		mReduceBtn = ((Component)attachment).GetComponent<Button>();
		if ((Object)(object)mReduceBtn == (Object)null)
		{
			Debug.LogError("AttachReduceClickEvent mReduceBtn need Button component");
		}
		else
		{
			((UnityEvent)mReduceBtn.onClick).RemoveListener((UnityAction)OnClickReduceBtn);
			((UnityEvent)mReduceBtn.onClick).AddListener((UnityAction)OnClickReduceBtn);
		}
	}
}

}
