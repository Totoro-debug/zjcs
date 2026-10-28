// Complete original visible-range implementation; see Map13 minimap view source receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public abstract class MapViewRangeHandlerBase : IMapViewRangeHandler, IDisposable
{
	protected Pos2d mCurGridPos;

	protected List<Pos2d> mVisiablePosList = new List<Pos2d>();

	public abstract GAction<IMapViewRangeHandler> OnVisibleRangeUpdate { get; set; }

	public IReadOnlyList<Pos2d> VisiblePosList => mVisiablePosList;

	protected abstract Pos2d ViewRange { get; }

	public abstract void Dispose();

	public bool IsEntityInViewRange(IMiniMapEntityBody gridTransform)
	{
		Pos2d pos = gridTransform.Pos;
		foreach (MiniMapBodyOffset bodyRangeInfo in gridTransform.BodyRangeInfos)
		{
			if (IsPosInViewRange(pos + bodyRangeInfo.Pos))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsPosInViewRange(Pos2d pos)
	{
		if (pos.X >= mCurGridPos.X - ViewRange.X && pos.X <= mCurGridPos.X + ViewRange.X)
		{
			if (pos.Y >= mCurGridPos.Y - ViewRange.Y)
			{
				return pos.Y <= mCurGridPos.Y + ViewRange.Y;
			}
			return false;
		}
		return false;
	}

	public bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos)
	{
		Pos2d pos2d = new Pos2d(mCurGridPos.X - ViewRange.X, mCurGridPos.Y - ViewRange.Y);
		Pos2d pos2d2 = new Pos2d(mCurGridPos.X + ViewRange.X, mCurGridPos.Y + ViewRange.Y);
		bool num = maxPos.X >= pos2d.X && minPos.X <= pos2d2.X;
		bool flag = maxPos.Y >= pos2d.Y && minPos.Y <= pos2d2.Y;
		return num & flag;
	}

	protected void CalcVisiablePosList(Pos2d toPos, List<Pos2d> visiablePosList)
	{
		mVisiablePosList.Clear();
		int num = toPos.X - ViewRange.X;
		int num2 = toPos.X + ViewRange.X;
		int num3 = toPos.Y - ViewRange.Y;
		int num4 = toPos.Y + ViewRange.Y;
		for (int i = num; i <= num2; i++)
		{
			for (int j = num3; j <= num4; j++)
			{
				visiablePosList.Add(new Pos2d(i, j));
			}
		}
	}
}

}
