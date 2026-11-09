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
public static class FightHitViewFormula
{
public struct SkillHitBaseDelayInfoProvider
	{
		public Vector3 Offset;

		public Vector3 TargetOffset;

		public Vector3 RotateCenterOffset;

		public float DelayDistFactor;

		public bool IsUseCenterRotation;
	}
	public static float CalcSkillHitDelay(SkillHitBaseDelayInfoProvider hitInfo, Pos2d startPos, Pos2d hitPos, IFightSkillViewServices ground, bool flipX)
	{









		Vector3 val = CalcSkillStartPos(ground, hitInfo.IsUseCenterRotation, flipX, startPos, hitPos, hitInfo.Offset, hitInfo.TargetOffset, hitInfo.RotateCenterOffset);
		Vector3 val2 = ground.GridToPos(hitPos) - val;
		return val2.magnitude * hitInfo.DelayDistFactor;
	}

	public static Vector3 CalcSkillStartPos(IFightSkillViewServices ground, bool isUseCenterRotation, bool flipX, Pos2d startPos, Pos2d targetPos, Vector3 sourceOffset, Vector3 targetOffset, Vector3 rotateCenterOffset)
	{






























		Vector3 val = ground.GridToPos(startPos);
		Vector3 zero = Vector3.zero;
		if (isUseCenterRotation)
		{
			zero.x = (flipX ? (val.x - rotateCenterOffset.x) : (val.x + rotateCenterOffset.x));
			zero.y = val.y + rotateCenterOffset.y;
			zero.z = val.z + rotateCenterOffset.z;
		}
		val = GetPosByOffset(val, sourceOffset, flipX);
		if (isUseCenterRotation)
		{
			Vector3 posByOffset = GetPosByOffset(ground, targetPos, targetOffset, flipX);
			Vector3 val2 = val - zero;
			Vector3 val3 = posByOffset - zero;
			return val3.normalized * val2.magnitude + zero;
		}
		return val;
	}

	public static Vector3 GetPosByOffset(IFightSkillViewServices ground, Pos2d pos2d, Vector3 offset, bool flipX)
	{







		Vector3 pos = ground.GridToPos(pos2d);
		return GetPosByOffset(pos, offset, flipX);
	}

	public static Vector3 GetPosByOffset(Vector3 pos, Vector3 offset, bool flipX)
	{







		pos.x = (flipX ? (pos.x - offset.x) : (pos.x + offset.x));
		pos.y += offset.y;
		pos.z += offset.z;
		return pos;
	}

}
}
