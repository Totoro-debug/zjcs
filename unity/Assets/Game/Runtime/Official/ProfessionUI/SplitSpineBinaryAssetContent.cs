namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;

[Serializable]
public class SplitSpineBinaryAssetContent
{
	[SerializeField]
	public string ContentName;

	[SerializeField]
	public AssetGuidProxy<TextAsset> TextAsset;

	public SplitSpineBinaryAssetContent(string contentName, AssetGuidProxy<TextAsset> textAsset)
	{
		ContentName = contentName;
		TextAsset = textAsset;
	}
}

}
