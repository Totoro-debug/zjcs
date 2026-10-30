using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeWater;
using UnityEngine.Tilemaps;

[ExecuteInEditMode]
[RequireComponent(typeof(WaterMaskRenderer))]
public class WaterMaskGenerator : MonoBehaviour
{
	private WaterMaskRenderer mWaterMaskRenderer;

	public Tilemap RefTilemap;

	public Tilemap ExtraTilemap;

	public int ExtraUnderWaterOrder;

	public WaterConfig Config;

	private bool mIsPartitioning;

	public WaterMaskRenderer MaskRenderer
	{
		get
		{
			if (mWaterMaskRenderer == null)
			{
				mWaterMaskRenderer = GetComponent<WaterMaskRenderer>();
			}
			return mWaterMaskRenderer;
		}
	}

	private void Start()
	{
		ToggleTilemap(RefTilemap, value: true);
		ToggleTilemap(ExtraTilemap, value: true);
		GenerateWater();
	}

	public void StartPartition()
	{
		mIsPartitioning = true;
	}

	private Tilemap CreateTilemap(Tilemap refTilemap, string no, bool isMask, out TilemapRenderer renderer)
	{
		string text = (isMask ? "_WaterMask" : "_");
		string n = ((Object)(object)refTilemap).name + text + no;
		Transform transform = ((Component)(object)refTilemap).transform.parent.Find(n);
		if (transform != null)
		{
			DestoryGo(transform.gameObject);
		}
		GameObject gameObject = new GameObject(n);
		if (!mIsPartitioning)
		{
			gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
		}
		if (isMask)
		{
			gameObject.layer = LayerMask.NameToLayer("WaterMask");
		}
		gameObject.transform.SetParent(((Component)(object)refTilemap).transform.parent, worldPositionStays: false);
		Tilemap tilemap = gameObject.AddComponent<Tilemap>();
		TilemapRenderer tilemapRenderer = gameObject.AddComponent<TilemapRenderer>();
		tilemap.tileAnchor = refTilemap.tileAnchor;
		renderer = tilemapRenderer;
		tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
		if (isMask)
		{
			tilemapRenderer.sharedMaterial = Config.WaterMaskMat;
		}
		return tilemap;
	}

	[ContextMenu("重新生成")]
	public void GenerateWater()
	{
		GenerateWaterMaskForTilemap(RefTilemap);
		GenerateWaterMaskForTilemap(ExtraTilemap, ExtraUnderWaterOrder);
	}

	private void GenerateWaterMaskForTilemap(Tilemap tilemap, int underWaterOrder = int.MinValue)
	{
		if ((Object)(object)tilemap == null)
		{
			return;
		}
		Tilemap tilemap2 = null;
		if (Application.isPlaying)
		{
			tilemap2 = tilemap;
		}
		if ((Object)(object)tilemap2 == null)
		{
			return;
		}
		string manualMaskName = GetManualMaskName(tilemap);
		Transform transform = ((Component)(object)tilemap2).transform.parent.Find(manualMaskName);
		Tilemap tilemap3 = null;
		if (transform != null)
		{
			tilemap3 = transform.GetComponent<Tilemap>();
		}
		Dictionary<Sprite, Tile> dictionary = new Dictionary<Sprite, Tile>();
		Dictionary<Vector3Int, Matrix4x4> dictionary2 = new Dictionary<Vector3Int, Matrix4x4>();
		Tilemap tilemap4 = null;
		BoundsInt cellBounds = tilemap2.cellBounds;
		TilemapRenderer component = ((Component)(object)tilemap2).GetComponent<TilemapRenderer>();
		if (underWaterOrder == int.MinValue)
		{
			underWaterOrder = component.sortingOrder;
		}
		WaterConfig config = Config;
		Tile defaultMaskTile = config.DefaultMaskTile;
		List<Vector3Int> list = new List<Vector3Int>(cellBounds.size.x * cellBounds.size.y / 2);
		List<Tile> list2 = new List<Tile>(cellBounds.size.x * cellBounds.size.y / 2);
		TilemapRenderer tilemapRenderer = null;
		MeshRenderer seaRenderer = MaskRenderer.SeaRenderer;
		if (seaRenderer == null)
		{
			return;
		}
		int i = cellBounds.position.x;
		for (int num = cellBounds.position.x + cellBounds.size.x; i < num; i++)
		{
			int j = cellBounds.position.y;
			for (int num2 = cellBounds.position.y + cellBounds.size.y; j < num2; j++)
			{
				Vector3Int vector3Int = new Vector3Int(i, j, 0);
				Tile tile = tilemap2.GetTile(vector3Int) as Tile;
				if (tile == null || ((Object)(object)tilemap3 != null && tilemap3.HasTile(vector3Int)))
				{
					continue;
				}
				Sprite sprite = tile.sprite;
				if (config.FindMask(sprite, out var result, out var special))
				{
					if (special)
					{
						if ((Object)(object)tilemap4 == null)
						{
							tilemap4 = CreateTilemap(tilemap2, "Under", isMask: false, out tilemapRenderer);
							tilemapRenderer.sharedMaterial = Config.SubmersionUnderMat;
							tilemapRenderer.sortingLayerID = seaRenderer.sortingLayerID;
							tilemapRenderer.sortingOrder = underWaterOrder;
						}
						Matrix4x4 transformMatrix = tilemap2.GetTransformMatrix(vector3Int);
						tilemap2.SetTile(vector3Int, null);
						tilemap4.SetTile(vector3Int, tile);
						tilemap4.SetTransformMatrix(vector3Int, transformMatrix);
						dictionary2.Add(vector3Int, transformMatrix);
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = GetMaskTile(result, mIsPartitioning);
						dictionary.Add(result, value);
					}
					list.Add(vector3Int);
					list2.Add(value);
				}
				else
				{
					list.Add(vector3Int);
					list2.Add(defaultMaskTile);
				}
			}
		}
		if (tilemapRenderer != null)
		{
			tilemapRenderer.detectChunkCullingBounds = TilemapRenderer.DetectChunkCullingBounds.Manual;
			tilemapRenderer.chunkCullingBounds = component.chunkCullingBounds;
		}
		if (list.Count > 0)
		{
			Tilemap tilemap5 = CreateTilemap(tilemap2, string.Empty, isMask: true, out var _);
			Vector3Int[] positionArray = list.ToArray();
			TileBase[] tileArray = list2.ToArray();
			tilemap5.SetTiles(positionArray, tileArray);
			foreach (KeyValuePair<Vector3Int, Matrix4x4> item in dictionary2)
			{
				tilemap5.SetTransformMatrix(item.Key, item.Value);
			}
		}
		if ((Object)(object)tilemap4 != null)
		{
			GameObject gameObject = Object.Instantiate(((Component)(object)tilemap4).gameObject, ((Component)(object)tilemap2).transform.parent);
			string text = gameObject.name;
			gameObject.name = text.Replace("Under", "Above").Replace("(Clone)", "");
			TilemapRenderer component2 = gameObject.GetComponent<TilemapRenderer>();
			component2.sharedMaterial = Config.SubmersionAboveMat;
			component2.sortingLayerID = component.sortingLayerID;
			component2.mode = component.mode;
			if (SortingLayer.GetLayerValueFromID(seaRenderer.sortingLayerID) < SortingLayer.GetLayerValueFromID(component.sortingLayerID))
			{
				component2.sortingOrder = component.sortingOrder;
			}
			else
			{
				component2.sortingOrder = seaRenderer.sortingOrder + 1;
			}
		}
	}

	public static Tile GetMaskTile(Sprite sprite, bool forPartition)
	{
		Tile tile = null;
		if (!forPartition)
		{
			tile = ScriptableObject.CreateInstance<Tile>();
			tile.sprite = sprite;
		}
		return tile;
	}

	private static string GetManualMaskName(Tilemap refTilemap)
	{
		return ((Object)(object)refTilemap).name + "_Manual";
	}

	[ContextMenu("关闭功能")]
	public void DisableWater()
	{
		if ((Object)(object)RefTilemap == null)
		{
			return;
		}
		Transform parent = ((Component)(object)RefTilemap).transform.parent;
		for (int num = parent.childCount - 1; num >= 0; num--)
		{
			Transform child = parent.GetChild(num);
			if ((child.gameObject.hideFlags & HideFlags.DontSave) != 0)
			{
				DestoryGo(child.gameObject);
			}
		}
		ToggleTilemap(RefTilemap, value: true);
		ToggleTilemap(ExtraTilemap, value: true);
	}

	private void ToggleTilemap(Tilemap tilemap, bool value)
	{
		if ((Object)(object)tilemap != null)
		{
			TilemapRenderer component = ((Component)(object)tilemap).GetComponent<TilemapRenderer>();
			((Behaviour)(object)tilemap).enabled = value;
			component.enabled = value;
		}
	}

	private void DestoryGo(GameObject go)
	{
		if (Application.isPlaying)
		{
			Object.Destroy(go);
		}
		else
		{
			Object.DestroyImmediate(go);
		}
	}
}
