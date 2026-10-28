using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
public class OfflineMiniMapMaskManager : MiniMapMaskManagerBase, IFullScrMiniMapStateListener
{
	private bool mInited;

	private MiniMapCameraController mCameraController;

	private bool mDragControllerRefreshed;

	private MapGroundGridBoolData mGridVisibilityData;

	public void Init(int mapClassId, PlayerOfflineMiniMapDataResponse resp)
	{
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		mVisibleRectMinPos = mMapRootLiteInfo.MapGroundSize;
		mVisibleRectMaxPos = Pos2d.Zero;
		mGridVisibilityData = GridVisibilityCompressUtils.DoMyUnzip(resp.GridVisibilityByte);
		FillBitGrid(mGridVisibilityData, mapClassId, resp.ActiveRegions);
		Vector3Int[] array = new Vector3Int[mMapRootLiteInfo.MapGroundSize.X * mMapRootLiteInfo.MapGroundSize.Y];
		Tile[] array2 = new Tile[mMapRootLiteInfo.MapGroundSize.X * mMapRootLiteInfo.MapGroundSize.Y];
		int num = 0;
		for (int num2 = mMapRootLiteInfo.MapGroundSize.Y - 1; num2 >= 0; num2--)
		{
			MapRootEntityLiteGridInfo[] array3 = mMapRootLiteInfo.PosDataArray[num2];
			for (int num3 = mMapRootLiteInfo.MapGroundSize.X - 1; num3 >= 0; num3--)
			{
				if (array3[num3].Area >= 0)
				{
					Pos2d pos2d = new Pos2d(num3, num2);
					if (GridMaskable(pos2d))
					{
						if (!IsPosLocked(pos2d))
						{
							OnPosUnlocked(pos2d);
						}
						else
						{
							array[num] = MiniMap.GetTilePosByGrid(MiniMap.MapClassId, pos2d);
							array2[num++] = MaskTile;
						}
					}
				}
			}
		}
		SetTiles(array, array2);
		mInited = true;
		RefreshCameraController();
		MaskHandler.Init(MiniMap.MapClassId, mMapRootLiteInfo, IsPosLocked, GridMaskable);
		mRegionDescManager.Init(mGridVisibilityData);
		MaskHandler.ShowMaskedGo();
	}

	protected override bool IsPosLocked(Pos2d pos)
	{
		return !mGridVisibilityData.GetPosValue(pos.X, pos.Y);
	}

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		if (!MapActived)
		{
			mDragControllerRefreshed = false;
			mCameraController = cameraController;
			RefreshCameraController();
			cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Combine(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		}
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Remove(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		mCameraController = null;
	}

	private void RefreshCameraController()
	{
		if (mInited && !((Object)(object)mCameraController == (Object)null) && !mDragControllerRefreshed)
		{
			mDragControllerRefreshed = true;
			RefreshDragController(mCameraController);
		}
	}

	private void FillBitGrid(MapGroundGridBoolData gridVisLitData, int mapClassId, List<int> activeRegions)
	{
		foreach (int activeRegion in activeRegions)
		{
			foreach (Pos2d item in MiniMap.ForEachMapRegionActivedAllGrid(mapClassId, activeRegion))
			{
				gridVisLitData.SetPosValue(item.X, item.Y, value: true);
			}
		}
	}
}

}
