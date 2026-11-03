using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniMapItemDirectionGuider : MiniDirectionGuiderBase
{
	[Header("当获得该物品类型时，资源指引关闭")]
	public List<MiniMapDirectionItemType> TriggerItemTypes;

	public Transform IconSlot;

	private MiniMapDirectionItemData mMapEntityGuiderItemData;

	private MiniMapDirectionItemInfo mItemInfo;

	private int mIndex;

	private HashSet<int> mRefreshGroupSet = new HashSet<int>();

	private AreaMiniMapResourcePointData mPointData;



	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Context.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		Show();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if (Context.HasMainPlayer)
		{
			Context.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		ResetView();
	}

	private void MainPlayer_OnItemCountUpdate(MiniMapDirectionPlayerItemData itemData, long preCount, object sourceData)
	{
		if (mItemInfo != null && mIsMapPosInit && TriggerItemTypes.Contains(itemData.Info.ItemType) && (itemData.Info.ClassId == mItemInfo.ClassId || itemData.Info.ClassId == mItemInfo.AgioItemId))
		{
			Hide();
		}
	}

	public void Show()
	{
		if (Context.TryGetItemGuiderData(out var obj))
		{
			InitMapSourceGuider(obj);
		}
	}

	public void Hide()
	{
		mIsMapPosInit = false;
		SetGuiderBtnActive(value: false);
		foreach (int item in mRefreshGroupSet)
		{
			mResourcePointManager.CancelSelect(item, guide: true);
		}
	}

	private void InitMapSourceGuider(MiniMapDirectionData mapEntityGuiderData)
	{
		mMapEntityGuiderItemData = mapEntityGuiderData as MiniMapDirectionItemData;
		mItemInfo = Context.GetItemInfo(mMapEntityGuiderItemData.ItemId);
		mResourcePointManager.GetRefreshGroupSetByItemId(mMapEntityGuiderItemData.ItemId, mRefreshGroupSet);
		if (mRefreshGroupSet.Count <= 0)
		{
			Debug.LogWarning("MiniMapItemDirectionGuider InitMapSourceGuider error, itemId[" + mMapEntityGuiderItemData.ItemId + "] can not find any RefreshGroup");
		}
		Context.DestroyChildren(IconSlot);
		Context.CreateItemIconWithoutText(mMapEntityGuiderItemData.ItemId, IconSlot);
		mIndex = mMapEntityGuiderItemData.Index;
		if (mapEntityGuiderData.AreaList.Count != 0)
		{
			foreach (int item in mRefreshGroupSet)
			{
				mResourcePointManager.Select(item, guide: true, mItemInfo.Icon);
			}
			int area = mapEntityGuiderData.AreaList[mIndex];
			InitEntityPos(area);
		}
		else
		{
			Hide();
		}
	}

	private void InitEntityPos(int area)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (mRefreshGroupSet.Count > 0)
		{
			mPointData = mResourcePointManager.GetSelectResourcePointDatas(mRefreshGroupSet.First(), area);
			if (mPointData != null && mPointData.Datas.Count != 0)
			{
				SetPosition(mMyGridGround.GridToPos(mPointData.Datas[0].PointPos));
			}
			else
			{
				Pos2d centerPos = DirectionGuiderUtils.GetCenterPos(Context, area, mMapEntityGuiderItemData.MapClassId, mMapEntityGuiderItemData.ItemId);
				SetPosition(mMyGridGround.GridToPos(centerPos));
			}
			mIsMapPosInit = true;
			UpdateDirectionGuide(mPosition, mIsMapPosInit);
		}
	}

	private void ResetView()
	{
		if (mPointData != null)
		{
			mPointData = null;
		}
	}

	private void SetPosition(Vector3 pos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		mPosition = pos + mMiniMapTransform.position;
	}
}
}
