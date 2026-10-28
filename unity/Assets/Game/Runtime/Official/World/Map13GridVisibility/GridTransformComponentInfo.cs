// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;
using UnityEngine;





namespace Zjcs.Official.World.Map13GridVisibility
{
[Serializable]
public class GridTransformComponentInfo : VisibilityComponentInfo
{
	[Tooltip("体型信息")]

	public List<GridTransformComponentBodyPosInfo> BodyRangeInfos;

	[Tooltip("体型间穿行阻拦信息(两个点都配，这里默认是无向边，也就是PosOne和PosTwo互相都不能到达，但是两个点中至少得有一个点在body上)")]

	public List<GridTransformComponentBodyCrossBlockedInfo> BodyCrossBlockedInfos;

	[Tooltip("解锁几个占位格子后，后解锁全部占位格子")]

	public int UnlockWholeRequestCount = 1;

	[Tooltip("真实的体型中心点（例如2*2的体型，真实中心点就是(0.5,0.5)。这样旋转不会导致角色站位变化）")]

	public Vector2 RealCenter;

	public bool IsRigidBody = true;

	private List<Pos2d> mBodyRanges;

	private HashSet<Pos2d> mBodyAddOneRanges;

	private Dictionary<Pos2d, GridTransformComponentBodyPosInfo> mBodyInfoDict;

	private HashSet<Pos2d> mBodyAddOneRangesTempPosList;

	private Pos2d mBodyAddOneRangesTempOrigPos = new Pos2d(int.MinValue, int.MinValue);

	public override void Init()
	{
		base.Init();
		mBodyInfoDict = new Dictionary<Pos2d, GridTransformComponentBodyPosInfo>();
		mBodyRanges = new List<Pos2d>();
		mBodyAddOneRanges = new HashSet<Pos2d>();
		foreach (GridTransformComponentBodyPosInfo info in BodyRangeInfos)
		{
			mBodyInfoDict.Add(info.Pos, info);
			mBodyRanges.Add(info.Pos);
			mBodyAddOneRanges.Add(info.Pos);
			Pos2d.EightDirs.ForEach((Pos2d dir) =>
			{
				mBodyAddOneRanges.Add(info.Pos + dir);
			});
		}
	}

	public List<Pos2d> GetBodyRange()
	{
		return mBodyRanges;
	}

	public HashSet<Pos2d> GetBodyAddOneRange(Pos2d centerPos, HashSet<Pos2d> outPosList)
	{
		outPosList.UnionWith(GetBodyAddOneRange(centerPos));
		return outPosList;
	}

	public HashSet<Pos2d> GetBodyAddOneRange(Pos2d centerPos)
	{
		if (mBodyAddOneRangesTempOrigPos != centerPos)
		{
			if (mBodyAddOneRangesTempPosList == null)
			{
				mBodyAddOneRangesTempPosList = new HashSet<Pos2d>();
			}
			else
			{
				mBodyAddOneRangesTempPosList.Clear();
			}
			foreach (Pos2d mBodyAddOneRange in mBodyAddOneRanges)
			{
				mBodyAddOneRangesTempPosList.Add(centerPos + mBodyAddOneRange);
			}
			mBodyAddOneRangesTempOrigPos = centerPos;
		}
		return mBodyAddOneRangesTempPosList;
	}

	public bool InBodyRange(Pos2d offset)
	{
		return mBodyInfoDict.ContainsKey(offset);
	}

	public bool TryGetBodyInfo(Pos2d offset, out GridTransformComponentBodyPosInfo info)
	{
		return mBodyInfoDict.TryGetValue(offset, out info);
	}
}

}
