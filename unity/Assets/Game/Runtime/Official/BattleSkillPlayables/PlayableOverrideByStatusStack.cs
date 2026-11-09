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
public abstract class PlayableOverrideByStatusStack : MonoBehaviour, IPlyableOverridSetter
{
	public IFightPlayableOverrideStatusServices StatusServices { get; set; }

	[SerializeField]
	protected int mStatusId;

	protected int? mCurrentStack;

	protected Dictionary<string, GameObject> mPlayableOriginOverrides = new Dictionary<string, GameObject>();

	public abstract void Prepare();

	protected virtual void OnDisable()
	{
		mCurrentStack = null;
	}

	public GameObject GetOverridePlayable(PlayableOverrideInfo info)
	{
		List<GameObject> playables = info.Playables;
		if (!mCurrentStack.HasValue || playables.Count <= 0 || mCurrentStack.Value < 0)
		{
			return null;
		}
		if (mCurrentStack.Value >= playables.Count)
		{
			return playables.Last();
		}
		return playables[mCurrentStack.Value];
	}

	public GameObject GetOriginPlayable(string tag)
	{
		if (!mPlayableOriginOverrides.TryGetValue(tag, out var value))
		{
			return null;
		}
		return value;
	}

	public abstract MyEntity GetSource();

	public abstract MyEntity GetLifecycleEntity();

	public abstract void OverridePlayable(string tag, GameObject playable);

	public void DoOverride(PlayableOverrideInfo info)
	{
		if (info.Playables.Count <= 0)
		{
			return;
		}
		if (!mCurrentStack.HasValue)
		{
			MyEntity source = GetSource();
			if ((Object)(object)source == (Object)null)
			{
				return;
			}
			FightHitSourceStatusAgent component = StatusServices.GetSourceStatusAgent(source);
			if (component == null)
			{
				return;
			}
			foreach (KeyValuePair<EcEntityIdentifier, FightHitSourceStatus> item in component.ApplyStatusDict)
			{
				FightHitSourceStatus value = item.Value;
				if (value.Entity.ClassId == mStatusId)
				{
					mCurrentStack = value.StackedCount;
					break;
				}
			}
		}
		if (mCurrentStack.HasValue)
		{
			GameObject overridePlayable = GetOverridePlayable(info);
			if ((Object)(object)overridePlayable != (Object)null)
			{
				OverridePlayable(info.Tag, overridePlayable);
			}
		}
	}
}

}
