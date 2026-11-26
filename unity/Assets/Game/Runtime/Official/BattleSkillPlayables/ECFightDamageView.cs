// Current official EC view; explicit entity/DI boundaries are recorded in its source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
[System.Serializable]
public class ECFightDamageView : ECViewBase, IFightGetUsePlayable
{
	[Tooltip("无论是否暴击，是否档格，都播放的效果")]
	[SerializeField]
	private GameObject mCommonPlayable;

	[Tooltip("仅当没有暴击并且没有档格的时候播放")]
	[SerializeField]
	private GameObject mDamagePlayable;

	[Tooltip("仅档格的时候播放")]
	[SerializeField]
	private GameObject mBlockPlayable;

	[Tooltip("仅暴击的时候播放")]
	[SerializeField]
	private GameObject mCritPlayable;

	[Tooltip("仅触发状态的时候播放")]
	[SerializeField]
	private GameObject mTriggerStatusPlayable;

	[Tooltip("受创伤害为0是是否播放飘血效果（部分受创仅作为触发其它效果使用，不飘血）")]
	[SerializeField]
	private bool mShowZeroDamage = true;

	[Tooltip("隐藏飘字(无论如何都不会显示飘字)")]
	[SerializeField]
	private bool mHideDamage;

	private FightDamageComponent mDamageComponent;

	private MyEntity mDamageEntity;

	public IMyPlayableManager MyPlayableManager { get; set; }
	public IFightSkillViewServices ViewServices { get; set; }

	public bool ShowZeroDamage => mShowZeroDamage;

	public bool HideDamage => mHideDamage;

	public FightDamageComponent DamageComponent
	{
		get
		{
			if (mDamageComponent == null)
			{
				mDamageComponent = GetComponent<FightDamageComponent>();
			}
			return mDamageComponent;
		}
	}

	public ECViewEntityData Entity => mEntity;

	protected override void Load()
	{
		base.Load();
		mDamageComponent = GetComponent<FightDamageComponent>();
		if (mDamageComponent == null)
		{
			return;
		}
		MyEntity componentInParent = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		if ((Object)(object)componentInParent == (Object)null)
		{
			return;
		}
		mDamageEntity = componentInParent;
		MyContainer myContainer = componentInParent.myContainer;
		if ((Object)(object)myContainer == (Object)null)
		{
			return;
		}
		MyEntity entity = ViewServices.GetEntity(mDamageComponent.TargetId);
		if ((Object)(object)entity == (Object)null)
		{
			return;
		}
		var carrier = mDamageEntity.GetComponent<MyFightDamage>();
		carrier.myShowZeroDamage = mShowZeroDamage; carrier.myHideDamage = mHideDamage;
		carrier.myCommonPlayable = mCommonPlayable; carrier.myDamagePlayable = mDamagePlayable;
		carrier.myBlockPlayable = mBlockPlayable; carrier.myCritPlayable = mCritPlayable;
		carrier.myTriggerStatusPlayable = mTriggerStatusPlayable;
		((Component)entity).GetComponent<MyFightRoleFight>().FireDamageEvent(carrier);
		MyEntity skillEntity = ViewServices.GetEntity(mDamageComponent.SkillId);
		FightSkillComponent skill = skillEntity ? skillEntity.GetComponent<MyFightSkill>()?.Component : null;
		if (skill == null)
		{
			return;
		}
		MyEntity entity2 = ViewServices.GetEntity(skill.SrcEntityId);
		if (!((Object)(object)entity2 == (Object)null))
		{
			((Component)entity2).GetComponent<MyFightRoleFight>().FireHitEvent(carrier);
			GameObject gameObject = ((Component)entity).gameObject;
			GameObject gameObject2 = ((Component)entity2).gameObject;
			Dictionary<string, object> param = new Dictionary<string, object> {
			{
				"Damage",
				((Component)componentInParent).gameObject
			} };
			PlayWithOverride("myCommonPlayable", mCommonPlayable, gameObject, gameObject2, param);
			DamageResultParameter damageResult = mDamageComponent.DamageResult;
			if (damageResult.DamageTypeData.IsBlock)
			{
				PlayWithOverride("myBlockPlayable", mBlockPlayable, gameObject, gameObject2, param);
			}
			else if (damageResult.DamageTypeData.IsCrit)
			{
				PlayWithOverride("myCritPlayable", mCritPlayable, gameObject, gameObject2, param);
			}
			else
			{
				PlayWithOverride("myDamagePlayable", mDamagePlayable, gameObject, gameObject2, param);
			}
			if (mDamageComponent.IsDamageTriggerStatus)
			{
				PlayWithOverride("myTriggerStatusPlayable", mTriggerStatusPlayable, gameObject, gameObject2, param);
			}
		}
	}

	private void PlayPlayable(GameObject playable, GameObject source, GameObject target, Dictionary<string, object> param)
	{
		if (!((Object)(object)playable == (Object)null))
		{
			MyPlayableManager.Play(playable, source, target, 1f, null, param);
		}
	}

	private void PlayWithOverride(string tag, GameObject playable, GameObject source, GameObject target, Dictionary<string, object> param)
	{
		BattlePlayableOverrideView eCPlayableOverrideView = (((Object)(object)mDamageEntity != (Object)null) ? CurrentFightViewLookup.GetOverride(mDamageEntity) : null);
		GameObject playable2 = ((eCPlayableOverrideView != null) ? eCPlayableOverrideView.Resolve(tag, playable) : playable);
		PlayPlayable(playable2, source, target, param);
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)mCommonPlayable != (Object)null)
			{
				playables.Add(mCommonPlayable);
			}
			if ((Object)(object)mDamagePlayable != (Object)null)
			{
				playables.Add(mDamagePlayable);
			}
			if ((Object)(object)mBlockPlayable != (Object)null)
			{
				playables.Add(mBlockPlayable);
			}
			if ((Object)(object)mCritPlayable != (Object)null)
			{
				playables.Add(mCritPlayable);
			}
			if ((Object)(object)mTriggerStatusPlayable != (Object)null)
			{
				playables.Add(mTriggerStatusPlayable);
			}
		}
	}
}

}
