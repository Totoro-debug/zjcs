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
public class TipDotHasItemFastHangUpTimesConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public EFurniture Furniture;

	private List<HomeTipFastHangUpInfo> mFastHangUpList;

	public override void Init()
	{
		base.Init();
		if (Services.TryGetFastHangUpInfo(Furniture, out mFastHangUpList))
		{
			Services.Player.OnHangUpUnlock += OnHangUpUnlock;
			Services.Player.OnFastHangUp += OnFastHangUp;
			Services.Player.OnItemCountUpdate += OnItemUpdate;
		}
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnHangUpUnlock -= OnHangUpUnlock;
			Services.Player.OnFastHangUp -= OnFastHangUp;
			Services.Player.OnItemCountUpdate -= OnItemUpdate;
		}
		base.Release();
	}

	private void OnFastHangUp(int fastHangUpId)
	{
		if (mFastHangUpList == null)
		{
			return;
		}
		foreach (HomeTipFastHangUpInfo mFastHangUp in mFastHangUpList)
		{
			if (mFastHangUp.FastHangUpId == fastHangUpId)
			{
				SetDirty();
				break;
			}
		}
	}

	private void OnItemUpdate(HomeTipItemData itemData, long preCount, object sourceData)
	{
		if (itemData != null && Services.IsFastHangUpItem(itemData.Info.ClassId))
		{
			SetDirty();
		}
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		if (Services.GetHangUpBindFurniture(hangUpId) == Furniture)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		if (mFastHangUpList == null)
		{
			return false;
		}
		long num = 0L;
		foreach (HomeTipFastHangUpInfo mFastHangUp in mFastHangUpList)
		{
			if (mFastHangUp.FastHangUpItemDic == null || !ValidCheck(mFastHangUp))
			{
				continue;
			}
			foreach (int key in mFastHangUp.FastHangUpItemDic.Keys)
			{
				num += Services.Player.GetItemAmount(key);
			}
			if (num > 0)
			{
				TipDotInfo.Data = num.ToString();
				return true;
			}
		}
		return false;
	}

	private bool ValidCheck(HomeTipFastHangUpInfo fastHangUpInfo)
	{
		IHomeTipDotPlayer mainPlayer = Services.Player;
		foreach (int hangUpId in fastHangUpInfo.HangUpIdList)
		{
			if (mainPlayer.HasUnlockedHangUp(hangUpId))
			{
				return true;
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHasItemFastHangUpTimesConditionNode tipDotHasItemFastHangUpTimesConditionNode = new TipDotHasItemFastHangUpTimesConditionNode();
		tipDotHasItemFastHangUpTimesConditionNode.Fill(this);
		return tipDotHasItemFastHangUpTimesConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHasItemFastHangUpTimesConditionNode tipDotHasItemFastHangUpTimesConditionNode = source as TipDotHasItemFastHangUpTimesConditionNode;
		if (tipDotHasItemFastHangUpTimesConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHasItemFastHangUpTimesConditionNode);
	}

	public void Fill(TipDotHasItemFastHangUpTimesConditionNode obj)
	{
		Furniture = obj.Furniture;
	}
}

}
