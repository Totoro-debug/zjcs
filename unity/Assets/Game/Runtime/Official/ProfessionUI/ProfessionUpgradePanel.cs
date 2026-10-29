namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionUpgradePanel : MonoBehaviour, IProfessionDetailUpgradePanel
{
    private IProfessionUpgradeBindings mBindings;
    protected IProfessionUpgradeBindings Bindings => mBindings ?? ProfessionUpgradeBindings.Required;
    public void Configure(IProfessionUpgradeBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public Transform CostTrans;

	public ProfessionUpgradeRedDotItem ProfessionRedDot;

	public Transform PropItemSlot;

	public GameObject PropItemPrefab;

	public WorldMultiLanguageText AwardPropText;

	public Button BtnUpgrade;

	public Animation UpAnimation;

	public GameObject ReachMaxGo;

	private int mPro;

	private ProfessionDetailDefinition mInfo;

	private List<ItemIcon> mItemIcons = new List<ItemIcon>();

	private StringBuilder mSB = new StringBuilder();

	private GameObjectPool<ProfessionPlayerPropItem> mPropItemPool;

	private List<ProfessionPlayerPropItem> mPropItems = new List<ProfessionPlayerPropItem>();

	private bool mNeedEffect;

	[SerializeField]
	private PropUpgradeEffectAgent mEffectAgent;


	private void Awake()
	{
		mPropItemPool = new GameObjectPool<ProfessionPlayerPropItem>(((Component)this).transform, PropItemSlot, PropItemPrefab);
		BtnUpgrade.onClick.AddListener(OnUpgrade);
	}

	private void OnDisable()
	{
		RecycleItem();
		StopAllEffectAndClearItem();
		mNeedEffect = false;
	}

	public void Init(int pro)
	{
		mPro = pro;
		mInfo = Bindings.GetProfession(mPro);
		ProfessionRedDot.Configure(Bindings);
		ProfessionRedDot.SetProfessions(pro);
		UpdateView();
	}

	private void UpdateView()
	{
		int professionLevel = Bindings.MainPlayer.GetProfessionLevel(mPro);
		UpdateProp(professionLevel);
		if (Bindings.TryGetLevelInfo(mPro, professionLevel + 1, out var _))
		{
			UpdateDiffProp(professionLevel);
			UpdateCostAndBtn(professionLevel + 1);
		}
	}

	private void UpdateProp(int level)
	{
		int levelPropId = Bindings.GetLevelPropId(mPro);
		StopAllEffectAndClearItem();
		IReadOnlyDictionary<int, long> readOnlyDictionary = null;
		if (level > 1)
		{
			readOnlyDictionary = Bindings.GetProps(levelPropId, level - 1);
		}
		IReadOnlyDictionary<int, long> props = Bindings.GetProps(levelPropId, level);
		int num = 0;
		foreach (KeyValuePair<int, long> item in props)
		{
			if (item.Value == 0L)
			{
				continue;
			}
			ProfessionPlayerPropItem newPropItem = GetNewPropItem();
			((Component)newPropItem).gameObject.SetActive(true);
			Bindings.InitPropItem(newPropItem, item.Key, item.Value, -1L);
			if (readOnlyDictionary != null)
			{
				if (readOnlyDictionary.TryGetValue(item.Key, out var value))
				{
					if (item.Value != value)
					{
						mEffectAgent.AddPropItem(newPropItem);
					}
				}
				else
				{
					mEffectAgent.AddPropItem(newPropItem);
				}
			}
			num++;
		}
		PlayEffect();
	}

	private void StopAllEffectAndClearItem()
	{
		mEffectAgent.StopPlayEffect();
		mEffectAgent.ClearPropItem();
		foreach (ProfessionPlayerPropItem mPropItem in mPropItems)
		{
			mPropItemPool.Return(mPropItem);
		}
		mPropItems.Clear();
	}

	private void PlayEffect()
	{
		if (mNeedEffect && mEffectAgent.GetPropItemCount() > 0)
		{
			mEffectAgent.StartPlayEffect(null, isForce: true);
		}
	}

	private void UpdateDiffProp(int curLevel)
	{
		int toLevel = curLevel + 1;
		Dictionary<int, long> professionLevelPropDiff = Bindings.GetProfessionLevelPropDiff(mPro, curLevel, toLevel);
		mSB.Clear();
		foreach (KeyValuePair<int, long> item in professionLevelPropDiff)
		{
			string text = Bindings.GetPropNameText(item.Key);
			mSB.Append(text);
			mSB.Append($"+{Bindings.FormatPropValue(item.Key, item.Value)} ");
		}
		AwardPropText.SetKey(mSB.ToString());
	}

	private void UpdateCostAndBtn(int targetLevel)
	{
		RecycleItem();
		if (!Bindings.TryGetLevelInfo(mPro, targetLevel, out var info))
		{
			return;
		}
		foreach (KeyValuePair<int, long> item2 in info.UpgradeCost)
		{
			ItemIcon item = Bindings.GetIcon(item2.Key, CostTrans).SetRequireCount(item2.Value).SetClickPopupInfoDialog();
			mItemIcons.Add(item);
		}
	}

	private void RecycleItem()
	{
		if ((mBindings ?? ProfessionUpgradeBindings.Current) == null)
		{
			return;
		}
		foreach (ItemIcon mItemIcon in mItemIcons)
		{
			Bindings.RecycleIcon(mItemIcon);
		}
		mItemIcons.Clear();
	}

	private ProfessionPlayerPropItem GetNewPropItem()
	{
		ProfessionPlayerPropItem playerPropItem = mPropItemPool.Borrow();
		bool bgVisible = mPropItems.Count % 4 < 2;
		playerPropItem.SetBgVisible(bgVisible);
		mPropItems.Add(playerPropItem);
		return playerPropItem;
	}

	public void OnUpgrade()
	{
		Bindings.Upgrade(mPro, () =>
		{
			mNeedEffect = true;
			UpdateView();
			UpAnimation.Rewind();
			UpAnimation.Play();
		});
	}
}

}
