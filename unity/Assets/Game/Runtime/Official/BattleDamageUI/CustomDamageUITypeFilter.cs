using Zjcs.Official.BattleSkillPlayables;
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
public class CustomDamageUITypeFilter : MonoBehaviour, ICustomDamageUIFilter
{
	public bool IsCrit;

	public bool IsBlock;

	public bool IsDodge;

	public bool IsBlinding;

	public bool IsImmunity;

	public bool IsBreakResilience;

	public bool IsTrueDamage;

	public bool IsFilter(MyFightDamage dmg)
	{
		DamageTypeData damageTypeData = dmg.Component.DamageResult.DamageTypeData;
		if ((!IsCrit || !damageTypeData.IsCrit) && (!IsBlock || !damageTypeData.IsBlock) && (!IsDodge || !damageTypeData.IsDodge) && (!IsBlinding || !damageTypeData.IsBlinding) && (!IsImmunity || !damageTypeData.IsImmunity) && (!IsBreakResilience || !damageTypeData.IsBreakResilience))
		{
			if (IsTrueDamage)
			{
				return damageTypeData.IsTrueDamage;
			}
			return false;
		}
		return true;
	}
}
}
