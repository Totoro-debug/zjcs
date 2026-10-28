namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;

public class ItemIconAttachment : MonoBehaviour
{
	private Dictionary<ItemIconAttachmentType, ItemIconAttachmentCell> mAttachments = new Dictionary<ItemIconAttachmentType, ItemIconAttachmentCell>();

	private IItemIconAttachmentManager mItemIconAttachmentManager { get; set; }

	public void Bind(IItemIconAttachmentManager manager) => mItemIconAttachmentManager = manager;

	public void SetShow(ItemIconAttachmentType type, bool show)
	{
		ItemIconAttachmentCell itemIconAttachmentCell = TryGetAttachment(type);
		if ((Object)(object)itemIconAttachmentCell != (Object)null)
		{
			((Component)itemIconAttachmentCell).gameObject.SetActive(show);
		}
	}

	public void HideAll()
	{
		foreach (KeyValuePair<ItemIconAttachmentType, ItemIconAttachmentCell> mAttachment in mAttachments)
		{
			mItemIconAttachmentManager.RecycleAttachment(mAttachment.Key, mAttachment.Value);
		}
		mAttachments.Clear();
	}

	public ItemIconAttachmentCell GetAttachment(ItemIconAttachmentType type, bool autActive = true)
	{
		return TryGetAttachment(type, autActive);
	}

	private ItemIconAttachmentCell TryGetAttachment(ItemIconAttachmentType type, bool autActive = true)
	{
		ItemIconAttachmentCell value = null;
		mAttachments.TryGetValue(type, out value);
		if ((Object)(object)value == (Object)null)
		{
			GameObject attachment = mItemIconAttachmentManager.GetAttachment(type, ((Component)this).transform, autActive);
			if ((Object)(object)attachment == (Object)null)
			{
				return null;
			}
			value = attachment.GetComponent<ItemIconAttachmentCell>();
			if ((Object)(object)value != (Object)null)
			{
				Sort(value);
				mAttachments.Add(type, value);
			}
			else
			{
				Debug.LogWarning("TryGetAttachment type: " + type + " attachment is null", this);
			}
		}
		return value;
	}

	private void Sort(ItemIconAttachmentCell cell)
	{
		foreach (KeyValuePair<ItemIconAttachmentType, ItemIconAttachmentCell> mAttachment in mAttachments)
		{
			if (cell.Layer < mAttachment.Value.Layer)
			{
				((Component)cell).transform.SetSiblingIndex(((Component)mAttachment.Value).transform.GetSiblingIndex());
				break;
			}
		}
	}
}

}
