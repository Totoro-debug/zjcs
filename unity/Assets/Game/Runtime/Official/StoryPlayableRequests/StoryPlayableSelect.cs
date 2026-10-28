// Migrated original client request body; see playable requests receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.ProfessionUI;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableRequests
{
[Serializable]
public class StoryPlayableSelect
{
	public EStoryPlayableSelectType Type;

	public IStoryPrefabAssetProxy CustomAssetProxy;
	public string PreDefineKey;

	private IStoryPrefabAssetProxy mRegisterProxy;

	public GameObject Load(IStoryHandleProxy proxy)
	{
		if (Type == EStoryPlayableSelectType.Custom)
		{
			return CustomAssetProxy.Load();
		}
		if (Type == EStoryPlayableSelectType.PreDefine)
		{
			mRegisterProxy = proxy.GetStoryRegisterAssetProxy(PreDefineKey);
			if (mRegisterProxy != null)
			{
				return mRegisterProxy.Load();
			}
		}
		return null;
	}

	public void UnLoad()
	{
		if (Type != EStoryPlayableSelectType.None)
		{
			if (mRegisterProxy != null)
			{
				mRegisterProxy.Unload();
			}
			if (CustomAssetProxy != null)
			{
				CustomAssetProxy.Unload();
			}
		}
	}
}

}
