// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotAnyProfessionPromotionCondtionNode : TipDotConditionNodeBase
{
	public override void Init()
	{
		base.Init();
		AddEventListener();
	}

	public override void Release()
	{
		RemoveEventListener();
		base.Release();
	}

	private void AddEventListener()
	{
		IProfessionTipDotPlayer mainPlayer = Player;
		mainPlayer.OnProfessionPromote += Me_OnProfessionPromote;
		mainPlayer.OnProfessionChange += Me_OnProfessionChange;
		mainPlayer.OnProfessionUpgrade += Me_OnProfessionUpgrade;
		mainPlayer.OnTaskAdd += Me_OnTaskAdd;
	}

	private void RemoveEventListener()
	{
		IProfessionTipDotPlayer mainPlayer = Player;
		if (mainPlayer != null)
		{
			mainPlayer.OnProfessionPromote -= Me_OnProfessionPromote;
			mainPlayer.OnProfessionChange -= Me_OnProfessionChange;
			mainPlayer.OnProfessionUpgrade -= Me_OnProfessionUpgrade;
			mainPlayer.OnTaskAdd -= Me_OnTaskAdd;
		}
	}

	private void Me_OnProfessionPromote(int obj)
	{
		SetDirty();
	}

	private void Me_OnProfessionUpgrade(int pro, int level)
	{
		SetDirty();
	}

	private void Me_OnProfessionChange(int from, int to)
	{
		SetDirty();
	}

	private void Me_OnTaskAdd(object obj)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		if (!Rules.AnyPromotionRequirementCompleted())
		{
			return Rules.HasAvailableHigherPromotion();
		}
		return true;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotAnyProfessionPromotionCondtionNode tipDotAnyProfessionPromotionCondtionNode = new TipDotAnyProfessionPromotionCondtionNode();
		tipDotAnyProfessionPromotionCondtionNode.Fill(this);
		return tipDotAnyProfessionPromotionCondtionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotAnyProfessionPromotionCondtionNode tipDotAnyProfessionPromotionCondtionNode = source as TipDotAnyProfessionPromotionCondtionNode;
		if (tipDotAnyProfessionPromotionCondtionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotAnyProfessionPromotionCondtionNode);
	}

	public void Fill(TipDotAnyProfessionPromotionCondtionNode obj)
	{
	}
}
}
