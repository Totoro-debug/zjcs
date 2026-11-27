namespace Zjcs.Official.TreasureSlotsUI {
using UnityEngine;

public class ItemListTypeMultiText : MonoBehaviour
{
	public Zjcs.Official.WorldUI.WorldMultiLanguageText TypeText;

	public void SetKey(string textKey)
	{
		TypeText.SetKey(textKey);
	}

	public void Format(params object[] args)
	{
		TypeText.Format(args);
	}

	public void FormatKey(string textKey, params object[] args)
	{
		TypeText.SetKey(textKey, args);
	}
}

}
