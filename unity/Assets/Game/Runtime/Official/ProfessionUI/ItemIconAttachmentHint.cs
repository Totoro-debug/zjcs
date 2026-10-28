namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public class ItemIconAttachmentHint : ItemIconAttachmentCell
{
	[SerializeField]
	private GameObject HintObj;

	[SerializeField]
	private MultiLanguageText Text;

	public void SetCenterHintObjActive(bool active)
	{
		if ((Object)(object)HintObj != (Object)null)
		{
			HintObj.SetActive(active);
		}
	}

	public void SetKey(string key)
	{
		if ((Object)(object)Text != (Object)null)
		{
			Text.SetKey(key);
		}
	}
}

}
