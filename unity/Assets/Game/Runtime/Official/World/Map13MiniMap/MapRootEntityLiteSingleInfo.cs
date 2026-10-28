// Original shared map data; only MessagePack transport annotations removed.
using UnityEngine;

using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public struct MapRootEntityLiteSingleInfo
{

	public int MapClassId;

	public int CharacterClassId;

	public int MemberClassId;

	public Pos2d MapGroundSize;

	public Vector3 MapGroundOriginPos;

	public MapRootEntityLiteGridInfo[][] PosDataArray;

	public MapRootEntityLiteAreaInfo[] AreaInfos;

	public int MaxMapFindPathStep;

	public bool TryGetAreaLiteInfo(int area, out MapRootEntityLiteAreaInfo areaLiteInfo)
	{
		areaLiteInfo = default;
		if (area < 0 || AreaInfos == null || area >= AreaInfos.Length)
		{
			return false;
		}
		areaLiteInfo = AreaInfos[area];
		return true;
	}

	public bool TryGetPosData(int x, int y, out MapRootEntityLiteGridInfo info)
	{
		info = default;
		if (y < 0 || y >= PosDataArray.Length || x < 0 || x >= PosDataArray[y].Length)
		{
			return false;
		}
		info = PosDataArray[y][x];
		return true;
	}

	public int GridToArea(Pos2d grid)
	{
		if (grid.X >= MapGroundSize.X || grid.Y >= MapGroundSize.Y)
		{
			return -1;
		}
		return PosDataArray[grid.Y][grid.X].Area;
	}

	public Vector3 GridToPos(Pos2d grid)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		Vector3 mapGroundOriginPos = MapGroundOriginPos;
		return new Vector3(mapGroundOriginPos.x + 1f * (float)grid.X, mapGroundOriginPos.y + 1f * (float)grid.Y, mapGroundOriginPos.z);
	}

	public Pos2d PosToGrid(Vector3 pos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Vector3 mapGroundOriginPos = MapGroundOriginPos;
		Vector3 dir = pos - mapGroundOriginPos + new Vector3(0.5f, 0.5f, 0f);
		return DirToGrid(dir);
	}

	private Pos2d DirToGrid(Vector3 dir)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new Pos2d((int)(dir.x / 1f), (int)(dir.y / 1f));
	}
}

}
