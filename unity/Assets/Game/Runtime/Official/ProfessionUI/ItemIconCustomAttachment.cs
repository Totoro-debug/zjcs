namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemIconCustomAttachment : MonoBehaviour
{
	[Serializable]
	public class ItemIconAttachmentConfig
	{
		public ItemIconAttachmentType ItemIconAttachmentType;

		public ItemIconAttachmentCell AttachmentCell;
	}

	[SerializeField]
	private List<ItemIconAttachmentConfig> AttachmentConfig;

	public bool SetShow(ItemIconAttachmentType itemIconAttachmentType, bool show)
	{
		foreach (ItemIconAttachmentConfig item in AttachmentConfig)
		{
			if (item.ItemIconAttachmentType == itemIconAttachmentType)
			{
				((Component)item.AttachmentCell).gameObject.SetActive(show);
				return true;
			}
		}
		return false;
	}

	public void HideAll()
	{
		foreach (ItemIconAttachmentConfig item in AttachmentConfig)
		{
			((Component)item.AttachmentCell).gameObject.SetActive(false);
		}
	}

	public ItemIconAttachmentCell GetAttachment(ItemIconAttachmentType type)
	{
		foreach (ItemIconAttachmentConfig item in AttachmentConfig)
		{
			if (item.ItemIconAttachmentType == type)
			{
				return item.AttachmentCell;
			}
		}
		return null;
	}
}

}
