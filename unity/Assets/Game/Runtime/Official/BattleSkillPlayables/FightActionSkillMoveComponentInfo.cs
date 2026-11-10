// Migrated selected original source; see skill movement runtime receipt.
namespace Zjcs.Official.BattleSkillPlayables
{
using System;
using UnityEngine;


[Serializable]
public class FightActionSkillMoveComponentInfo
{
	[Header("移动")]
	[Tooltip("是否根据路径更新移动者的翻转。")]
	public bool UpdateFlipX;

	[Tooltip("移动时面朝起始点而非终点。需要UpdateFlipX为true时生效。")]
	public bool ReverseFlipX;

	[Tooltip("可以跨过的高度。")]
	public int PassHeight = 999;

	[Tooltip("如果 >= 0，则为固定时间的位移，时间为每段移动的时间。")]
	public float MoveDuration = -1f;

	[Tooltip("如果MoveDuration < 0，则采用速度值，为固定速度的位移。")]
	public float MoveSpeed = -1f;

	[Header("碰撞")]
	[Tooltip("碰撞深入多少格")]
	public float BlockedLen = 0.5f;

	[Tooltip("移动后的碰撞伤害技能。移动指定距离时，若路径有其他对象阻拦导致中断的碰撞伤害技能。")]
	public int BlockedSkillId;

	[Header("销毁")]
	[Tooltip("碰撞后的延迟销毁时间。若没有碰撞，则为移动后的延迟销毁时间。")]
	public float DelayDestroyTime;

	[Header("强制到达")]
	[Tooltip("勾选后一定会到达指定位置，并将该位置范围内所有的敌人推出")]
	public bool ForceToTargetPos;

	[Tooltip("强制推出的技能Id(这里没填会默认使用BlockedSkillId,都没有将无法执行强制击飞)")]
	public int ForcePushOutSkillId;
}

}
