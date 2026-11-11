// Migrated original damage view; see damage-view-runtime source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.BattleSkillPlayables
{
public class MyFightRoleFight : MyComponent<BattleRoleFightViewData>
{
	public List<Collider> myRtColliders;

	public event System.Action<MyFightRoleFight, MyFightDamage> OnDamage;

	public event System.Action<MyFightRoleFight, MyFightDamage> OnHit;

	public void FireDamageEvent(MyFightDamage damage)
	{


		try
		{
			if (OnDamage != null)
			{
				OnDamage(this, damage);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogException(ex);
		}
	}

	public void FireHitEvent(MyFightDamage damage)
	{


		try
		{
			if (OnHit != null)
			{
				OnHit(this, damage);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogException(ex);
		}
	}
}

}
