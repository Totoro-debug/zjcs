// Migrated official partition execution; see Map13 partition source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using Zjcs.Offline.World.NativePartition;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(101)]
public class PartitionLoader : MonoBehaviour
{
	private const int kIdleLastFrame = 900;

	private const int kUnloadIntervalFrame = 5;

	private int mLastUnloadFrame;

	public Transform ExtensionHost;

	public Transform ChunkHost;

	private Action<TileChunkData> mTileChunkLoadedAction;

	private Dictionary<int, Tile[]> mTileBlocks = new Dictionary<int, Tile[]>();

	private Vector3Int[] mPositionArray = new Vector3Int[100];

	private TileBase[] mTileArray = new TileBase[100];

	private Dictionary<string, Tilemap> mTilemapHosts = new Dictionary<string, Tilemap>();

	private readonly Vector2Int[] mVicinities = new Vector2Int[9]
	{
		new Vector2Int(0, 0),
		new Vector2Int(0, 1),
		new Vector2Int(0, -1),
		new Vector2Int(-1, 0),
		new Vector2Int(1, 0),
		new Vector2Int(-1, -1),
		new Vector2Int(-1, 1),
		new Vector2Int(1, 1),
		new Vector2Int(1, -1)
	};

	private List<Vector2Int> mPendingRemoveChunkPos = new List<Vector2Int>();

	private Dictionary<int, int> mTilemapUseCount = new Dictionary<int, int>();

	private bool mIsCameraReady;

	private Camera mCamera;

	private Vector2Int mChunkSize;


	private HashSet<Vector2Int> mVisited = new HashSet<Vector2Int>();

	private int mColumn;

	private int mMinX;

	private int mMaxX;

	private int mMinY;

	private int mMaxY;

	private ChunkData[] mChunkGrid;

	private Dictionary<ushort, PartitionAsset> mId2Assets = new Dictionary<ushort, PartitionAsset>();

	private bool mIgnoreCamera;

	public IPartitionResourceLoader ResourceLoader { get; set; }

	public IPartitionInstanceFactory ServiceProvider { get; set; }

	public Func<bool> IsOtherGameRunning { get; set; }

	public Func<bool> IsCameraReady { get; set; }


	private void Awake()
	{
		Tilemap[] componentsInChildren = ((Component)this).GetComponentsInChildren<Tilemap>(true);
		PartitionElement partitionElement = default;
		foreach (Tilemap val in componentsInChildren)
		{
			mTilemapHosts.Add(((Object)val).name, val);
			if (!((Component)val).TryGetComponent<PartitionElement>(out partitionElement) || !partitionElement.Keep)
			{
				((Component)val).gameObject.SetActive(false);
			}
		}
		Application.lowMemory += ApplicationService_OnLowMemory;
	}

	private void OnDestroy()
	{
		Application.lowMemory -= ApplicationService_OnLowMemory;
	}

	private void LateUpdate()
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if (mIgnoreCamera)
		{
			return;
		}
		if (!mIsCameraReady)
		{
			if (IsCameraReady == null || !IsCameraReady())
			{
				return;
			}
			mIsCameraReady = true;
		}
		if (IsOtherGameRunning != null && !IsOtherGameRunning() && !((Object)(object)mCamera != (Object)(object)Camera.main))
		{
			Vector3 position = ((Component)mCamera).transform.position;
			Vector2Int val = ConvertWorldPos2ChunkPos(position);
			bool didLoad = LoadVicinity(val.x, val.y);
			TryToUnloadIdleChunk(val.x, val.y, didLoad);
		}
	}

	private void ApplicationService_OnLowMemory()
	{
		RemoveChunksOutsideView();
	}

	public void Initialize(int minX, int maxX, int minY, int maxY, List<Vector2> cellSizes, Vector2Int chunkSize)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		mMinX = minX;
		mMinY = minY;
		mMaxX = maxX;
		mMaxY = maxY;
		mColumn = maxX - minX + 1;
		int num = maxY - minY + 1;
		mChunkGrid = new ChunkData[num * mColumn];
		mCamera = Camera.main;
		mChunkSize = chunkSize;
		foreach (Vector2 cellSize in cellSizes)
		{
			int num2 = (int)Math.Round((float)chunkSize.x / cellSize.x);
			int num3 = (int)Math.Round((float)chunkSize.y / cellSize.y);
			Tile[] value = new Tile[num2 * num3];
			mTileBlocks.Add(num2 * num3, value);
		}
		mTileChunkLoadedAction = OnTileChunkLoaded;
	}

	public void SetIgnoreCamera(bool value)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		mIgnoreCamera = value;
		UnityEngine.Debug.LogWarning("SetIgnoreCamera:");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Vector2Int ConvertWorldPos2ChunkPos(Vector3 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		return new Vector2Int(Mathf.FloorToInt(pos.x / (float)mChunkSize.x), Mathf.FloorToInt(pos.y / (float)mChunkSize.y));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int ChunkPos2Index(int chunkPosX, int chunkPosY)
	{
		if (chunkPosX < mMinX || chunkPosX > mMaxX || chunkPosY < mMinY || chunkPosY > mMaxY)
		{
			return -1;
		}
		return (chunkPosY - mMinY) * mColumn + (chunkPosX - mMinX);
	}

	public void AddAsset(Vector2Int chunkPos, ushort id, string res, ChunkAssetType type, ChunkRuntimeState runtimeState)
	{
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		int num = ChunkPos2Index(chunkPos.x, chunkPos.y);
		ChunkData[] array = mChunkGrid;
		int num2 = num;
		ChunkData obj = array[num2] ?? (array[num2] = new ChunkData());
		PartitionAsset partitionAsset = null;
		partitionAsset = ((type != ChunkAssetType.Tile) ? ((PartitionAsset)new PartitionScene(id, res, ChunkHost, ServiceProvider)) : ((PartitionAsset)new PartitionTile(id, res, mTileChunkLoadedAction, ClearTileChunk)));
		partitionAsset.SetRuntimeState(runtimeState);
		obj.Assets.Add(partitionAsset);
		mId2Assets.Add(partitionAsset.Id, partitionAsset);
		if (mVisited.Contains(chunkPos))
		{
			partitionAsset.Load(ResourceLoader);
		}
	}

    // Local lifetime adapter: original Remove releases loaded and pending handles.
    public void ReleaseOfflineAssets()
    {
        foreach (PartitionAsset asset in mId2Assets.Values) asset.Remove();
        mId2Assets.Clear();
        if (mChunkGrid != null)
            foreach (ChunkData chunk in mChunkGrid) chunk?.Assets.Clear();
        mVisited.Clear();
    }

	public void RemoveAsset(Vector2Int chunkPos, ushort id, string res, ChunkAssetType type)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		int num = ChunkPos2Index(chunkPos.x, chunkPos.y);
		ChunkData chunkData = mChunkGrid[num];
		bool flag = false;
		for (int num2 = chunkData.Assets.Count - 1; num2 >= 0; num2--)
		{
			PartitionAsset partitionAsset = chunkData.Assets[num2];
			if (partitionAsset.Type == type && partitionAsset.Id == id)
			{
				partitionAsset.Remove();
				chunkData.Assets.RemoveAt(num2);
				flag = true;
				break;
			}
		}
		mId2Assets.Remove(id);
		if (!flag)
		{
			UnityEngine.Debug.LogError("Failed to remove Asset:");
		}
	}

	public void AddExtensionAsset(List<Vector2Int> chunkPoss, ushort id, string res, ChunkRuntimeState runtimeState)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		PartitionAsset partitionAsset = new PartitionExtension(id, res, ExtensionHost, ServiceProvider);
		partitionAsset.SetRuntimeState(runtimeState);
		int i = 0;
		for (int count = chunkPoss.Count; i < count; i++)
		{
			Vector2Int item = chunkPoss[i];
			int num = ChunkPos2Index(item.x, item.y);
			ChunkData[] array = mChunkGrid;
			int num2 = num;
			(array[num2] ?? (array[num2] = new ChunkData())).Assets.Add(partitionAsset);
			if (mVisited.Contains(item))
			{
				partitionAsset.Load(ResourceLoader);
			}
		}
		mId2Assets.Add(partitionAsset.Id, partitionAsset);
	}

	public void RemoveExtensionAsset(List<Vector2Int> chunkPoss, ushort id, string res)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		int i = 0;
		for (int count = chunkPoss.Count; i < count; i++)
		{
			Vector2Int val = chunkPoss[i];
			ChunkData[] array = mChunkGrid;
			int num = ChunkPos2Index(val.x, val.y);
			ChunkData chunkData = array[num] ?? (array[num] = new ChunkData());
			for (int num2 = chunkData.Assets.Count - 1; num2 >= 0; num2--)
			{
				PartitionAsset partitionAsset = chunkData.Assets[num2];
				if (partitionAsset.Type == ChunkAssetType.Extension && partitionAsset.Id == id)
				{
					partitionAsset.Remove();
					chunkData.Assets.RemoveAt(num2);
					flag = true;
				}
			}
		}
		mId2Assets.Remove(id);
		if (!flag)
		{
			UnityEngine.Debug.LogError("Failed to remove Extension:");
		}
	}

	public void ChangeAssetRuntimeState(ushort id, ChunkRuntimeState runtimeState)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (mId2Assets.TryGetValue(id, out var value))
		{
			value.SetRuntimeState(runtimeState);
			return;
		}
		UnityEngine.Debug.LogError("map: Id2Asset doesn't exist id:");
	}

	private void CheckDuplicateAsset(List<PartitionAsset> assets, ushort idToAdd, string resPath)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		int i = 0;
		for (int count = assets.Count; i < count; i++)
		{
			PartitionAsset partitionAsset = assets[i];
			if (partitionAsset.Id == idToAdd)
			{
				UnityEngine.Debug.LogError("Exist Duplicated Asset Id: type: resPath:, other:");
			}
		}
	}

	public bool HasValidChunkInVicinity(Vector2Int chunkPos)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		int i = 0;
		for (int num = mVicinities.Length; i < num; i++)
		{
			Vector2Int val = mVicinities[i];
			int num2 = ChunkPos2Index(val.x + chunkPos.x, val.y + chunkPos.y);
			if (num2 >= 0 && num2 < mChunkGrid.Length && mChunkGrid[num2] != null)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsPosReady(Vector3 worldPos)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (mVisited.Count == 0)
		{
			return false;
		}
		Vector2Int val = ConvertWorldPos2ChunkPos(worldPos);
		int i = 0;
		for (int num = mVicinities.Length; i < num; i++)
		{
			Vector2Int val2 = mVicinities[i];
			int num2 = ChunkPos2Index(val2.x + val.x, val2.y + val.y);
			if (num2 >= 0 && num2 < mChunkGrid.Length)
			{
				ChunkData chunkData = mChunkGrid[num2];
				if (chunkData != null && !chunkData.IsState(ChunkAssetState.Loaded))
				{
					return false;
				}
			}
		}
		return true;
	}

	public string GetNearbyAbnormalStateInfo(Vector2Int chunkPos)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		StringBuilder stringBuilder = new StringBuilder();
		int i = 0;
		for (int num = mVicinities.Length; i < num; i++)
		{
			Vector2Int val = mVicinities[i];
			int num2 = ChunkPos2Index(val.x + chunkPos.x, val.y + chunkPos.y);
			if (num2 < 0 || num2 >= mChunkGrid.Length)
			{
				continue;
			}
			ChunkData chunkData = mChunkGrid[num2];
			if (chunkData == null || chunkData.IsState(ChunkAssetState.Loaded))
			{
				continue;
			}
			stringBuilder.Append($"*****{val.x + chunkPos.x},{val.y + chunkPos.y}:");
			for (int j = 0; j < chunkData.Assets.Count; j++)
			{
				PartitionAsset partitionAsset = chunkData.Assets[j];
				if (partitionAsset.LoadedState != ChunkAssetState.Loaded)
				{
					stringBuilder.Append($"{partitionAsset.Res}->{partitionAsset.LoadedState}  ");
				}
			}
		}
		return stringBuilder.ToString();
	}

	private void Load()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		Vector2Int lb = new Vector2Int(mMinX, mMinY);
		Vector2Int rt = new Vector2Int(mMaxX, mMaxY);
		VisitFromCenter(0, 0, lb, rt);
	}

	public bool LoadVicinity(int x, int y)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		int count = mVisited.Count;
		Vector2Int[] array = mVicinities;
		for (int i = 0; i < array.Length; i++)
		{
			Vector2Int val = array[i];
			Visit(x + val.x, y + val.y);
		}
		return mVisited.Count > count;
	}

	private void Visit(int x, int y)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (x < mMinX || x > mMaxX || y < mMinY || y > mMaxY)
		{
			return;
		}
		Vector2Int item = new Vector2Int(x, y);
		ChunkData chunkData = mChunkGrid[ChunkPos2Index(x, y)];
		if (chunkData == null)
		{
			return;
		}
		if (!mVisited.Add(item))
		{
			chunkData.Keep();
			return;
		}
		List<PartitionAsset> assets = chunkData.Assets;
		int i = 0;
		for (int count = assets.Count; i < count; i++)
		{
			assets[i].Load(ResourceLoader);
		}
	}

	private void TryToUnloadIdleChunk(int viewChunkX, int viewChunkY, bool didLoad)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		mPendingRemoveChunkPos.Clear();
		bool flag = false;
		int frameCount = Time.frameCount;
		foreach (Vector2Int item in mVisited)
		{
			Vector2Int current = item;
			int num = current.x - viewChunkX;
			int num2 = current.y - viewChunkY;
			bool flag2 = false;
			ChunkData chunkData = mChunkGrid[ChunkPos2Index(current.x, current.y)];
			if (num >= -1 && num <= 1 && num2 >= -1 && num2 <= 1)
			{
				flag2 = true;
			}
			if (flag2)
			{
				chunkData.Keep();
			}
			else
			{
				chunkData.IdleFrameCount++;
			}
			if (!didLoad && !flag && frameCount - mLastUnloadFrame > 5 && chunkData.IdleFrameCount > 900 && chunkData.CanUnload())
			{
				mLastUnloadFrame = frameCount;
				flag = true;
				mPendingRemoveChunkPos.Add(current);
			}
		}
		int i = 0;
		for (int count = mPendingRemoveChunkPos.Count; i < count; i++)
		{
			Vector2Int val = mPendingRemoveChunkPos[i];
			mVisited.Remove(val);
			RemoveChunk(val);
		}
	}

	private void RemoveChunksOutsideView()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		mLastUnloadFrame = Time.frameCount;
		mPendingRemoveChunkPos.Clear();
		Vector3 position = ((Component)mCamera).transform.position;
		Vector2Int val = ConvertWorldPos2ChunkPos(position);
		foreach (Vector2Int item in mVisited)
		{
			Vector2Int current = item;
			int num = current.x - val.x;
			int num2 = current.y - val.y;
			ChunkData chunkData = mChunkGrid[ChunkPos2Index(current.x, current.y)];
			if ((num < -1 || num > 1 || num2 < -1 || num2 > 1) && chunkData.CanUnload())
			{
				mPendingRemoveChunkPos.Add(current);
			}
		}
		int i = 0;
		for (int count = mPendingRemoveChunkPos.Count; i < count; i++)
		{
			Vector2Int val2 = mPendingRemoveChunkPos[i];
			mVisited.Remove(val2);
			RemoveChunk(val2);
		}
	}

	private void RemoveChunk(Vector2Int pos)
	{
		ChunkData obj = mChunkGrid[ChunkPos2Index(pos.x, pos.y)];
		obj.IdleFrameCount = 0;
		List<PartitionAsset> assets = obj.Assets;
		int i = 0;
		for (int count = assets.Count; i < count; i++)
		{
			assets[i].Unload();
		}
	}

	private void OnTileChunkLoaded(TileChunkData tcd)
	{
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)((Component)this).gameObject == (Object)null)
		{
			return;
		}
		if (tcd.IsPartial)
		{
			PopulatePartialTiles(tcd);
			return;
		}
		List<TileChunkData.TilemapData> tilemapDatas = tcd.TilemapDatas;
		int i = 0;
		for (int count = tilemapDatas.Count; i < count; i++)
		{
			TileChunkData.TilemapData tilemapData = tilemapDatas[i];
			TileChunkData.Pair[] tiles = tilemapData.Tiles;
			int num = (int)Math.Round((float)mChunkSize.x / tilemapData.CellSize.x);
			int num2 = (int)Math.Round((float)mChunkSize.y / tilemapData.CellSize.y);
			Tile[] array = mTileBlocks[num * num2];
			Array.Clear(array, 0, array.Length);
			Tilemap val = mTilemapHosts[tilemapData.Name];
			RetainTilemap(val);
			int j = 0;
			for (int num3 = tiles.Length; j < num3; j++)
			{
				TileChunkData.Pair pair = tiles[j];
				array[pair.Index] = tcd.Tiles[pair.TileIndex];
			}
			BoundsInt tileBounds = tcd.GetTileBounds(tilemapData.CellSize);
			TileBase[] array2 = (TileBase[])(object)array;
			val.SetTilesBlock(tileBounds, array2);
			if (!tilemapData.HasOffset)
			{
				continue;
			}
			short[] matrixIndexes = tilemapData.MatrixIndexes;
			int k = 0;
			for (int num4 = tiles.Length; k < num4; k++)
			{
				short num5 = matrixIndexes[k];
				if (num5 >= 0)
				{
					TileChunkData.Pair pair2 = tiles[k];
					val.SetTransformMatrix(tcd.GetPos(pair2.Index, tilemapData.CellSize), tcd.Matrices[num5]);
				}
			}
		}
	}

	private void ClearTileChunk(TileChunkData tcd)
	{
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		if (tcd.IsPartial)
		{
			ClearPartialTiles(tcd);
			return;
		}
		List<TileChunkData.TilemapData> tilemapDatas = tcd.TilemapDatas;
		foreach (KeyValuePair<int, Tile[]> mTileBlock in mTileBlocks)
		{
			Tile[] value = mTileBlock.Value;
			Array.Clear(value, 0, value.Length);
		}
		int i = 0;
		for (int count = tilemapDatas.Count; i < count; i++)
		{
			TileChunkData.TilemapData tilemapData = tilemapDatas[i];
			int num = (int)Math.Round((float)mChunkSize.x / tilemapData.CellSize.x);
			int num2 = (int)Math.Round((float)mChunkSize.y / tilemapData.CellSize.y);
			Tile[] array = mTileBlocks[num * num2];
			Tilemap val = mTilemapHosts[tilemapData.Name];
			ReleaseTilemap(val);
			BoundsInt tileBounds = tcd.GetTileBounds(tilemapData.CellSize);
			TileBase[] array2 = (TileBase[])(object)array;
			val.SetTilesBlock(tileBounds, array2);
		}
	}

	private void PopulatePartialTiles(TileChunkData tcd)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		List<TileChunkData.TilemapData> tilemapDatas = tcd.TilemapDatas;
		Vector3Int val = new Vector3Int(0, 0, 1);
		int i = 0;
		for (int count = tilemapDatas.Count; i < count; i++)
		{
			TileChunkData.TilemapData tilemapData = tilemapDatas[i];
			TileChunkData.Pair[] tiles = tilemapData.Tiles;
			Tilemap val2 = mTilemapHosts[tilemapData.Name];
			RetainTilemap(val2);
			if (mPositionArray.Length < tiles.Length)
			{
				mPositionArray = new Vector3Int[tiles.Length];
				mTileArray = new TileBase[tiles.Length];
			}
			int j = 0;
			for (int num = tiles.Length; j < num; j++)
			{
				TileChunkData.Pair pair = tiles[j];
				mPositionArray[j] = tcd.GetPos(pair.Index, tilemapData.CellSize);
				mTileArray[j] = (TileBase)(object)tcd.Tiles[pair.TileIndex];
			}
			int k = tiles.Length;
			for (int num2 = mPositionArray.Length; k < num2; k++)
			{
				mPositionArray[k] = val;
				mTileArray[k] = null;
			}
			val2.SetTiles(mPositionArray, mTileArray);
			if (!tilemapData.HasOffset)
			{
				continue;
			}
			short[] matrixIndexes = tilemapData.MatrixIndexes;
			int l = 0;
			for (int num3 = tiles.Length; l < num3; l++)
			{
				short num4 = matrixIndexes[l];
				if (num4 >= 0)
				{
					TileChunkData.Pair pair2 = tiles[l];
					val2.SetTransformMatrix(tcd.GetPos(pair2.Index, tilemapData.CellSize), tcd.Matrices[num4]);
				}
			}
		}
	}

	private void ClearPartialTiles(TileChunkData tcd)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		List<TileChunkData.TilemapData> tilemapDatas = tcd.TilemapDatas;
		Vector3Int val = new Vector3Int(0, 0, 1);
		int i = 0;
		for (int count = tilemapDatas.Count; i < count; i++)
		{
			TileChunkData.TilemapData tilemapData = tilemapDatas[i];
			TileChunkData.Pair[] tiles = tilemapData.Tiles;
			int j = 0;
			for (int num = tiles.Length; j < num; j++)
			{
				TileChunkData.Pair pair = tiles[j];
				mPositionArray[j] = tcd.GetPos(pair.Index, tilemapData.CellSize);
				mTileArray[j] = null;
			}
			int k = tiles.Length;
			for (int num2 = mPositionArray.Length; k < num2; k++)
			{
				mPositionArray[k] = val;
				mTileArray[k] = null;
			}
			Tilemap val2 = mTilemapHosts[tilemapData.Name];
			ReleaseTilemap(val2);
			val2.SetTiles(mPositionArray, mTileArray);
		}
	}

	private void RetainTilemap(Tilemap tilemap)
	{
		int instanceID = ((Object)tilemap).GetInstanceID();
		if (mTilemapUseCount.TryGetValue(instanceID, out var value))
		{
			int num = value + 1;
			mTilemapUseCount[instanceID] = num;
			if (num == 1)
			{
				((Component)tilemap).gameObject.SetActive(true);
			}
		}
		else
		{
			mTilemapUseCount[instanceID] = 1;
			((Component)tilemap).gameObject.SetActive(true);
		}
	}

	private void ReleaseTilemap(Tilemap tilemap)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		int instanceID = ((Object)tilemap).GetInstanceID();
		if (mTilemapUseCount.TryGetValue(instanceID, out var value))
		{
			int num = value - 1;
			mTilemapUseCount[instanceID] = num;
			if (num <= 0)
			{
				((Component)tilemap).gameObject.SetActive(false);
			}
			return;
		}
		UnityEngine.Debug.LogError("Release Unexpected,tilemap:");
	}

	private void VisitFromCenter(int centerX, int centerY, Vector2Int lb, Vector2Int rt)
	{
		int x = rt.x;
		int x2 = lb.x;
		int y = lb.y;
		int y2 = rt.y;
		int num = Mathf.Max(new int[4]
		{
			x - centerX,
			centerX - x2,
			y2 - centerY,
			centerY - y
		});
		Visit(centerX, centerY);
		for (int i = 1; i <= num; i++)
		{
			int num2 = Mathf.Max(x2, centerX - i);
			int num3 = Mathf.Min(x, centerX + i);
			int y3 = Mathf.Min(y2, centerY + i);
			int y4 = Mathf.Max(y, centerY - i);
			int num4 = Mathf.Max(y, centerY - i + 1);
			int num5 = Mathf.Min(y2, centerY + i - 1);
			if (centerY + i <= y2)
			{
				for (int j = num2; j <= num3; j++)
				{
					Visit(j, y3);
				}
			}
			if (centerX + i <= x)
			{
				for (int num6 = num5; num6 >= num4; num6--)
				{
					Visit(num3, num6);
				}
			}
			if (centerY - i >= y)
			{
				for (int num7 = num3; num7 >= num2; num7--)
				{
					Visit(num7, y4);
				}
			}
			if (centerX - i >= x2)
			{
				for (int k = num4; k <= num5; k++)
				{
					Visit(num2, k);
				}
			}
		}
	}

	public PartitionLoader()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
