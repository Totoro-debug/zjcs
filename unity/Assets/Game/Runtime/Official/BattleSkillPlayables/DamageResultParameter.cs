// Migrated original damage view; see damage-view-runtime source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.BattleSkillPlayables
{
public struct DamageResultParameter
{
	public long DamageValue;
	public long ShieldValue;
	public Zjcs.Official.World.NativeTerrain.Common.Pos2d Pos;
	public long AbsoluteDamage;
	public DamageTypeData DamageTypeData;
	public bool IsElementAffect;
	public float ElementConfrontation;
	public DamageResultType ResultType;
	public ElementType SkillElementType;
	public bool IsCure => ResultType == DamageResultType.Cure;
	public bool IsDamage => ResultType == DamageResultType.Damage;

	public long GetDamageByType(DamageResultValueType type)
	{
		return type switch
		{
			DamageResultValueType.FinalDamageValue => DamageValue,
			DamageResultValueType.ShieldAddFinalDamageValue => DamageValue - ShieldValue,
			DamageResultValueType.AbsoluteDamage => AbsoluteDamage,
			_ => 0L,
		};
	}
}

}
