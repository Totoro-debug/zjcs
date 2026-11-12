
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
public enum EffectUIType
{
	NormalDamageUI,
	CritDamageUI,
	BlockDamageUI,
	CureUI,
	ImmunityDamageUI,
	AbyssNormalDamageUI,
	AbyssCritDamageUI,
	AbyssBlockDamageUI,
	DodgeDamageUI,
	BlindingDamageUI,
	ResetSkillCDUI,
	CustomActionUI,
	DisperseDamageUI
}
}
