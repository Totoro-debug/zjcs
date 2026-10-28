namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemIconAttachmentConfig : ScriptableObject
{
	[Serializable]
	public class ItemIconAttachmentProxy
	{
		public ItemIconAttachmentType Type;

		public AssetGuidProxy<GameObject> GuidProxy;
	}

	[Tooltip("拖动列表中的项改变顺序，越前面的项对应的attach显示越下面")]
	public List<ItemIconAttachmentProxy> Templates;
}

}
