// Complete original common promotion control; explicit local material services.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.ProfessionUI {
using Object=UnityEngine.Object;
using System;
using System.Collections.Generic;




using UnityEngine;

public class ProfessionDebugUIHandler : MonoBehaviour
{
    private IProfessionDebugUIServices services;
    private IProfessionDebugUIServices Services=>services??throw new InvalidOperationException("Bind the original debug UI actor before activation.");
    public void Configure(IProfessionDebugUIServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	[Serializable]
	public struct AssetGuidAndSlotProxy
	{
		[field: SerializeField]
		public ProfessionPrefabGuidProxy AssetGuidProxy { get; private set; }

		[field: SerializeField]
		public Transform Slot { get; private set; }

		public bool IsValid
		{
			get
			{
				if (AssetGuidProxy != null)
				{
					return AssetGuidProxy.IsValid;
				}
				return false;
			}
		}
	}

	public bool IsForceHiden;

	[SerializeField]
	private List<AssetGuidAndSlotProxy> mAssetGuidAndSlotProxies;

	private bool mIsShow;

	private ProfessionResourceOperationBag mDisposableBag=new ProfessionResourceOperationBag();


	private void Awake()
	{

		mIsShow = !IsForceHiden && Services.PlayerAvailable && Services.IsGameMaster;
		if (mIsShow)
		{
			mIsShow = false;
			mDisposableBag = new ProfessionResourceOperationBag();
			Services.CheckGameMaster(false,DebugUIHandler_OnComplete);
		}
	}

	private void DebugUIHandler_OnComplete(bool isGm)
	{
		mIsShow = isGm;
		if (!mIsShow)
		{
			return;
		}
		foreach (AssetGuidAndSlotProxy mAssetGuidAndSlotProxy in mAssetGuidAndSlotProxies)
		{
			if (mAssetGuidAndSlotProxy.IsValid)
			{
				Services.Instantiate(mDisposableBag.Add(Services.Resources.LoadAssetByGuid<GameObject>(mAssetGuidAndSlotProxy.AssetGuidProxy.AssetGuid)).Result,mAssetGuidAndSlotProxy.Slot);
			}
		}
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}
}

}
