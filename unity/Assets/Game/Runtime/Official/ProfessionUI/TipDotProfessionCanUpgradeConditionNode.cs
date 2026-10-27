// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotProfessionCanUpgradeConditionNode : TipDotConditionNodeBase
{
	public List<int> Professions;

	public bool IsAll;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is ProfessionNodeInstanceInfo professionNodeInstanceInfo)
		{
			Professions = new List<int> { professionNodeInstanceInfo.Profession };
		}
		if (IsAll)
		{
			foreach (int item in Rules.GetAllProfessions())
			{
				if (!Professions.Contains(item))
				{
					Professions.Add(item);
				}
			}
		}
		AddEventListener();
	}

	public override void Release()
	{
		RemoveEventListener();
		base.Release();
	}

	private void AddEventListener()
	{
		Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		Player.OnProfessionUpgrade += Me_OnProfessionUpgrade;
		Player.OnProfessionChange += Me_OnProfessionChange;
		Player.OnSkillRankRewardUpdate += OnSkillRankRewardUpdate;
		Player.OnSkillRankUp += Me_OnSkillRankeUp;
		Player.OnSkillRankUpBatch += Me_OnSkillRankUpBatch;
	}

	private void RemoveEventListener()
	{
		_ = Player;
		if (Player != null)
		{
			Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
			Player.OnProfessionUpgrade -= Me_OnProfessionUpgrade;
			Player.OnProfessionChange -= Me_OnProfessionChange;
			Player.OnSkillRankRewardUpdate -= OnSkillRankRewardUpdate;
			Player.OnSkillRankUp -= Me_OnSkillRankeUp;
			Player.OnSkillRankUpBatch -= Me_OnSkillRankUpBatch;
		}
	}

	public override bool CheckShowTip()
	{
		if (Player.Profession == 0)
		{
			return false;
		}
		if (Player.IsFunctionLimit(39))
		{
			return false;
		}
		return Professions.Exists(CanUpgrade);
	}

	private void MainPlayer_OnItemCountUpdate(ProfessionTipDotItem itemData, long preCount, object sourceData)
	{
		if (itemData != null)
		{
			if (itemData.Info.ItemType == 316)
			{
				SetDirty();
			}
			if (itemData.Info.ItemType == 100 || itemData.Info.ItemType == 101)
			{
				SetDirty();
			}
		}
	}

	private void OnSkillRankRewardUpdate(int arg1, int arg2)
	{
		SetDirty();
	}

	private void Me_OnProfessionUpgrade(int arg1, int arg2)
	{
		SetDirty();
	}

	private void Me_OnSkillRankeUp(ProfessionTipDotItem itemData, int preRank, int newRank)
	{
		SetDirty();
	}

	private void Me_OnSkillRankUpBatch(Dictionary<int, int> skillRanks, Dictionary<int, int> preSkillRanks)
	{
		SetDirty();
	}

	private void Me_OnProfessionChange(int from, int to)
	{
		SetDirty();
	}

	private bool CanUpgrade(int pro)
	{
		IProfessionTipDotPlayer mainPlayer = Player;
		if (!mainPlayer.IsProfessionUnlocked(pro))
		{
			return false;
		}
		IProfessionTipDotRules professionLevelCostInfoParser = Rules;
		ProfessionTipDotBaseInfo professionBaseInfo = Rules.GetProfessionBaseInfo(pro);
		int professionLevel = mainPlayer.GetProfessionLevel(pro);
		if (!professionLevelCostInfoParser.TryGetLevelInfo(pro, professionLevel + 1, out var info))
		{
			return false;
		}
		foreach (KeyValuePair<int, long> item in info.UpgradeCost)
		{
			long num = mainPlayer.GetItemAmount(item.Key);
			if (item.Key == professionBaseInfo.BadgeClassId && num < item.Value)
			{
				num += Rules.CalcAwardBadge(professionBaseInfo, mainPlayer);
			}
			if (num < item.Value)
			{
				return false;
			}
		}
		return true;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotProfessionCanUpgradeConditionNode tipDotProfessionCanUpgradeConditionNode = new TipDotProfessionCanUpgradeConditionNode();
		tipDotProfessionCanUpgradeConditionNode.Fill(this);
		return tipDotProfessionCanUpgradeConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotProfessionCanUpgradeConditionNode tipDotProfessionCanUpgradeConditionNode = source as TipDotProfessionCanUpgradeConditionNode;
		if (tipDotProfessionCanUpgradeConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotProfessionCanUpgradeConditionNode);
	}

	public void Fill(TipDotProfessionCanUpgradeConditionNode obj)
	{
		Professions = obj.Professions == null ? null : new List<int>(obj.Professions);
		IsAll = obj.IsAll;
	}
}
}
