// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{
[Serializable]
public class TipDotHangUpItemCanLevelUpConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public EFurniture Furniture;

	protected List<HomeTipHangUpInfo> mHangUpInfos;

	private readonly Dictionary<int, Dictionary<int, long>> mHangUpCanLevelUpItemCostDict = new Dictionary<int, Dictionary<int, long>>();

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is HangUpNodeInstanceInfo hangUpNodeInstanceInfo)
		{
			if (mHangUpInfos == null)
			{
				mHangUpInfos = new List<HomeTipHangUpInfo>(1);
			}
			mHangUpInfos.Clear();
			if (Services.TryGetHangUpInfo(hangUpNodeInstanceInfo.HangUpId, out HomeTipHangUpInfo item) && item != null)
			{
				mHangUpInfos.Add(item);
			}
		}
		else
		{
			Services.TryGetHangUpInfos(Furniture, out mHangUpInfos);
		}
		IHomeTipDotPlayer mainPlayer = Services.Player;
		mainPlayer.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mainPlayer.OnHangUpLevelUp += MainPlayer_OnHangUpLevelUp;
		mainPlayer.OnHangUpUnlock += MainPlayer_OnHangUpUnlock;
		mainPlayer.OnGoddessUpdate += MainPlayer_OnGoddessUpdate;
		RefreshLevelUpInfo();
	}

	public override void Release()
	{
		IHomeTipDotPlayer mainPlayer = Services.Player;
		if (mainPlayer != null)
		{
			mainPlayer.OnHangUpLevelUp -= MainPlayer_OnHangUpLevelUp;
			mainPlayer.OnHangUpUnlock -= MainPlayer_OnHangUpUnlock;
			mainPlayer.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
			mainPlayer.OnGoddessUpdate -= MainPlayer_OnGoddessUpdate;
		}
		base.Release();
	}

	protected virtual void MainPlayer_OnGoddessUpdate()
	{
		RefreshLevelUpInfo();
		SetDirty();
	}

	protected virtual void MainPlayer_OnHangUpUnlock(int hangUpId)
	{
		if (ContainsHangUpId(hangUpId))
		{
			RefreshLevelUpInfo();
			SetDirty();
		}
	}

	private void MainPlayer_OnHangUpLevelUp(int hangUpId, int level)
	{
		if (ContainsHangUpId(hangUpId))
		{
			RefreshLevelUpInfo();
			SetDirty();
		}
	}

	private void MainPlayer_OnItemCountUpdate(HomeTipItemData itemData, long preCount, object sourceData)
	{
		if (itemData == null)
		{
			return;
		}
		foreach (KeyValuePair<int, Dictionary<int, long>> item in mHangUpCanLevelUpItemCostDict)
		{
			if (item.Value.ContainsKey(itemData.Id))
			{
				SetDirty();
				break;
			}
		}
	}

	public override bool CheckShowTip()
	{
		if (mHangUpInfos == null)
		{
			return false;
		}
		if (mHangUpCanLevelUpItemCostDict.Count == 0)
		{
			return false;
		}
		IHomeTipDotPlayer mainPlayer = Services.Player;
		foreach (KeyValuePair<int, Dictionary<int, long>> item in mHangUpCanLevelUpItemCostDict)
		{
			if (mainPlayer.HasItems(item.Value))
			{
				return true;
			}
		}
		return false;
	}

	protected void RefreshLevelUpInfo()
	{
		if (mHangUpInfos == null)
		{
			return;
		}
		mHangUpCanLevelUpItemCostDict.Clear();
		int hangUpMaxLevel = Services.Player.GetHangUpMaxLevel();
		foreach (HomeTipHangUpInfo mHangUpInfo in mHangUpInfos)
		{
			if (!mHangUpInfo.CanLevelUp)
			{
				continue;
			}
			int id = mHangUpInfo.Id;
			if (Services.Player.TryGetUnlockSingleHangUpData(id, out int currentLevel))
			{
				int maxLevel = Services.GetHangUpUpgradeMaxLevel(id);
				if (currentLevel < hangUpMaxLevel && currentLevel != maxLevel)
				{
					Dictionary<int, long> cost = Services.GetHangUpUpgradeCosts(id, currentLevel + 1);
					if (cost != null)
					{
						mHangUpCanLevelUpItemCostDict[id] = cost;
					}
				}
			}
		}
	}

	private bool ContainsHangUpId(int hangUpId)
	{
		foreach (HomeTipHangUpInfo mHangUpInfo in mHangUpInfos)
		{
			if (mHangUpInfo.Id == hangUpId)
			{
				return true;
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHangUpItemCanLevelUpConditionNode tipDotHangUpItemCanLevelUpConditionNode = new TipDotHangUpItemCanLevelUpConditionNode();
		tipDotHangUpItemCanLevelUpConditionNode.Fill(this);
		return tipDotHangUpItemCanLevelUpConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHangUpItemCanLevelUpConditionNode tipDotHangUpItemCanLevelUpConditionNode = source as TipDotHangUpItemCanLevelUpConditionNode;
		if (tipDotHangUpItemCanLevelUpConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHangUpItemCanLevelUpConditionNode);
	}

	public void Fill(TipDotHangUpItemCanLevelUpConditionNode obj)
	{
		Furniture = obj.Furniture;
	}
}

}
