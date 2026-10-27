// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotProfessionPromotionCondtionNode : TipDotConditionNodeBase
{
	public int Profession;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is ProfessionNodeInstanceInfo professionNodeInstanceInfo)
		{
			Profession = professionNodeInstanceInfo.Profession;
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
		Player.OnProfessionUpgrade += Me_OnProfessionUpgrade;
	}

	private void RemoveEventListener()
	{
		IProfessionTipDotPlayer mainPlayer = Player;
		if (mainPlayer != null)
		{
			mainPlayer.OnProfessionUpgrade -= Me_OnProfessionUpgrade;
		}
	}

	private void Me_OnProfessionUpgrade(int pro, int level)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		if (Rules.IsPromotionRequirementCompleted(Profession))
		{
			return true;
		}
		if (Rules.IsPromotableAndInNewRank(Profession))
		{
			return true;
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotProfessionPromotionCondtionNode tipDotProfessionPromotionCondtionNode = new TipDotProfessionPromotionCondtionNode();
		tipDotProfessionPromotionCondtionNode.Fill(this);
		return tipDotProfessionPromotionCondtionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotProfessionPromotionCondtionNode tipDotProfessionPromotionCondtionNode = source as TipDotProfessionPromotionCondtionNode;
		if (tipDotProfessionPromotionCondtionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotProfessionPromotionCondtionNode);
	}

	public void Fill(TipDotProfessionPromotionCondtionNode obj)
	{
		Profession = obj.Profession;
	}
}
}
