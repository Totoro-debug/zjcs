// Migrated from selected official terrain code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileChunkData : ScriptableObject
{
	[Serializable]
	public class Pair
	{
		public short Index;

		public short TileIndex;
	}

	[Serializable]
	public class TilemapData
	{
		public string Name;

		public Vector2 CellSize;

		public Pair[] Tiles;

		public bool HasOffset;

		public short[] MatrixIndexes;
	}

	public bool IsPartial;

	public Vector2Int Pos;

	public BoundsInt Bounds;

	public List<TilemapData> TilemapDatas = new List<TilemapData>();

	public Matrix4x4[] Matrices;

	public Tile[] Tiles;

	public Vector3Int GetPos(int index, Vector2 cellSize)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		Vector3Int size = Bounds.size;
		int num = (int)Math.Round((float)size.x / cellSize.x);
		return new Vector3Int(index % num + (int)Math.Round((float)Bounds.x / cellSize.x), index / num + (int)Math.Round((float)Bounds.y / cellSize.y));
	}

	public BoundsInt GetTileBounds(Vector2 cellSize)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		Vector3Int position = Bounds.position;
		Vector3Int val = new Vector3Int((int)Math.Round((float)position.x / cellSize.x), (int)Math.Round((float)position.y / cellSize.y), position.z);
		Vector3Int size = Bounds.size;
		Vector3Int val2 = new Vector3Int((int)Math.Round((float)size.x / cellSize.x), (int)Math.Round((float)size.y / cellSize.y), size.z);
		return new BoundsInt(val, val2);
	}
}

}
