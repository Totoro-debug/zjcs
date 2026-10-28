// Migrated original map tip-dot application code; see entity tip-dot receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using UnityEngine;

namespace Zjcs.Official.EntityTipDotViews
{
public class ECTipdotLinkerView : ECViewBase
{
	[SerializeField]
	private MapEntityTipDotNodeLinker mTipdotLinker;

	[SerializeField]
	private AssetGuidProxy<GameObject> mTipdotLinkerPrefabGuidProxy;

	[SerializeField]
	private Transform mLoadedPrefabSlot;

	private MapEntityTipDotNodeLinker mLoadedPrefab;

	private EntityTipDotPrefabLease mLoadHandler;

	public IEntityTipDotPrefabLoader ResourceLoader { get; set; }

	protected override void Load()
	{
		base.Load();
		if (!EntityTipDotServices.Require.IsLeader || !EntityTipDotOwnership.EntityShowTipForMe(GetComponent<ECEntityTipDotSourceData>()))
		{
			return;
		}
		MapEntityTipDotNodeLinker nodeLinker = GetNodeLinker();
		if ((Object)(object)nodeLinker == (Object)null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else
		{
			nodeLinker.RegisterEntity(mEntityIdentifier);
		}
	}

	protected override void Unload()
	{
		MapEntityTipDotNodeLinker mapEntityTipDotNodeLinker = (((Object)(object)mTipdotLinker != (Object)null) ? mTipdotLinker : mLoadedPrefab);
		if ((Object)(object)mapEntityTipDotNodeLinker != (Object)null)
		{
			mapEntityTipDotNodeLinker.UnregisterEntity();
		}
		ClearLoadedGo();
		base.Unload();
	}

	private MapEntityTipDotNodeLinker GetNodeLinker()
	{
		if ((Object)(object)mTipdotLinker != (Object)null)
		{
			return mTipdotLinker;
		}
		if ((Object)(object)mLoadedPrefab != (Object)null)
		{
			return mLoadedPrefab;
		}
		if (!mTipdotLinkerPrefabGuidProxy.IsValid)
		{
			return null;
		}
		mLoadHandler = ResourceLoader.LoadAssetByGuid(mTipdotLinkerPrefabGuidProxy.AssetGuid);
		mLoadedPrefab = ResourceLoader.Instantiate(mLoadHandler.Result, mLoadedPrefabSlot).GetComponent<MapEntityTipDotNodeLinker>();
		return mLoadedPrefab;
	}

	private void ClearLoadedGo()
	{
		if (mLoadHandler != null)
		{
			mLoadHandler.Dispose();
			mLoadHandler = null;
		}
		if ((Object)(object)mLoadedPrefab != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mLoadedPrefab).gameObject);
		}
	}
}

}
