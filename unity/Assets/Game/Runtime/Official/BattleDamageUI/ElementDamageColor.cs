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
[Serializable]
public struct ElementDamageColor
{
	public ElementType ElementType;

	public DamageColor Normal;

	public DamageColor Crit;

	public DamageColor Block;

	public DamageColor Immunity;
}
}
