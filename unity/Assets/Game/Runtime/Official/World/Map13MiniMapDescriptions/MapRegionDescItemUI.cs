using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.Map13GridVisibility;
using MapRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
public class MapRegionDescItemUI : MonoBehaviour
{
	[Header("区域名字")]
	[SerializeField]
	private List<MultiLanguageText> mRegionNameTexts;

	[Header("区域探索度")]
	[SerializeField]
	private List<MultiLanguageText> mProgressTexts;

	[Header("区域等级")]
	[SerializeField]
	public MultiLanguageText mRegionLevelText;

	[Header("区域推荐战力")]
	[SerializeField]
	private MultiLanguageText mRegionCombatRatingText;

	[Header("区域强度表现")]
	[SerializeField]
	private MapRegionStrengthDisplayUI mRegionStrengthDisplayUI;

	[Header("区域名以外显示内容的节点")]
	[SerializeField]
	private GameObject mExtendSlot;

	[Header("区域强调类物件的挂载父节点，不缩放")]
	[SerializeField]
	private Transform mHighlightSlotWithoutScale;

	private int mMapClassId;

	private int mRegion = -1;

	private MiniMapRegionInfo mRegionCfg;

	private bool mIsMember;

	private MiniMapDescriptionContext mMinimap;

	private MiniMapDescriptionBag mMiniMapDescriptionBag;

	private Transform mLineTransform;

	private Transform mHighlightTransform;

	public IMap13LabelServices Services { get; set; }
	private MiniMapDescriptionContext ResourceLoader => mMinimap;

	public void Init(int mapClassId, int region, MiniMapDescriptionContext minimap, List<Pos2d> regionPosList)
	{
		mMapClassId = mapClassId;
		mRegion = region;
		mMinimap = minimap;
		mRegionCfg = minimap.GetRegion(mMapClassId, mRegion);
		mRegionStrengthDisplayUI.Init(mRegionCfg.LabelInfo);
		mIsMember = Services.IsTeamMember;
		Refresh(mRegionCfg, regionPosList);
		RefreshProgressText();
		RefreshExtendShow();
		if ((Object)(object)mMinimap != (Object)null)
		{
			_ = mMinimap.ShowFullScreenAction + new Action(Minimap_OnShowFullScreen);
			_ = mMinimap.HideFullScreenAction + new Action(Minimap_OnHideFullScreen);
			RefreshHighlightMask();
		}
	}

	private void OnEnable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnWorldMapProgressUpdate += MyMap_OnProgressUpdate;
			Services.Player.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		}
		RefreshProgressText();
	}

	private void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
			Services.Player.OnWorldMapProgressUpdate -= MyMap_OnProgressUpdate;
		}
	}

	private void OnDestroy()
	{
		mMiniMapDescriptionBag.Dispose();
		if ((Object)(object)mMinimap != (Object)null)
		{
			_ = mMinimap.HideFullScreenAction - new Action(Minimap_OnHideFullScreen);
			_ = mMinimap.ShowFullScreenAction - new Action(Minimap_OnShowFullScreen);
		}
		mMinimap = null;
	}

	private void MyMap_OnProgressUpdate(int mapClassId, List<int> regionProgresses)
	{
		if (mapClassId == mMapClassId)
		{
			RefreshProgressText();
		}
	}

	private void MainPlayer_OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preStatus, MapRegionStatus status)
	{
		if (mMapClassId == mapClassId && mRegionCfg != null && mRegionCfg.Region == region)
		{
			RefreshExtendShow();
		}
	}

	private void Minimap_OnShowFullScreen()
	{
		RefreshHighlightMask();
	}

	private void Minimap_OnHideFullScreen()
	{
		RefreshHighlightMask();
	}

	private void Refresh(MiniMapRegionInfo regionInfo, List<Pos2d> regionPosList)
	{
		if ((Object)(object)mMinimap != (Object)null)
		{
			RefreshDisplayByConfig(regionInfo, regionPosList);
		}
		else
		{
			string regionNameKey = MiniMapDescriptionContext.GetRegionNameKey(regionInfo.ClassId, regionInfo.Region);
			foreach (MultiLanguageText mRegionNameText in mRegionNameTexts)
			{
				mRegionNameText.SetKey(regionNameKey);
			}
		}
		if ((Object)(object)mRegionLevelText != (Object)null)
		{
			mRegionLevelText.Format(regionInfo.Level);
		}
		if ((Object)(object)mRegionCombatRatingText != (Object)null)
		{
			mRegionCombatRatingText.Format(CultureNumberArg.Compact(regionInfo.CombatRating));
		}
	}

	private void RefreshExtendShow()
	{
		if (!((Object)(object)mExtendSlot == (Object)null))
		{
			bool flag = false;
			if (!mIsMember)
			{
				flag = Services.Player.GetMapRegionStatus(mRegionCfg.ClassId, mRegionCfg.Region) == MapRegionStatus.Private;
			}
			if (flag != mExtendSlot.activeSelf)
			{
				mExtendSlot.SetActive(flag);
			}
		}
	}

	private void RefreshProgressText()
	{
		if (mProgressTexts == null || mProgressTexts.Count <= 0)
		{
			return;
		}
		int worldMapRegionProgress = Services.Player.GetWorldMapRegionProgress(mMapClassId, mRegion);
		int num = Services.Data.CalcWorldMapRegionProgress(mMapClassId, mRegion, worldMapRegionProgress);
		if (Services.Player.RegionIsActived(mMapClassId, mRegion))
		{
			num = 1;
		}
		if (((float)num / 10000f) >= 1f)
		{
			Services.Player.OnWorldMapProgressUpdate -= MyMap_OnProgressUpdate;
		}
		string text = (num / 100).ToString();
		foreach (MultiLanguageText mProgressText in mProgressTexts)
		{
			mProgressText.Format(text);
		}
	}

	private void RefreshDisplayByConfig(MiniMapRegionInfo regionInfo, List<Pos2d> regionPosList)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		MiniMapRegionDisplayInfo minimapRegionDisplayInfo = mMinimap.GetRegionDisplay(regionInfo.ClassId, regionInfo.Region);
		if (minimapRegionDisplayInfo != null)
		{
			((Component)this).transform.localPosition = (Vector3)new Vector2(minimapRegionDisplayInfo.CenterOffsetX, minimapRegionDisplayInfo.CenterOffsetY);
		}
		RefreshName(regionInfo, minimapRegionDisplayInfo);
		if (regionPosList != null && regionPosList.Count > 0 && mMinimap.TryGetMapLiteData(mMapClassId, out var liteInfo))
		{
			mMinimap.QueryCommonGridGround(mMapClassId);
			RefreshLine(regionInfo, minimapRegionDisplayInfo, regionPosList, liteInfo.MapGroundOriginPos);
			RefreshMask(regionInfo, minimapRegionDisplayInfo, regionPosList, liteInfo.MapGroundOriginPos);
		}
		void RefreshLine(MiniMapRegionInfo mapRegionInfo, MiniMapRegionDisplayInfo displayInfo, List<Pos2d> posList, Vector3 originPos)
		{
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)mLineTransform != (Object)null) && !string.IsNullOrEmpty(displayInfo?.LinePrefabPath))
			{
				IMiniMapDescriptionAssetLease<GameObject> assetOperationHandle = ResourceLoader.LoadPrefab(displayInfo.LinePrefabPath);
				mLineTransform = mMinimap.InstantiateDisplay(assetOperationHandle.Result, mHighlightSlotWithoutScale).transform;
				RefreshLineRenderer(((Component)mLineTransform).GetComponent<LineRendererDashedLine>(), posList, (Vector2)(-((Component)this).transform.parent.localPosition), originPos);
				MiniMapDescriptionDisposable.AddTo<IMiniMapDescriptionAssetLease<GameObject>>(assetOperationHandle, ref mMiniMapDescriptionBag);
			}
		}
		static void RefreshLineRenderer(LineRendererDashedLine lineRenderer, List<Pos2d> posList, Vector2 offset, Vector3 originPos)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			List<Vector2> gridsBorderPoints = PrivateRegionUtility.GetGridsBorderPoints(posList);
			offset.x += originPos.x;
			offset.y += originPos.y;
			gridsBorderPoints.Add(gridsBorderPoints[0]);
			int count = gridsBorderPoints.Count;
			lineRenderer.SetCount(count);
			Vector3[] array = new Vector3[count];
			for (int i = 0; i < count; i++)
			{
				Vector2 val = gridsBorderPoints[i];
				array[i] = new Vector3(val.x + offset.x, val.y + offset.y, 0f);
			}
			lineRenderer.SetPoints(array);
		}
		void RefreshMask(MiniMapRegionInfo mapRegionInfo, MiniMapRegionDisplayInfo displayInfo, List<Pos2d> list, Vector3 originPos)
		{
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)mHighlightTransform != (Object)null) && !string.IsNullOrEmpty(displayInfo?.HighlightPrefabPath))
			{
				IMiniMapDescriptionAssetLease<GameObject> assetOperationHandle = ResourceLoader.LoadPrefab(displayInfo.HighlightPrefabPath);
				mHighlightTransform = mMinimap.InstantiateDisplay(assetOperationHandle.Result, mHighlightSlotWithoutScale).transform;
				mHighlightTransform.localPosition = (Vector3)new Vector2(displayInfo.CenterOffsetX, displayInfo.CenterOffsetY);
				mHighlightTransform.localScale = new Vector3(displayInfo.HighlightScaleX, displayInfo.HighlightScaleY, 1f);
				MiniMapDescriptionDisposable.AddTo<IMiniMapDescriptionAssetLease<GameObject>>(assetOperationHandle, ref mMiniMapDescriptionBag);
			}
		}
		void RefreshName(MiniMapRegionInfo mapRegionInfo, MiniMapRegionDisplayInfo displayInfo)
		{
			string regionNameKey = MiniMapDescriptionContext.GetRegionNameKey(mapRegionInfo.ClassId, mapRegionInfo.Region);
			if (string.IsNullOrEmpty(displayInfo?.NameFormat))
			{
				foreach (MultiLanguageText mRegionNameText in mRegionNameTexts)
				{
					mRegionNameText.SetKey(regionNameKey);
				}
				return;
			}
			CultureTextArg cultureTextArg = new CultureTextArg(regionNameKey);
			foreach (MultiLanguageText mRegionNameText2 in mRegionNameTexts)
			{
				mRegionNameText2.SetKey(displayInfo.NameFormat, cultureTextArg);
			}
		}
	}

	private void RefreshHighlightMask()
	{
		if (!((Object)(object)mHighlightTransform == (Object)null))
		{
			((Component)mHighlightTransform).gameObject.SetActive((Object)(object)mMinimap != (Object)null && mMinimap.IsFullScreen);
		}
	}
}

}
