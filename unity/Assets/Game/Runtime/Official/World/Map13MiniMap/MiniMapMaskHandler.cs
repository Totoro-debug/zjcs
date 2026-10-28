// Migrated from selected official code; see minimap mask source receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public class MiniMapMaskHandler : MonoBehaviour
{
	public delegate bool Pos2dDelegate(Pos2d pos);

	[Serializable]
	private class TilePatternData
	{
		private const int cFourSideValue = 4;

		private const float cRightAngleDegree = 90f;

		public Tile MyTile;

		public TilePatternConditionData DefaultCondition;

		private List<TilePatternConditionData> mConditions;

		public void Init()
		{
			mConditions = new List<TilePatternConditionData>();
			for (int i = 0; i < 4; i++)
			{
				mConditions.Add(new TilePatternConditionData(MyTile, (float)i * 90f));
			}
			foreach (TilePatternSinglePosData posData in DefaultCondition.PosDatas)
			{
				mConditions[0].PosDatas.Add(posData);
				mConditions[1].PosDatas.Add(new TilePatternSinglePosData(-posData.Pos.Y, posData.Pos.X, posData.Locked));
				mConditions[2].PosDatas.Add(new TilePatternSinglePosData(-posData.Pos.X, -posData.Pos.Y, posData.Locked));
				mConditions[3].PosDatas.Add(new TilePatternSinglePosData(posData.Pos.Y, -posData.Pos.X, posData.Locked));
			}
		}

		public TilePatternConditionData GetMetPattern(Pos2d pos, Pos2dDelegate isPosLocked, Pos2dDelegate gridMaskable)
		{
			foreach (TilePatternConditionData mCondition in mConditions)
			{
				if (mCondition.IsMet(pos, isPosLocked, gridMaskable))
				{
					return mCondition;
				}
			}
			return null;
		}
	}

	[Serializable]
	private class TilePatternConditionData
	{
		public List<TilePatternSinglePosData> PosDatas = new List<TilePatternSinglePosData>();

		public readonly Tile MyTile;

		public readonly Quaternion Quaternion;

		public TilePatternConditionData(Tile tile, float angle)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			MyTile = tile;
			Quaternion = Quaternion.Euler(0f, 0f, angle);
		}

		public bool IsMet(Pos2d pos, Pos2dDelegate isPosLocked, Pos2dDelegate gridMaskable)
		{
			foreach (TilePatternSinglePosData posData in PosDatas)
			{
				Pos2d pos2 = pos + posData.Pos;
				bool flag = gridMaskable(pos2) && isPosLocked(pos2);
				if (posData.Locked != flag)
				{
					return false;
				}
			}
			return true;
		}
	}

	[Serializable]
	private class TilePatternSinglePosData
	{
		public Pos2d Pos;

		public bool Locked;

		public TilePatternSinglePosData(int x, int y, bool locked)
		{
			Pos = new Pos2d(x, y);
			Locked = locked;
		}
	}

	[Flags]
	private enum AroundLockGridType
	{
		None = 0,
		Empty = 1,
		Edge = 2,
		Corner = 4
	}

	[SerializeField]
	private Grid mMaskGrid;

	[SerializeField]
	private Tilemap mMaskTilemap;

	[SerializeField]
	private StateSwitcher<bool> mMaskedGoSwitcher;

	[SerializeField]
	private List<TilePatternData> mCornerPatterns;

	[SerializeField]
	private List<TilePatternData> mEdgePatterns;

	private int mMapClassId;

	private Pos2dDelegate mIsPosLocked;

	private Pos2dDelegate mGridMaskable;

	private List<Pos2d> mAddedPosCacheList = new List<Pos2d>();

	private List<Pos2d> mRemovedPosCacheList = new List<Pos2d>();

	private HashSet<Pos2d> mTileExistPosSet = new HashSet<Pos2d>();

	private bool mMaskedGoShowing;

	private void Awake()
	{
		mMaskedGoSwitcher.SetState(state: false);
		((Behaviour)mMaskGrid).enabled = false;
		foreach (TilePatternData mCornerPattern in mCornerPatterns)
		{
			mCornerPattern.Init();
		}
		foreach (TilePatternData mEdgePattern in mEdgePatterns)
		{
			mEdgePattern.Init();
		}
	}


    // Local resource/session binding supplies the original MapUtils lookup.
    public Func<int, Pos2d, Vector3> GridToWorld { private get; set; }
    private Vector3Int GetTilePosByGrid(int mapClassId, Pos2d grid)
    {
        if (GridToWorld == null) throw new InvalidOperationException("Original minimap grid binding is missing.");
        Vector3 val = GridToWorld(mapClassId, grid);
        return new Vector3Int((int)((val.x - 0.5f) / 1f), (int)((val.y - 0.5f) / 1f), (int)(val.z / 1f));
    }

	public void Init(int mapClassId, MapRootEntityLiteSingleInfo mMapRootEntLiteInfo, Pos2dDelegate isPosLocked, Pos2dDelegate gridMaskable)
	{
		mMapClassId = mapClassId;
		mIsPosLocked = isPosLocked;
		mGridMaskable = gridMaskable;
		GLoggerErrorInterpolatedStringHandler message;
		bool enabled;
		if (mIsPosLocked == null)
		{
			message = new GLoggerErrorInterpolatedStringHandler(53, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("MiniMapMaskHandler.Init use param isPosLocked is null");
			}
			Log.Error(ref message, null, "Init", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MiniMapMaskHandler.cs", 62);
			return;
		}
		if (mGridMaskable == null)
		{
			message = new GLoggerErrorInterpolatedStringHandler(54, 0, out enabled);
			if (enabled)
			{
				message.AppendLiteral("MiniMapMaskHandler.Init use param gridMaskable is null");
			}
			Log.Error(ref message, null, "Init", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MiniMapMaskHandler.cs", 67);
			return;
		}
		for (int num = mMapRootEntLiteInfo.MapGroundSize.Y - 1; num >= 0; num--)
		{
			MapRootEntityLiteGridInfo[] array = mMapRootEntLiteInfo.PosDataArray[num];
			for (int num2 = mMapRootEntLiteInfo.MapGroundSize.X - 1; num2 >= 0; num2--)
			{
				if (array[num2].Area >= 0)
				{
					DoFixPos(new Pos2d(num2, num));
				}
			}
		}
		((Behaviour)mMaskGrid).enabled = true;
	}

	public void UnlockSomePos(List<Pos2d> unlockPosList, Pos2d mePos, int viewRange)
	{
		foreach (Pos2d unlockPos in unlockPosList)
		{
			DoFixPos(unlockPos);
		}
		RefreshTileExistPosSet();
		foreach (Pos2d item in mTileExistPosSet)
		{
			DoFixPos(item);
		}
		RefreshTileExistPosSet();
		void RefreshTileExistPosSet()
		{
			foreach (Pos2d mAddedPosCache in mAddedPosCacheList)
			{
				mTileExistPosSet.Add(mAddedPosCache);
			}
			foreach (Pos2d mRemovedPosCache in mRemovedPosCacheList)
			{
				mTileExistPosSet.Remove(mRemovedPosCache);
			}
			mAddedPosCacheList.Clear();
			mRemovedPosCacheList.Clear();
		}
	}

	public void ShowMaskedGo()
	{
		if (!mMaskedGoShowing)
		{
			mMaskedGoShowing = true;
			mMaskedGoSwitcher.SetState(state: true);
		}
	}

	private void DoFixPos(Pos2d pos)
	{
		if (!mIsPosLocked(pos) && mGridMaskable(pos))
		{
			SetTile(pos, GetPosPattern(pos));
		}
	}

	private void SetTile(Pos2d pos, TilePatternConditionData data)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		Vector3Int tilePosByGrid = GetTilePosByGrid(mMapClassId, pos);
		mMaskTilemap.SetTile(tilePosByGrid, (TileBase)(object)data?.MyTile);
		if (data == null)
		{
			mRemovedPosCacheList.Add(pos);
			return;
		}
		mAddedPosCacheList.Add(pos);
		Matrix4x4 transformMatrix = mMaskTilemap.GetTransformMatrix(tilePosByGrid);
		transformMatrix.SetTRS((Vector3)transformMatrix.GetColumn(3), data.Quaternion, Vector3.one);
		mMaskTilemap.SetTransformMatrix(tilePosByGrid, transformMatrix);
	}

	private TilePatternConditionData GetPosPattern(Pos2d pos)
	{
		AroundLockGridType aroundLockGridType = PosAroundLockType(pos);
		if (aroundLockGridType == AroundLockGridType.Empty)
		{
			return null;
		}
		GLoggerErrorInterpolatedStringHandler message;
		bool enabled;
		if (aroundLockGridType.HasFlag(AroundLockGridType.Edge))
		{
			TilePatternConditionData metPattern = GetMetPattern(pos, mEdgePatterns);
			if (metPattern == null)
			{
				message = new GLoggerErrorInterpolatedStringHandler(35, 0, out enabled);
				if (enabled)
				{
					message.AppendLiteral("Edge pattern can not match this pos");
				}
				Log.Error(ref message, null, "GetPosPattern", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MiniMapMaskHandler.cs", 172);
			}
			return metPattern;
		}
		if (aroundLockGridType.HasFlag(AroundLockGridType.Corner))
		{
			TilePatternConditionData metPattern2 = GetMetPattern(pos, mCornerPatterns);
			if (metPattern2 == null)
			{
				message = new GLoggerErrorInterpolatedStringHandler(44, 1, out enabled);
				if (enabled)
				{
					message.AppendLiteral("Corner pattern can not match this pos, type:");
					message.AppendFormatted(aroundLockGridType, 0, null, "type");
				}
				Log.Error(ref message, null, "GetPosPattern", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MiniMapMaskHandler.cs", 183);
				foreach (Pos2d eightDir in Pos2d.EightDirs)
				{
					Pos2d pos2 = pos + eightDir;
					message = new GLoggerErrorInterpolatedStringHandler(33, 4, out enabled);
					if (enabled)
					{
						message.AppendLiteral("pos:");
						message.AppendFormatted(pos, 0, null, "pos");
						message.AppendLiteral(", offset:");
						message.AppendFormatted(eightDir, 0, null, "offset");
						message.AppendLiteral(", maskable:");
						message.AppendFormatted(mGridMaskable(pos2), 0, null, "mGridMaskable(tempPos)");
						message.AppendLiteral(", Locked:");
						message.AppendFormatted(mIsPosLocked(pos2), 0, null, "mIsPosLocked(tempPos)");
					}
					Log.Error(ref message, null, "GetPosPattern", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/MiniMapMaskHandler.cs", 187);
				}
			}
			return metPattern2;
		}
		return null;
	}

	private AroundLockGridType PosAroundLockType(Pos2d pos)
	{
		AroundLockGridType aroundLockGridType = AroundLockGridType.None;
		foreach (Pos2d eightDir in Pos2d.EightDirs)
		{
			Pos2d pos2 = pos + eightDir;
			if (mGridMaskable(pos2) && mIsPosLocked(pos2))
			{
				aroundLockGridType = ((eightDir.X != 0 && eightDir.Y != 0) ? (aroundLockGridType | AroundLockGridType.Corner) : (aroundLockGridType | AroundLockGridType.Edge));
				if (aroundLockGridType.HasFlag(AroundLockGridType.Edge) && aroundLockGridType.HasFlag(AroundLockGridType.Corner))
				{
					return aroundLockGridType;
				}
			}
		}
		if (aroundLockGridType != AroundLockGridType.None)
		{
			return aroundLockGridType;
		}
		return AroundLockGridType.Empty;
	}

	private TilePatternConditionData GetMetPattern(Pos2d pos, List<TilePatternData> patterns)
	{
		foreach (TilePatternData pattern in patterns)
		{
			TilePatternConditionData metPattern = pattern.GetMetPattern(pos, mIsPosLocked, mGridMaskable);
			if (metPattern != null)
			{
				return metPattern;
			}
		}
		return null;
	}
}

}
