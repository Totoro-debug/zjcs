using UnityEngine;
using UnityEngine.Tilemaps;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
public abstract class MiniMapMaskManagerBase : MiniMapManagerBase
{
	public Tilemap MaskTilemap;

	public Tile MaskTile;

	public MiniMapMaskHandler MaskHandler;

	[SerializeField]
	protected MiniMapRegionDescManager mRegionDescManager;

	protected MapRootEntityLiteSingleInfo mMapRootLiteInfo;

	protected Pos2d mVisibleRectMinPos;

	protected Pos2d mVisibleRectMaxPos;

	protected override void InitSub()
	{
		base.InitSub();
		MiniMap.TryGetMapLiteData(MiniMap.MapClassId, out mMapRootLiteInfo);
		((Component)MaskTilemap).GetComponent<TilemapRenderer>().detectChunkCullingBounds = (TilemapRenderer.DetectChunkCullingBounds)1;
		mVisibleRectMinPos = new Pos2d(int.MaxValue, int.MaxValue);
		mVisibleRectMaxPos = new Pos2d(int.MinValue, int.MinValue);
	}

	public void SetTiles(Vector3Int[] posArray, Tile[] tiles)
	{
		MaskTilemap.SetTiles(posArray, (TileBase[])(object)tiles);
	}

	protected void OnPosUnlocked(Pos2d pos)
	{
		mVisibleRectMinPos.X = Mathf.Min(mVisibleRectMinPos.X, pos.X);
		mVisibleRectMinPos.Y = Mathf.Min(mVisibleRectMinPos.Y, pos.Y);
		mVisibleRectMaxPos.X = Mathf.Max(mVisibleRectMaxPos.X, pos.X);
		mVisibleRectMaxPos.Y = Mathf.Max(mVisibleRectMaxPos.Y, pos.Y);
	}

	protected abstract bool IsPosLocked(Pos2d pos);

	protected bool GridMaskable(Pos2d pos)
	{
		if (pos.X < 0 || pos.Y < 0 || pos.X >= mMapRootLiteInfo.MapGroundSize.X || pos.Y >= mMapRootLiteInfo.MapGroundSize.Y)
		{
			return false;
		}
		return mMapRootLiteInfo.PosDataArray[pos.Y][pos.X].Area >= 0;
	}

	protected void OnCameraOrthographicSizeUpdate(MiniMapCameraController cameraController)
	{
		RefreshDragController(cameraController);
	}

	protected void RefreshDragController(MiniMapCameraController cameraController)
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		UITouchInput myInput = cameraController.myInput;
		float num = ((float)(mVisibleRectMaxPos.X - mVisibleRectMinPos.X) + 1f) / 2f;
		float num2 = ((float)(mVisibleRectMaxPos.Y - mVisibleRectMinPos.Y) + 1f) / 2f;
		Pos2d grid = new Pos2d((mVisibleRectMaxPos.X + mVisibleRectMinPos.X) / 2, (mVisibleRectMaxPos.Y + mVisibleRectMinPos.Y) / 2);
		float num3 = MiniMap.MiniMapSetting.BoundExtendSize * MiniMapKeepSizeHandler.GetScale(cameraController, 0f, cameraController.GetZoomRate());
		myInput.Data.BorderBounds.center = ((Component)this).transform.position + mMapRootLiteInfo.GridToPos(grid);
		myInput.Data.BorderBounds.extents = new Vector3(num + num3, num2 + num3, 0f);
		float num4 = cameraController.MiniMapCamera.orthographicSize * cameraController.MiniMapCamera.aspect;
		Vector3 extents = myInput.Data.BorderBounds.extents;
		myInput.Data.TargetBounds.extents = new Vector3(Mathf.Min(num4, extents.x), Mathf.Min(num4, extents.y), 0f);
	}
}

}
