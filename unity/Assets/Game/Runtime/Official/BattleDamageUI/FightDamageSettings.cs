using System.Collections.Generic;
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
[CreateAssetMenu(fileName = "FightDamageSettings", menuName = "ScriptableObjects/FightDamageSettings")]
public class FightDamageSettings : ScriptableObject
{
	[Tooltip("元素伤害颜色配置")]
	public List<ElementDamageColor> ElementDamageColors;

	[Tooltip("元素治疗颜色")]
	public DamageColor ElementCureColor;

	[Tooltip("技能伤害颜色")]
	public DamageColor SkillCostDamageColor;

	[Tooltip("状态伤害颜色")]
	public DamageColor StatusDamageColor;

	[Tooltip("状态治疗颜色")]
	public DamageColor StatusCureColor;

	[Tooltip("默认显示颜色，正常不会使用，只有出错情况为了保证流程正常使用")]
	public DamageColor DefaultColor;

	[Tooltip("伤害飘字对象,顺序和EffectUIType一致")]
	public List<CharacterDamageUIPoolPresent> EffectUIPoolPresents;

	[Tooltip("每个伤害飘字UI间隔")]
	public Vector3 DamageUISpacing;
}
}
