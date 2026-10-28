// Selected original client proxy methods; remaining services are outside this port.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.StoryPlayableRequests
{
public class StoryHandleProxy:IStoryHandleProxy
{
private IMyPlayableManager mPlayableManager;
public Func<string,IStoryPrefabAssetProxy> RegisterProxyLookup {get;set;}
public MyPlayable PlayPlayable(GameObject prefab, GameObject source, GameObject target, float speed = 1f, GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
	{
		if (mPlayableManager == null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
			return null;
		}
		return mPlayableManager.Play(prefab, source, target, speed, parentGo, param, needStopObj);
	}

public IStoryPrefabAssetProxy GetStoryRegisterAssetProxy(string registerKey)
	{
		return RegisterProxyLookup(registerKey);
	}

public void BindMyPlayableManager(ActivePlayableManagerInfo info)
	{
		mPlayableManager = info.MyPlayableManager;
	}

public void UnbindMyPlayableManager(InactivePlayableManagerInfo info)
	{
		if (mPlayableManager == info.MyPlayableManager)
		{
			mPlayableManager = null;
		}
	}
}
}
