// Migrated original skill-prefab role; see skill-prefab-runtime source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.BattleSkillPlayables
{
public class MyPlayableOverrider : MonoBehaviour, IFightGetUsePlayable
{
	private class CacheOverride
	{
		public string Tag;

		public GameObject OriginPlayable;
	}

	[SerializeField]
	private List<PlayableOverrideInfo> mPlayableOverrideInfos = new List<PlayableOverrideInfo>();

	private List<CacheOverride> mCacheOverrides = new List<CacheOverride>();

	private IPlyableOverridSetter mPlayableOverrideSetter;

	private void Awake()
	{
		mPlayableOverrideSetter = ((Component)this).GetComponent<IPlyableOverridSetter>();
		if (mPlayableOverrideSetter == null)
		{
			return;
		}
		mPlayableOverrideSetter.Prepare();
		foreach (PlayableOverrideInfo mPlayableOverrideInfo in mPlayableOverrideInfos)
		{
			mCacheOverrides.Add(new CacheOverride
			{
				Tag = mPlayableOverrideInfo.Tag,
				OriginPlayable = mPlayableOverrideSetter.GetOriginPlayable(mPlayableOverrideInfo.Tag)
			});
		}
	}

	private void OnEnable()
	{
		if (mPlayableOverrideSetter != null)
		{
			MyEntity lifecycleEntity = mPlayableOverrideSetter.GetLifecycleEntity();
			if (!((Object)(object)lifecycleEntity == (Object)null) && !((Object)(object)lifecycleEntity.myContainer == (Object)null))
			{
				TryDoOverride();
			}
		}
	}

	private void OnDisable()
	{
		if (mPlayableOverrideSetter == null)
		{
			return;
		}
		foreach (CacheOverride mCacheOverride in mCacheOverrides)
		{
			mPlayableOverrideSetter.OverridePlayable(mCacheOverride.Tag, mCacheOverride.OriginPlayable);
		}
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		foreach (PlayableOverrideInfo mPlayableOverrideInfo in mPlayableOverrideInfos)
		{
			foreach (GameObject playable in mPlayableOverrideInfo.Playables)
			{
				playables.Add(playable);
			}
		}
	}

	public void TryDoOverride()
	{
		if (mPlayableOverrideSetter == null)
		{
			return;
		}
		foreach (PlayableOverrideInfo mPlayableOverrideInfo in mPlayableOverrideInfos)
		{
			mPlayableOverrideSetter.DoOverride(mPlayableOverrideInfo);
		}
	}
}

}
