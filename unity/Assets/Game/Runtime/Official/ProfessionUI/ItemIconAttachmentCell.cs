namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

[ExecuteInEditMode]
public class ItemIconAttachmentCell : UIBase, IGameObjectPoolItem
{
	[Tooltip("layer用于attachment的显示顺序，值高显示在上面，值小显示在下面")]
	[SerializeField]
	private int mLayer;

	public int Layer => mLayer;

	public void OnGameObjectPoolClear()
	{
	}

	public void OnGameObjectPoolReset()
	{
	}
}

}
