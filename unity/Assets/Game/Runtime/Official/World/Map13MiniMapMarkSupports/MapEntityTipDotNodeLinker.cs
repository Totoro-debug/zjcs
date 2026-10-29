using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MapEntityTipDotNodeLinker : MonoBehaviour
{
	public StateSwitcher<ETipDotLevelType> TipLevelState;

	[SerializeField]
	private GameObject mContent;

	[SerializeField]
	private MultiLanguageText mDataText;

	private TipDotNodeKey mNodeKey;

	public IMiniMapTipDotController Controller { get; set; }

	public IMiniMapTipDotSystem TipDotSystem { get; set; }
	public IMiniMapMarkSupportContext Context { get; set; }
	public MiniMapManagedContext MiniMap { get; set; }

	private void OnEnable()
	{
		OnSetShow();
		IMiniMapTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
	}

	private void OnDisable()
	{
		IMiniMapTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Remove(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
	}

	private void OnSetShow()
	{
		if ((Object)(object)mContent == (Object)null)
		{
			Debug.LogWarning("MapEntityTipDotNodeLinker未配置mContent字段, 请检查！Path: " + MiniMapMarkSupportRules.FullName(gameObject));
		}
		else
		{
			mContent.SetActive(TipDotSystem.IsShowTipDot);
		}
	}

	public void RegisterEntity(EcEntityIdentifier entId)
	{
		IMiniMapSpawnEntity ecEntity = Context.HasMapContainer ? MiniMap.ManagedQueries.GetEntity(entId) : null;
		if (ecEntity == null)
		{
			return;
		}
		int classId = ecEntity.ContainerClassId;
		MiniMapMarkMapInfo mapInfo = MiniMap.MarkDataQueries.GetMapInfo(classId);
		if (mapInfo == null)
		{
			return;
		}
		string mapEntityReddotGraphName = MiniMapMarkSupportRules.GetMapEntityReddotGraphName(MiniMap, Context, ecEntity, mapInfo.MapGroupId, classId, ecEntity.GetEntityTag());
		if (string.IsNullOrEmpty(mapEntityReddotGraphName))
		{
			Debug.LogError("地图[" + classId + "]上的物件[" + ecEntity.ClassId + "]并没有配红点图, 物件位置：" + ecEntity.GridTransform.Pos);
		}
		else
		{
			mNodeKey = new TipDotNodeKey(mapEntityReddotGraphName, "0", new MapEntityNodeInstanceInfo(ecEntity));
			Controller.LinkNodeStateChange(mNodeKey, ((Component)this).gameObject, OnNodeStateChange);
		}
	}

	public void UnregisterEntity()
	{
		Controller.UnlinkNodeStateChange(mNodeKey, ((Component)this).gameObject, OnNodeStateChange);
		TipLevelState.SetState(ETipDotLevelType.None);
		mNodeKey = default;
	}

	private void OnNodeStateChange(TipDotStateInfo info)
	{
		TipLevelState.SetState(info.TipLevel);
		if ((Object)(object)mDataText != (Object)null)
		{
			string key = TryHandleOutMaxNum(info.Data);
			mDataText.SetKey(key);
		}
	}

	private string TryHandleOutMaxNum(string data)
	{
		string text = data;
		if (int.TryParse(text, out var result) && result > 99)
		{
			text = "99+";
		}
		return text;
	}
}

}
