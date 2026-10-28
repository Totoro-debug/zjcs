namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class SkinTextureAsset : ScriptableObject, ISerializationCallbackReceiver
{
	[SerializeField]
	private List<AssetGuidProxy<ScriptableObject>> mSkeletonDataAssets;

	[SerializeField]
	private List<SkinTextureConfig> mSkinTextureConfigList;

	private Dictionary<string, SkinTextureConfig> mSkinTextureConfigDict;

	public IReadOnlyList<AssetGuidProxy<ScriptableObject>> SkeletonDataAssets => mSkeletonDataAssets;

	public List<SkinTextureConfig> SkinTextureConfigList => mSkinTextureConfigList;

	public SkinTextureConfig GetConfig(string skinName)
	{
		if (mSkinTextureConfigDict.TryGetValue(skinName, out var value))
		{
			return value;
		}
		Debug.LogWarning("not exist the skin:" + skinName);
		return null;
	}

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		mSkinTextureConfigDict = new Dictionary<string, SkinTextureConfig>();
		foreach (SkinTextureConfig mSkinTextureConfig in mSkinTextureConfigList)
		{
			mSkinTextureConfigDict.Add(mSkinTextureConfig.SkinName, mSkinTextureConfig);
		}
	}
}

}
