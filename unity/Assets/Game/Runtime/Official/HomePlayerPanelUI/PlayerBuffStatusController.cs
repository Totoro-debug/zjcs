// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBuffStatusController : IPlayerStatusProvider
{
	private IHomePanelResourceLoader mResourceLoader;

	private List<EStatusSourceType> mShowStatusSourceType = new List<EStatusSourceType>
	{
		EStatusSourceType.Map,
		EStatusSourceType.Cooking,
		EStatusSourceType.Story,
		EStatusSourceType.MapScene
	};

	private MonoDictionary<StatusEffectIconType, AssetGuidProxy<GameObject>> mBuffIconAssetGuidDic;

	private Dictionary<StatusEffectIconType, GameObjectPool<BuffItemUI>> mBuffPoolDic = new Dictionary<StatusEffectIconType, GameObjectPool<BuffItemUI>>();

	private List<BuffItemUI> mBuffList = new List<BuffItemUI>();

	private Transform mStatusItemSlot;

	private HomePanelDisposableBag mDisposableBag;

	public List<HomeEntryDetailData> EntryDatas { get; private set; } = new List<HomeEntryDetailData>();

	private event Action OnStatusUpdated;

	private readonly IHomePlayerPanelBindings PanelBindings;

	public PlayerBuffStatusController(IHomePlayerPanelBindings bindings, IHomePanelResourceLoader resourceLoader, MonoDictionary<StatusEffectIconType, AssetGuidProxy<GameObject>> buffItemUI, Transform itemSlot)
	{

		PanelBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
		mDisposableBag = default;
		mResourceLoader = resourceLoader;
		mBuffIconAssetGuidDic = buffItemUI;
		mStatusItemSlot = itemSlot;
	}

	public void RegisterUpdateStatusCallBack(Action callBack)
	{
		OnStatusUpdated += callBack;
	}

	public void UnRegisterUpdateStatusCallBack(Action callBack)
	{
		OnStatusUpdated -= callBack;
	}

	public void Start()
	{
		PanelBindings.MainPlayer.OnStatusEffectDataUpdate += MainPlayer_OnStatusEffectDataUpdate;
		RefreshView();
	}

	public void End()
	{
		if (PanelBindings.MainPlayer != null)
		{
			PanelBindings.MainPlayer.OnStatusEffectDataUpdate -= MainPlayer_OnStatusEffectDataUpdate;
		}
	}

	public void Dispose()
	{
		mDisposableBag.Dispose();
	}

	public List<HomeEntryDetailData> GetEntryDatas()
	{
		return EntryDatas;
	}

	public bool HasEntryData()
	{
		return EntryDatas.Count > 0;
	}

	private void MainPlayer_OnStatusEffectDataUpdate(IHomePanelStatusEffect obj)
	{
		RefreshView();
		OnStatusUpdated?.Invoke();
	}

	private void RefreshView()
	{
		ClearItems();
		EntryDatas.Clear();
		foreach (IHomePanelStatusEffect value in PanelBindings.MainPlayer.StatusEffectManager.Values)
		{
			if (value != null && value.IsActive && mShowStatusSourceType.Contains(value.StatusSourceType))
			{
				BuffItemUI buffItemUI = GetItemIconPool(value.Info.IconType).Borrow(mStatusItemSlot);
				mBuffList.Add(buffItemUI);
				buffItemUI.Init(value);
				buffItemUI.SetButtonEnable(isEnable: false);
				EntryDatas.Add(BuffItemUIUtils.CreateBuffItemEntryDetailData(PanelBindings, value));
			}
		}
	}

	private void ClearItems()
	{
		foreach (BuffItemUI mBuff in mBuffList)
		{
			GetItemIconPool(mBuff.StatusEffectData.Info.IconType).Return(mBuff);
		}
		mBuffList.Clear();
	}

	private GameObjectPool<BuffItemUI> GetItemIconPool(StatusEffectIconType statusEffectIconType)
	{
		if (!mBuffPoolDic.TryGetValue(statusEffectIconType, out var value))
		{
			BuffItemUI buffItemUIPrefab = GetBuffItemUIPrefab(statusEffectIconType);
			value = new GameObjectPool<BuffItemUI>(mStatusItemSlot, mStatusItemSlot, buffItemUIPrefab);
			mBuffPoolDic.Add(statusEffectIconType, value);
		}
		return value;
	}

	private BuffItemUI GetBuffItemUIPrefab(StatusEffectIconType statusEffectIconType)
	{
		if (mBuffIconAssetGuidDic.TryGetValue(statusEffectIconType, out var value))
		{
			GameObject result = HomePanelDisposable.AddTo<IHomeHudAssetHandle<GameObject>>(mResourceLoader.LoadAssetByGuid<GameObject>(value.AssetGuid), ref mDisposableBag).Result;
			if ((Object)(object)result != (Object)null)
			{
				return result.GetComponent<BuffItemUI>();
			}
		}
		return null;
	}
}

}
