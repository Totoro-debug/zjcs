namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class BlessLevelTextViewChanger
{
	[SerializeField]
	private List<WorldMultiLanguageText> mBlessTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsBlessStateSwitcher;

	public void Refresh(bool isBlessing, bool needChangeOutline = true)
	{
		int count = mBlessTxt.Count;
		mIsBlessStateSwitcher.SetState(isBlessing);
		TMPEffect val = default;
		TextMeshProUGUI val2 = default;
		for (int i = 0; i < count; i++)
		{
			WorldMultiLanguageText multiLanguageText = mBlessTxt[i];
			if (((Component)multiLanguageText).TryGetComponent<TMPEffect>(out val))
			{
				if (needChangeOutline)
				{
					val.outline = isBlessing;
				}
				val.SetupEffect(true);
			}
			if (((Component)multiLanguageText).TryGetComponent<TextMeshProUGUI>(out val2))
			{
				((TMP_Text)val2).enableVertexGradient = isBlessing;
			}
		}
	}
}

}
