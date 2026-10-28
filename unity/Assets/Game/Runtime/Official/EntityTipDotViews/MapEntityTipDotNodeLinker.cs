// Migrated original map tip-dot application code; see entity tip-dot receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityTipDotViews
{
public class MapEntityTipDotNodeLinker : MonoBehaviour
{
	public WorldStateSwitcher<ETipDotLevelType> TipLevelState;

	[SerializeField]
	private GameObject mContent;

	[SerializeField]
	private WorldMultiLanguageText mDataText;

	private TipDotNodeKey mNodeKey;

	public IProfessionTipDotSystemController Controller { get; set; }

	public IProfessionTipDotSystem TipDotSystem { get; set; }

	private void OnEnable()
	{
		OnSetShow();
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
	}

	private void OnDisable()
	{
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Remove(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
	}

	private void OnSetShow()
	{
		if ((Object)(object)mContent == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else
		{
			mContent.SetActive(TipDotSystem.IsShowTipDot);
		}
	}

	public void RegisterEntity(EcEntityIdentifier entId)
	{
		ECViewEntityData ecEntity = EntityTipDotServices.Require.GetEntity(entId);
		if (ecEntity == null)
		{
			return;
		}
		int classId = EntityTipDotServices.Require.GetContainerClassId(ecEntity);
		EntityTipDotMapInfo mapInfo = EntityTipDotServices.Require.GetMapInfo(classId);
		if (mapInfo == null)
		{
			return;
		}
		string mapEntityReddotGraphName = EntityTipDotServices.Require.GetReddotGraphName(ecEntity, mapInfo.MapGroupId, classId, EntityTipDotServices.Require.GetEntityTag(ecEntity));
		if (string.IsNullOrEmpty(mapEntityReddotGraphName))
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
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
