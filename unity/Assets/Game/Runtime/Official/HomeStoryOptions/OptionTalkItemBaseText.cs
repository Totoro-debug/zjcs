// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public class OptionTalkItemBaseText : MonoBehaviour
{
	public IStoryOptionServices Services { get; set; }
	[SerializeField]
	private WorldMultiLanguageText NameText;

	[SerializeField]
	private WorldMultiLanguageText TipText;

	public virtual void RefreshText(DialogOptionDataBase data)
	{
		if (data != null)
		{
			NameText.Localizer = Services.Localize;
			TipText.Localizer = Services.Localize;
			NameText.FontProvider = Services.Font;
			TipText.FontProvider = Services.Font;
			NameText.SetKey(data.TextWrap.Key, data.TextWrap.Args);
			bool flag = Services.ContainsText(data.TipWrap.Key);
			((Component)TipText).gameObject.SetActive(flag);
			if (flag)
			{
				TipText.SetKey(data.TipWrap.Key);
			}
		}
	}
}

}
