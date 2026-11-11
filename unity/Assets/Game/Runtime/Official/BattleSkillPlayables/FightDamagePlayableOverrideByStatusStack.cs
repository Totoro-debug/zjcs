// Migrated original damage view; see damage-view-runtime source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.BattleSkillPlayables
{
public class FightDamagePlayableOverrideByStatusStack : PlayableOverrideByStatusStack
{
	private const string kCommonPlayableTag = "myCommonPlayable";

	private const string kDamagePlayableTag = "myDamagePlayable";

	private const string kBlockPlayableTag = "myBlockPlayable";

	private const string kCritPlayableTag = "myCritPlayable";

	private const string kTriggerStatusPlayableTag = "myTriggerStatusPlayable";

	[SerializeField]
	private MyFightDamage mFightDamage;

	public override void Prepare()
	{
		mPlayableOriginOverrides.Clear();
		mPlayableOriginOverrides.Add("myCommonPlayable", mFightDamage.myCommonPlayable);
		mPlayableOriginOverrides.Add("myDamagePlayable", mFightDamage.myDamagePlayable);
		mPlayableOriginOverrides.Add("myBlockPlayable", mFightDamage.myBlockPlayable);
		mPlayableOriginOverrides.Add("myCritPlayable", mFightDamage.myCritPlayable);
		mPlayableOriginOverrides.Add("myTriggerStatusPlayable", mFightDamage.myTriggerStatusPlayable);
	}

	public override MyEntity GetSource()
	{
		if ((Object)(object)mFightDamage == (Object)null || mFightDamage.Component == null || mFightDamage.ViewServices == null)
		{
			return null;
		}
		MyEntity entity = mFightDamage.ViewServices.GetEntity(mFightDamage.Component.SkillId);
		if ((Object)(object)entity == (Object)null)
		{
			return null;
		}
		MyFightSkill component = ((Component)entity).GetComponent<MyFightSkill>();
		if ((Object)(object)component == (Object)null)
		{
			return null;
		}
		return component.Source;
	}

	public override MyEntity GetLifecycleEntity()
	{
		if (!((Object)(object)mFightDamage != (Object)null))
		{
			return null;
		}
		return mFightDamage.myEntity;
	}

	public override void OverridePlayable(string tag, GameObject playable)
	{
		switch (tag)
		{
		case "myCommonPlayable":
			mFightDamage.myCommonPlayable = playable;
			break;
		case "myDamagePlayable":
			mFightDamage.myDamagePlayable = playable;
			break;
		case "myBlockPlayable":
			mFightDamage.myBlockPlayable = playable;
			break;
		case "myCritPlayable":
			mFightDamage.myCritPlayable = playable;
			break;
		case "myTriggerStatusPlayable":
			mFightDamage.myTriggerStatusPlayable = playable;
			break;
		}
	}
}

}
