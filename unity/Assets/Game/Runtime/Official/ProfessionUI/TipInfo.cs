using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
public class TipInfo : MonoBehaviour
{
	[SerializeField]
	private WorldMultiLanguageText mInfoText;

	[SerializeField]
	private Image mImage;

	public void SetKey(string key)
	{
		SetKey(true, key);
	}

	public void SetKey(string key, params object[] args)
	{
		SetKey(showImage: true, key, args);
	}

	public void SetKey(bool showImage, string key, params object[] args)
	{
		if ((Object)(object)mImage != (Object)null)
		{
			((Component)mImage).gameObject.SetActive(showImage);
		}
		mInfoText.SetKey(key, args);
	}
}
}
