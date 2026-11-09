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
public class FightSkillPlayableOverrideByStatusStack : PlayableOverrideByStatusStack
{
	private const string kOriginPlayablePrefabTag = "myPlayablePrefab";

	private const string kOriginUpPlayablePrefabTag = "UpPlayablePrefab";

	private const string kOriginDownPlayablePrefabTag = "DownPlayablePrefab";

	private const string kOriginLeftPlayablePrefabTag = "LeftPlayablePrefab";

	private const string kOriginRightPlayablePrefabTag = "RightPlayablePrefab";

	[SerializeField]
	private MyFightSkill mFightSkill;

	public override void Prepare()
	{
		mPlayableOriginOverrides.Clear();
		mPlayableOriginOverrides.Add("myPlayablePrefab", mFightSkill.myPlayablePrefab);
		mPlayableOriginOverrides.Add("UpPlayablePrefab", mFightSkill.UpPlayablePrefab);
		mPlayableOriginOverrides.Add("DownPlayablePrefab", mFightSkill.DownPlayablePrefab);
		mPlayableOriginOverrides.Add("LeftPlayablePrefab", mFightSkill.LeftPlayablePrefab);
		mPlayableOriginOverrides.Add("RightPlayablePrefab", mFightSkill.RightPlayablePrefab);
	}

	public override MyEntity GetSource()
	{
		if ((Object)(object)mFightSkill == (Object)null || mFightSkill.Component == null)
		{
			return null;
		}
		return mFightSkill.Source;
	}

	public override MyEntity GetLifecycleEntity()
	{
		if (!((Object)(object)mFightSkill != (Object)null))
		{
			return null;
		}
		return mFightSkill.myEntity;
	}

	public override void OverridePlayable(string tag, GameObject playable)
	{
		switch (tag)
		{
		case "myPlayablePrefab":
			mFightSkill.myPlayablePrefab = playable;
			break;
		case "UpPlayablePrefab":
			mFightSkill.UpPlayablePrefab = playable;
			mFightSkill.UpdateDirectionPlyables();
			break;
		case "DownPlayablePrefab":
			mFightSkill.DownPlayablePrefab = playable;
			mFightSkill.UpdateDirectionPlyables();
			break;
		case "LeftPlayablePrefab":
			mFightSkill.LeftPlayablePrefab = playable;
			mFightSkill.UpdateDirectionPlyables();
			break;
		case "RightPlayablePrefab":
			mFightSkill.RightPlayablePrefab = playable;
			mFightSkill.UpdateDirectionPlyables();
			break;
		}
	}
}

}
