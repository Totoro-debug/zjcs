// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]

public class TipDotAchievementConditionNode : TipDotConditionNodeBase
{
	public List<AchievementTypeAndParamItem> AchievementTypeAndParamList;

	protected List<ProfessionTipDotAchievementKey> mDataKeys = new List<ProfessionTipDotAchievementKey>();

	protected List<int> mAchievementGroupIds = new List<int>();

	public override void Init()
	{
		base.Init();
		mDataKeys.Clear();
		foreach (AchievementTypeAndParamItem achievementTypeAndParam in AchievementTypeAndParamList)
		{
			ProfessionTipDotAchievementKey achievementDataKey = GetAchievementDataKey(achievementTypeAndParam);
			AddDataKey(achievementDataKey);
		}
		mAchievementGroupIds = Rules.GetAllAchievementGroupIds();
		Player.OnRecordAlreadyGotAchievementRewardByGroupId += MainPlayer_OnRecordAlreadyGotAchievementReward;
		Player.OnRecordAlreadyGotAchievementRewardBatch += MainPlayer_OnRecordAlreadyGotAchievementRewardBatch;
		Player.OnUpdateAchievementData += MainPlayer_OnUpdateAchievementData;
		Rules.RegisterAchievementGroupLimitCondition(SetDirty);
	}

	public override void Release()
	{
		if (Player != null)
		{
			Player.OnRecordAlreadyGotAchievementRewardByGroupId -= MainPlayer_OnRecordAlreadyGotAchievementReward;
			Player.OnUpdateAchievementData -= MainPlayer_OnUpdateAchievementData;
			Player.OnRecordAlreadyGotAchievementRewardBatch -= MainPlayer_OnRecordAlreadyGotAchievementRewardBatch;
		}
		Rules.DeregisterAchievementGroupLimitCondition(SetDirty);
		AchievementTypeAndParamList.Clear();
		mDataKeys.Clear();
		base.Release();
	}

	protected void AddDataKey(ProfessionTipDotAchievementKey key)
	{
		IProfessionTipDotRules achievementRawInfoParser = Rules;
		if (Rules.AchievementTypeListForShowAllInfosInSameType.Contains(key.AchievementType))
		{
			mDataKeys.AddRange(achievementRawInfoParser.GetAchievementDataKeysBySameType(key));
		}
		else
		{
			mDataKeys.Add(key);
		}
	}

	private void MainPlayer_OnRecordAlreadyGotAchievementRewardBatch()
	{
		SetDirty();
	}

	private void MainPlayer_OnUpdateAchievementData(ProfessionTipDotAchievementKey achievementDataKey)
	{
		foreach (ProfessionTipDotAchievementKey mDataKey in mDataKeys)
		{
			if (mDataKey.AchievementType == achievementDataKey.AchievementType)
			{
				SetDirty();
				break;
			}
		}
	}

	private void MainPlayer_OnRecordAlreadyGotAchievementReward(int obj)
	{
		SetDirty();
	}

	protected ProfessionTipDotAchievementKey GetAchievementDataKey(AchievementTypeAndParamItem data)
	{
		return Rules.CreateAchievementDataKey(data.AchievementType, data.JsonParam);
	}

	public override bool CheckShowTip()
	{
		if (!Player.IsFunctionLimit(136))
		{
			foreach (ProfessionTipDotAchievementKey mDataKey in mDataKeys)
			{
				if (!mAchievementGroupIds.Contains(mDataKey.GroupId) && Player.IsAnyCanGetAchievementReward(mDataKey))
				{
					return true;
				}
			}
			return false;
		}
		return Player.IsAnyCanGetAchievementReward(mDataKeys);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotAchievementConditionNode tipDotAchievementConditionNode = new TipDotAchievementConditionNode();
		tipDotAchievementConditionNode.Fill(this);
		return tipDotAchievementConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotAchievementConditionNode tipDotAchievementConditionNode = source as TipDotAchievementConditionNode;
		if (tipDotAchievementConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotAchievementConditionNode);
	}

	public void Fill(TipDotAchievementConditionNode obj)
	{
		AchievementTypeAndParamList = obj.AchievementTypeAndParamList == null ? null : obj.AchievementTypeAndParamList.ConvertAll(value => value == null ? null : value.Clone());
	}
}
}
