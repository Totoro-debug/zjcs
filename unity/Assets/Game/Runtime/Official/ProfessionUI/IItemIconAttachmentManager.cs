namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public interface IItemIconAttachmentManager
{
	GameObject GetAttachment(ItemIconAttachmentType type, Transform slot, bool autoActive);

	void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell attachment);
}

}
