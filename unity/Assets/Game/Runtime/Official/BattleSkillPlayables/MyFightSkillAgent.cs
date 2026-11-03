// Complete selected official client source; see skill-view runtime receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
public class MyFightSkillAgent : MyComponent<FightSkillAgentComponent>
{
	public event Action<MyFightSkill> OnSkillApplied;

	public event Action<MyFightSkill> OnSkillEnded;

	public void SendSkillAppliedEvent(MyFightSkill skill)
	{
		OnSkillApplied?.Invoke(skill);
	}

	public void SendSkillEndedEvent(MyFightSkill skill)
	{
		OnSkillEnded?.Invoke(skill);
	}
}

}
