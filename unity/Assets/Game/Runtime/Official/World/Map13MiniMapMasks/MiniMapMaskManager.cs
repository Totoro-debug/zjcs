using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
public class MiniMapMaskManager : MiniMapMaskManagerBase, IFullScrMiniMapStateListener
{
	protected MiniMapVisibilityAgent mMiniMapVisibilityAgent;

	protected int mViewRange;

	private bool mDirty;

	private List<Pos2d> mOnFrameUnlockPosList = new List<Pos2d>();

	private MiniMapMaskContext MapRootManagerProxy => MiniMap;

	private MiniMapMaskContext MyCharacterContext => MiniMap;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (MapActived)
		{
			mMiniMapVisibilityAgent = MapRootManagerProxy.VisibilityAgent;
			mMiniMapVisibilityAgent.OnPosVisibilityUpdate += OnPosVisibilityUpdate;
			mViewRange = MiniMap.CharacterViewRange;
			Refresh();
			MaskHandler.Init(MiniMap.MapClassId, mMapRootLiteInfo, IsPosLocked, GridMaskable);
			mRegionDescManager.Init(MapRootManagerProxy.CurrentMapClassId, MapRootManagerProxy.VisibilityAgent.Component);
			MaskHandler.ShowMaskedGo();
		}
	}

	protected override void OnDisableAndInited()
	{
		mViewRange = 0;
		if ((Object)(object)mMiniMapVisibilityAgent != (Object)null)
		{
			mMiniMapVisibilityAgent.OnPosVisibilityUpdate -= OnPosVisibilityUpdate;
		}
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		int num = mMapRootLiteInfo.MapGroundSize.X * mMapRootLiteInfo.MapGroundSize.Y;
		List<Vector3Int> list = new List<Vector3Int>(num);
		Tile[] array = new Tile[num];
		int num2 = 0;
		for (int num3 = mMapRootLiteInfo.MapGroundSize.Y - 1; num3 >= 0; num3--)
		{
			MapRootEntityLiteGridInfo[] array2 = mMapRootLiteInfo.PosDataArray[num3];
			for (int num4 = mMapRootLiteInfo.MapGroundSize.X - 1; num4 >= 0; num4--)
			{
				if (array2[num4].Area >= 0)
				{
					Pos2d pos2d = new Pos2d(num4, num3);
					if (GridMaskable(pos2d))
					{
						if (IsPosLocked(pos2d))
						{
							list.Add(MiniMap.GetTilePosByGrid(MiniMap.MapClassId, pos2d));
							array[num2++] = MaskTile;
						}
						else
						{
							OnPosUnlocked(pos2d);
						}
					}
				}
			}
		}
		SetTiles(list.ToArray(), array);
	}

	protected override bool IsPosLocked(Pos2d pos)
	{
		return !mMiniMapVisibilityAgent.Component.GetPosVisibility(pos);
	}

	private void OnPosVisibilityUpdate(Pos2d pos, bool preVi, bool vi)
	{
		if (vi)
		{
			TileUnlock(pos);
		}
	}

	private void TileUnlock(Pos2d pos)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (GridMaskable(pos))
		{
			MaskTilemap.SetTile(MiniMap.GetTilePosByGrid(MiniMap.MapClassId, pos), (TileBase)null);
			OnPosUnlocked(pos);
			mDirty = true;
			mOnFrameUnlockPosList.Add(pos);
		}
	}

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		if (MapActived)
		{
			RefreshDragController(cameraController);
			cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Combine(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		}
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Remove(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
	}

	private void Update()
	{
		if (mDirty)
		{
			mDirty = false;
			MaskHandler.UnlockSomePos(mOnFrameUnlockPosList, MyCharacterContext.CharacterGridPos, mViewRange);
			mOnFrameUnlockPosList.Clear();
		}
	}
}

}
