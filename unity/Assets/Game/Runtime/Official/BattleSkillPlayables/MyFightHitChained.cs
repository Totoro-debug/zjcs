// Migrated official hit presentation; see hit-view-runtime source receipt.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
public class MyFightHitChained : MyFightHitBase<FightHitChainedComponent, FightHitChainedComponentInfo>
{
	[Tooltip("上次hit为起点要播放HitPlayable, 第一次hit的起点是技能位置")]
	public GameObject myHitToNextHitPlayable;

	private GameObject mStartGo;

	public override void OnStart(bool newEntity)
	{










		base.OnStart(newEntity);
		if ((Object)(object)myHitToNextHitPlayable != (Object)null && TryGetMySkill(out var skill) && (Object)(object)skill.Source != (Object)null)
		{
			if (mStartGo == null)
			{
				mStartGo = new GameObject($"ChainedHit_{Component.Entity.ClassId}_target");
			}
			mStartGo.transform.SetParent(((Component)this).transform.parent);
			IFightSkillViewServices component = ViewServices;
			Vector3 pos = ViewServices.ToWorldPos(component.GridToPos(Component.StartGridPos));
			pos = FightHitViewFormula.GetPosByOffset(pos, Info.Offset, skill.Source.myGridTransform.FlipX);
			mStartGo.transform.position = pos;
			Dictionary<string, object> param = new Dictionary<string, object> {
			{
				"ChainedHitCharacterGo",
				((Component)skill.Source).gameObject
			} };
			mMyPlayableManager.Play(myHitToNextHitPlayable, mStartGo, ((Component)this).gameObject, 1f, null, param);
		}
	}

	protected override Pos2d GetStartPos()
	{
		return Component.StartGridPos;
	}

	public override void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		base.TryGetPlayables(ref playables);
		if (playables != null && (Object)(object)myHitToNextHitPlayable != (Object)null)
		{
			playables.Add(myHitToNextHitPlayable);
		}
	}
}

}
