namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;

public class ItemIconConfig : ScriptableObject
{
	public List<Sprite> QualityBgImages;

	public Sprite GetQualityBgImage(Quality quality)
	{
		if (quality < Quality.None || (int)quality >= QualityBgImages.Count)
		{
			return null;
		}
		return QualityBgImages[(int)quality];
	}
}

}
