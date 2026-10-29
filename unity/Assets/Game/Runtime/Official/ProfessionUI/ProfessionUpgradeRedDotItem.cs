namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;

public class ProfessionUpgradeRedDotItem : ProfessionRedDotItemBase
{
	public List<int> Professions;

	public void SetProfessions(int professions)
	{
		Professions.Clear();
		Professions.Add(professions);
		Refresh();
	}

	public void SetProfessions(List<int> professions)
	{
		Professions = professions;
		Refresh();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		IProfessionUpgradePlayer mainPlayer = Bindings.MainPlayer;
		mainPlayer.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mainPlayer.OnProfessionUpgrade += Me_OnProfessionUpgrade;
		mainPlayer.OnSkillRankRewardUpdate += OnSkillRankRewardUpdate;
		mainPlayer.OnSkillRankUp += Me_OnSkillRankUp;
		mainPlayer.OnSkillRankUpBatch += Me_OnSkillRankUpBatch;
	}

	protected override void OnDisableAndInited()
	{
		IProfessionUpgradePlayer mainPlayer = Bindings.MainPlayer;
		if (mainPlayer != null)
		{
			mainPlayer.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
			mainPlayer.OnProfessionUpgrade -= Me_OnProfessionUpgrade;
			mainPlayer.OnSkillRankRewardUpdate -= OnSkillRankRewardUpdate;
			mainPlayer.OnSkillRankUp -= Me_OnSkillRankUp;
			mainPlayer.OnSkillRankUpBatch -= Me_OnSkillRankUpBatch;
		}
		((MonoBehaviour)this).StopAllCoroutines();
		base.OnDisableAndInited();
	}

	public override bool ShowRedDot()
	{
		if (Bindings.MainPlayer.Profession == 0)
		{
			return false;
		}
		if (Bindings.MainPlayer.IsProfessionFunctionLimit)
		{
			return false;
		}
		return Professions.Exists(CanUpgrade);
	}

	private void MainPlayer_OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (Bindings.GetItemType(itemData.Info) == 316)
		{
			Refresh();
		}
		if (Bindings.GetItemType(itemData.Info) == 100 || Bindings.GetItemType(itemData.Info) == 101)
		{
			Refresh();
		}
	}

	private void Me_OnSkillRankUp(IProfessionItemData itemData, int preRank, int newRank)
	{
		Refresh();
	}

	private void Me_OnSkillRankUpBatch(Dictionary<int, int> skillRanks, Dictionary<int, int> preSkillRanks)
	{
		Refresh();
	}

	private void OnSkillRankRewardUpdate(int arg1, int arg2)
	{
		Refresh();
	}

	private void Me_OnProfessionUpgrade(int arg1, int arg2)
	{
		Refresh();
	}

	private bool CanUpgrade(int pro)
	{
		IProfessionUpgradePlayer mainPlayer = Bindings.MainPlayer;
		if (!mainPlayer.IsProfessionUnlocked(pro))
		{
			return false;
		}
		ProfessionDetailDefinition professionBaseInfo = Bindings.GetProfession(pro);
		int professionLevel = mainPlayer.GetProfessionLevel(pro);
		if (!Bindings.TryGetLevelInfo(pro, professionLevel + 1, out var info))
		{
			return false;
		}
		foreach (KeyValuePair<int, long> item in info.UpgradeCost)
		{
			long num = mainPlayer.GetItemAmount(item.Key);
			if (item.Key == professionBaseInfo.BadgeClassId && num < item.Value)
			{
				num += Bindings.CalcAwardBadge(professionBaseInfo, mainPlayer);
			}
			if (num < item.Value)
			{
				return false;
			}
		}
		return true;
	}
}

}
