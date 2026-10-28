// Original client-source/ToggleText.cs; logging and local UI service bindings adapted.
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
public class ToggleText : MonoBehaviour
{
	[SerializeField]
	private Toggle mToggle;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldMultiLanguageText mMultiLanguageTexts;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldStateSwitcher<bool> mStateSwitcher;

	public void SetFormat(string argText)
	{
		mMultiLanguageTexts.Format(argText);
	}

	public void SetSelectFlag(bool isSelect)
	{
		mToggle.isOn = isSelect;
		SetSelectState(isSelect);
	}

	public void SetSelectState(bool isSelect)
	{
		mStateSwitcher.SetState(isSelect);
	}

	public bool IsOnToggleObj(GameObject obj)
	{
		if ((UnityEngine.Object)(object)obj == (Object)(object)((Component)mToggle).gameObject && mToggle.isOn)
		{
			return true;
		}
		return false;
	}
}
}
