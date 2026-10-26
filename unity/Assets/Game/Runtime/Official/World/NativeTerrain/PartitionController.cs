// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;

public class PartitionController : MonoBehaviour
{
	public PartitionLoader Loader;

	public PartitionData PartitionData;

	private int mMapClassId;

	private void Awake()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Loader.Initialize(PartitionData.MinX, PartitionData.MaxX, PartitionData.MinY, PartitionData.MaxY, PartitionData.CellSizes, PartitionData.ChunkSize);
		mMapClassId = Context.MapClassId;
		PopulateDefaultLayer();
		PopulateDynamicLayer();
		Context.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		Context.OnWorldMapProgressUpdate += MainPlayer_OnWorldMapProgressUpdate;
	}

	private void OnDestroy()
	{
		if (Context != null)
		{
			Context.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
			Context.OnWorldMapProgressUpdate -= MainPlayer_OnWorldMapProgressUpdate;
		}
	}

	private void MainPlayer_OnWorldMapProgressUpdate(int mapClassId, List<int> regionProgresses)
	{
		if (mapClassId == mMapClassId)
		{
			int num = Context.CurrentRegion;
			if (Context.GetCurrentMapStatus()[num] == MapRegionStatus.Private && Context.GetMapRegionGoddessId(mapClassId, num) > 0 && Context.WorldMapRegionCleared(mapClassId, num, regionProgresses[num]))
			{
				PartitionData.LayerData layerData = PartitionData.RegionDatas[num].GetLayerData(PartitionLayer.Day);
				AddLayer(layerData, ChunkRuntimeState.Inactive);
			}
		}
	}

	private void MainPlayer_OnMapStatusUpdate(int classId, int region, MapRegionStatus from, MapRegionStatus to)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (to == MapRegionStatus.Public)
		{
			PartitionData.LayerData layerData = PartitionData.RegionDatas[region].GetLayerData(PartitionLayer.Day);
			ChangeLayerState(layerData, ChunkRuntimeState.Active);
			StartCoroutine(DelayRemovePrivateRegion(region, 5f));
		}
	}




	public void PopulateDefaultLayer()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		int i = 0;
		for (int count = PartitionData.TileChunks.Count; i < count; i++)
		{
			PartitionData.Chunk chunk = PartitionData.TileChunks[i];
			Loader.AddAsset(chunk.Pos, chunk.Id, chunk.Res, ChunkAssetType.Tile, ChunkRuntimeState.Active);
		}
		int j = 0;
		for (int count2 = PartitionData.SceneChunks.Count; j < count2; j++)
		{
			PartitionData.Chunk chunk2 = PartitionData.SceneChunks[j];
			Loader.AddAsset(chunk2.Pos, chunk2.Id, chunk2.Res, ChunkAssetType.Scene, ChunkRuntimeState.Active);
		}
		int k = 0;
		for (int count3 = PartitionData.Extensions.Count; k < count3; k++)
		{
			PartitionData.Extension extension = PartitionData.Extensions[k];
			Loader.AddExtensionAsset(extension.Chunks, extension.Id, extension.Res, ChunkRuntimeState.Active);
		}
	}

	public void PopulateDynamicLayer()
	{
		List<MapRegionStatus> currentMapStatus = Context.GetCurrentMapStatus();
		int i = 0;
		for (int count = currentMapStatus.Count; i < count; i++)
		{
			MapRegionStatus mapRegionStatus = currentMapStatus[i];
			PartitionData.RegionData regionData = PartitionData.RegionDatas[i];
			PartitionData.LayerData layerData = regionData.GetLayerData(PartitionLayer.Day);
			if (mapRegionStatus == MapRegionStatus.Private)
			{
				PartitionData.LayerData layerData2 = regionData.GetLayerData(PartitionLayer.Night);
				AddLayer(layerData2, ChunkRuntimeState.Active);
				if (Context.GetMapRegionGoddessId(mMapClassId, i) <= 0)
				{
					AddLayer(layerData, ChunkRuntimeState.Inactive);
					continue;
				}
				int worldMapRegionProgress = Context.GetWorldMapRegionProgress(mMapClassId, i);
				if (Context.WorldMapRegionCleared(mMapClassId, i, worldMapRegionProgress))
				{
					AddLayer(layerData, ChunkRuntimeState.Inactive);
				}
			}
			else
			{
				AddLayer(layerData, ChunkRuntimeState.Active);
			}
		}
	}

	public void ChangeLayerState(PartitionData.LayerData data, ChunkRuntimeState runtimeState)
	{
		if (data == null)
		{
			return;
		}
		if (data.Tiles != null)
		{
			int i = 0;
			for (int count = data.Tiles.Count; i < count; i++)
			{
				PartitionData.Chunk chunk = data.Tiles[i];
				Loader.ChangeAssetRuntimeState(chunk.Id, runtimeState);
			}
		}
		if (data.Scenes != null)
		{
			int j = 0;
			for (int count2 = data.Scenes.Count; j < count2; j++)
			{
				PartitionData.Chunk chunk2 = data.Scenes[j];
				Loader.ChangeAssetRuntimeState(chunk2.Id, runtimeState);
			}
		}
		if (data.Extensions != null)
		{
			int k = 0;
			for (int count3 = data.Extensions.Count; k < count3; k++)
			{
				PartitionData.Extension extension = data.Extensions[k];
				Loader.ChangeAssetRuntimeState(extension.Id, runtimeState);
			}
		}
	}

	public void AddLayer(PartitionData.LayerData data, ChunkRuntimeState runtimeState)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		if (data == null)
		{
			return;
		}
		if (data.Tiles != null)
		{
			int i = 0;
			for (int count = data.Tiles.Count; i < count; i++)
			{
				PartitionData.Chunk chunk = data.Tiles[i];
				Loader.AddAsset(chunk.Pos, chunk.Id, chunk.Res, ChunkAssetType.Tile, runtimeState);
			}
		}
		if (data.Scenes != null)
		{
			int j = 0;
			for (int count2 = data.Scenes.Count; j < count2; j++)
			{
				PartitionData.Chunk chunk2 = data.Scenes[j];
				Loader.AddAsset(chunk2.Pos, chunk2.Id, chunk2.Res, ChunkAssetType.Scene, runtimeState);
			}
		}
		if (data.Extensions != null)
		{
			int k = 0;
			for (int count3 = data.Extensions.Count; k < count3; k++)
			{
				PartitionData.Extension extension = data.Extensions[k];
				Loader.AddExtensionAsset(extension.Chunks, extension.Id, extension.Res, runtimeState);
			}
		}
	}

	public void RemoveLayer(PartitionData.LayerData data)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (data == null)
		{
			return;
		}
		if (data.Tiles != null)
		{
			int i = 0;
			for (int count = data.Tiles.Count; i < count; i++)
			{
				PartitionData.Chunk chunk = data.Tiles[i];
				Loader.RemoveAsset(chunk.Pos, chunk.Id, chunk.Res, ChunkAssetType.Tile);
			}
		}
		if (data.Scenes != null)
		{
			int j = 0;
			for (int count2 = data.Scenes.Count; j < count2; j++)
			{
				PartitionData.Chunk chunk2 = data.Scenes[j];
				Loader.RemoveAsset(chunk2.Pos, chunk2.Id, chunk2.Res, ChunkAssetType.Scene);
			}
		}
		if (data.Extensions != null)
		{
			int k = 0;
			for (int count3 = data.Extensions.Count; k < count3; k++)
			{
				PartitionData.Extension extension = data.Extensions[k];
				Loader.RemoveExtensionAsset(extension.Chunks, extension.Id, extension.Res);
			}
		}
	}



	public void Reserve(Vector3 worldPosition)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		Vector2Int val = Loader.ConvertWorldPos2ChunkPos(worldPosition);
		Loader.LoadVicinity(val.x, val.y);
	}

	public void TogglePartitionGameObjects(bool value)
	{
		((Component)Loader.ChunkHost).gameObject.SetActive(value);
		((Component)Loader.ExtensionHost).gameObject.SetActive(value);
	}

    public IPartitionWorldContext<MapRegionStatus> Context { get; set; }

    private IEnumerator DelayRemovePrivateRegion(int region, float delayTime)
    {
        yield return new WaitForSecondsRealtime(delayTime);
        if (gameObject != null)
        {
            PartitionData.RegionData regionData = PartitionData.RegionDatas[region];
            RemoveLayer(regionData.GetLayerData(PartitionLayer.Night));
        }
    }

    public IEnumerator LoadByPlayerPos()
    {
        yield return LoadInstantly(Context.PlayerWorldPosition);
    }

    public IEnumerator LoadInstantly(Vector3 worldPos)
    {
        Vector2Int chunkPos = Loader.ConvertWorldPos2ChunkPos(worldPos);
        if (!Loader.HasValidChunkInVicinity(chunkPos))
        {
            Debug.Log("Partition LoadInstantly doesn't HasValidChunkInVicinity");
            yield break;
        }
        Loader.LoadVicinity(chunkPos.x, chunkPos.y);
        int maxTryTime = 120;
        int tryCount = 0;
        while (++tryCount < maxTryTime && !Loader.IsPosReady(worldPos)) yield return null;
        if (tryCount >= maxTryTime)
            Debug.LogError($"Partition LoadInstantly Exception, mMapClassId:{mMapClassId}, chunkPos:{chunkPos}, ErrorInfo:{Loader.GetNearbyAbnormalStateInfo(chunkPos)}");
        else Debug.Log($"Partition LoadInstantly:{tryCount}");
    }
}

}
