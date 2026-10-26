// Migrated from selected official terrain code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class PartitionData : ScriptableObject
{
	[Serializable]
	public class Extension
	{
		public List<Vector2Int> Chunks;

		public string Res;

		public ushort Id;
	}

	[Serializable]
	public class Chunk
	{
		public Vector2Int Pos;

		public string Res;

		public ushort Id;
	}

	[Serializable]
	public class RegionData
	{
		public LayerData[] LayerDatas;

		private void Init()
		{
			int num = 2;
			LayerDatas = new LayerData[num];
			for (int i = 0; i < num; i++)
			{
				LayerDatas[i] = new LayerData();
			}
		}

		public void RecordTileChunk(PartitionLayer layer, Chunk chunk)
		{
			if (LayerDatas == null || LayerDatas.Length == 0)
			{
				Init();
			}
			LayerData layerData2;
			LayerData layerData = (layerData2 = LayerDatas[(int)(layer - 1)]);
			if (layerData2.Tiles == null)
			{
				layerData2.Tiles = new List<Chunk>();
			}
			layerData.Tiles.Add(chunk);
		}

		public void RecordSceneChunk(PartitionLayer layer, Chunk chunk)
		{
			if (LayerDatas == null || LayerDatas.Length == 0)
			{
				Init();
			}
			LayerData layerData2;
			LayerData layerData = (layerData2 = LayerDatas[(int)(layer - 1)]);
			if (layerData2.Scenes == null)
			{
				layerData2.Scenes = new List<Chunk>();
			}
			layerData.Scenes.Add(chunk);
		}

		public void RecordExtension(PartitionLayer layer, Extension extension)
		{
			if (LayerDatas == null || LayerDatas.Length == 0)
			{
				Init();
			}
			LayerData layerData2;
			LayerData layerData = (layerData2 = LayerDatas[(int)(layer - 1)]);
			if (layerData2.Extensions == null)
			{
				layerData2.Extensions = new List<Extension>();
			}
			layerData.Extensions.Add(extension);
		}

		public LayerData GetLayerData(PartitionLayer layer)
		{
			if (LayerDatas == null || LayerDatas.Length == 0)
			{
				return null;
			}
			return LayerDatas[(int)(layer - 1)];
		}
	}

	[Serializable]
	public class LayerData
	{
		public List<Chunk> Tiles;

		public List<Chunk> Scenes;

		public List<Extension> Extensions;
	}

	public long SourceModifyTime;

	public long LastGenerateTime;

	public string GroundName;

	public List<Chunk> TileChunks = new List<Chunk>();

	public List<Chunk> SceneChunks = new List<Chunk>();

	public List<Extension> Extensions = new List<Extension>();

	public Vector2Int ChunkSize;

	public List<Vector2> CellSizes = new List<Vector2>();

	public int MinX = int.MaxValue;

	public int MaxX = int.MinValue;

	public int MinY = int.MaxValue;

	public int MaxY = int.MinValue;

	public RegionData[] RegionDatas;

	public void Init(int regionCount)
	{
		RegionDatas = new RegionData[regionCount];
		for (int i = 0; i < regionCount; i++)
		{
			RegionDatas[i] = new RegionData();
		}
	}

	public void RecordTileChunk(Vector2Int chunkPos, ushort id, string resPath, int region, PartitionLayer layer)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Chunk chunk = new Chunk
		{
			Res = resPath,
			Id = id,
			Pos = chunkPos
		};
		if (layer == PartitionLayer.Default)
		{
			TileChunks.Add(chunk);
		}
		else
		{
			RegionDatas[region].RecordTileChunk(layer, chunk);
		}
		MinX = Mathf.Min(MinX, chunkPos.x);
		MaxX = Mathf.Max(MaxX, chunkPos.x);
		MinY = Mathf.Min(MinY, chunkPos.y);
		MaxY = Mathf.Max(MaxY, chunkPos.y);
	}

	public void ClearTileChunkData()
	{
		TileChunks.Clear();
		CellSizes.Clear();
	}

	public void RecordSceneChunk(Vector2Int chunkPos, ushort id, string resPath, int region, PartitionLayer layer)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (layer != PartitionLayer.Default && (region < 0 || region >= RegionDatas.Length))
		{
			UnityEngine.Debug.LogError($"Scene Chunk is illegal, region:{region} res:{resPath} chunkPos:{chunkPos}");
			return;
		}
		Chunk chunk = new Chunk
		{
			Res = resPath,
			Id = id,
			Pos = chunkPos
		};
		if (layer == PartitionLayer.Default)
		{
			SceneChunks.Add(chunk);
		}
		else
		{
			RegionDatas[region].RecordSceneChunk(layer, chunk);
		}
		MinX = Mathf.Min(MinX, chunkPos.x);
		MaxX = Mathf.Max(MaxX, chunkPos.x);
		MinY = Mathf.Min(MinY, chunkPos.y);
		MaxY = Mathf.Max(MaxY, chunkPos.y);
	}

	public void ClearSceneChunkData()
	{
		SceneChunks.Clear();
	}

	public void AddCellSize(Vector2 cellSize)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (!CellSizes.Contains(cellSize))
		{
			CellSizes.Add(cellSize);
		}
	}

	public void RecordExtension(List<Vector2Int> chunks, ushort id, string resPath, PartitionLayer layer, int region)
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		if (layer != PartitionLayer.Default && (region < 0 || region >= RegionDatas.Length))
		{
			UnityEngine.Debug.LogError($"Extension Chunk is illegal, region:{region} res:{resPath}");
			return;
		}
		Extension extension = new Extension
		{
			Res = resPath,
			Chunks = new List<Vector2Int>(chunks),
			Id = id
		};
		for (int num = extension.Chunks.Count - 1; num >= 0; num--)
		{
			Vector2Int val = extension.Chunks[num];
			if (val.x > MaxX || val.x < MinX || val.y < MinY || val.y > MaxY)
			{
				extension.Chunks.RemoveAt(num);
			}
		}
		if (layer == PartitionLayer.Default)
		{
			Extensions.Add(extension);
		}
		else
		{
			RegionDatas[region].RecordExtension(layer, extension);
		}
	}

	public void ClearExtension()
	{
		Extensions.Clear();
	}

	public Vector2Int ConvertWorldPos2ChunkPos(Vector3 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		return new Vector2Int(Mathf.FloorToInt(pos.x / (float)ChunkSize.x), Mathf.FloorToInt(pos.y / (float)ChunkSize.y));
	}

	public bool IsOutdated()
	{
		return LastGenerateTime < SourceModifyTime;
	}
}

}
