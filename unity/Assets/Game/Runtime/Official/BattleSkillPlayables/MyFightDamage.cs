// Migrated original damage view; see damage-view-runtime source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.BattleSkillPlayables
{
[RequireComponent(typeof(MyTransform))]
public class MyFightDamage : MyComponent<FightDamageComponent, FightDamageComponentInfo>, IFightGetUsePlayable
{
	[Tooltip("无论是否暴击，是否档格，都播放的效果")]
	public GameObject myCommonPlayable;

	[Tooltip("仅当没有暴击并且没有档格的时候播放")]
	public GameObject myDamagePlayable;

	[Tooltip("仅档格的时候播放")]
	public GameObject myBlockPlayable;

	[Tooltip("仅暴击的时候播放")]
	public GameObject myCritPlayable;

	[Tooltip("仅触发状态的时候播放")]
	public GameObject myTriggerStatusPlayable;

	[Tooltip("受创伤害为0是是否播放飘血效果（部分受创仅作为触发其它效果使用，不飘血）")]
	public bool myShowZeroDamage = true;

	[Tooltip("隐藏飘字(无论如何都不会显示飘字)")]
	public bool myHideDamage;

	public IMyPlayableManager mMyPlayableManager { get; set; }
	public IFightDamageViewServices ViewServices { get; set; }

	public override void OnStart(bool newEntity)
	{
		MyEntity entity = ViewServices.GetEntity(Component.TargetId);
		if ((Object)(object)entity == (Object)null)
		{
			return;
		}
		ViewServices.FireDamageEvent(entity, this);
		MyEntity entity2 = ViewServices.GetEntity(Component.SkillId);
		if ((Object)(object)entity2 == (Object)null)
		{
			return;
		}
		MyFightSkill component = ((Component)entity2).GetComponent<MyFightSkill>();
		if (!((Object)(object)component.Source == (Object)null))
		{
			ViewServices.FireHitEvent(component.Source, this);
			Dictionary<string, object> param = new Dictionary<string, object> {
			{
				"Damage",
				((Component)this).gameObject
			} };
			if ((Object)(object)myCommonPlayable != (Object)null)
			{
				mMyPlayableManager.Play(myCommonPlayable, ((Component)entity).gameObject, ((Component)component.Source).gameObject, 1f, null, param);
			}
			DamageResultParameter damageResult = Component.DamageResult;
			if (damageResult.DamageTypeData.IsBlock && (Object)(object)myBlockPlayable != (Object)null)
			{
				mMyPlayableManager.Play(myBlockPlayable, ((Component)entity).gameObject, ((Component)component.Source).gameObject, 1f, null, param);
			}
			else if (damageResult.DamageTypeData.IsCrit && (Object)(object)myCritPlayable != (Object)null)
			{
				mMyPlayableManager.Play(myCritPlayable, ((Component)entity).gameObject, ((Component)component.Source).gameObject, 1f, null, param);
			}
			else if ((Object)(object)myDamagePlayable != (Object)null)
			{
				mMyPlayableManager.Play(myDamagePlayable, ((Component)entity).gameObject, ((Component)component.Source).gameObject, 1f, null, param);
			}
			if (Component.IsDamageTriggerStatus && myTriggerStatusPlayable)
			{
				mMyPlayableManager.Play(myTriggerStatusPlayable, ((Component)entity).gameObject, ((Component)component.Source).gameObject, 1f, null, param);
			}
		}
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)myCommonPlayable != (Object)null)
			{
				playables.Add(myCommonPlayable);
			}
			if ((Object)(object)myDamagePlayable != (Object)null)
			{
				playables.Add(myDamagePlayable);
			}
			if ((Object)(object)myBlockPlayable != (Object)null)
			{
				playables.Add(myBlockPlayable);
			}
			if ((Object)(object)myCritPlayable != (Object)null)
			{
				playables.Add(myCritPlayable);
			}
		}
	}
}

}
