// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotCanRecycleGemConditionNode : TipDotConditionNodeBase, IBagTipDotNode, IBagGemSubscriber
{
	public IBagTipDotServices Services { get; set; }

	private IBagGemMergeManager GemDataManager => Services.GemMergeManager;

	public override void Init()
	{
		base.Init();
		Services.Player.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
		}
		base.Release();
	}

	protected override void AddScopeDataCollector()
	{
		base.AddScopeDataCollector();
		GemDataManager?.Subscribe(this);
		SetDirty();
	}

	protected override void RemoveScopeDataCollector()
	{
		GemDataManager?.Unsubscribe(this);
		SetDirty();
		base.RemoveScopeDataCollector();
	}

	private void MainPlayer_OnSubRankUpdate(int obj)
	{
		Refresh();
	}

	public override bool CheckShowTip()
	{
		if (Services.IsFunctionLimit(Services.Player, FunctionType.RecycleGem))
		{
			return false;
		}
		return Services.Player.GetCanRecycleGem().Count > 0;
	}

	public void Refresh()
	{
		SetDirty();
	}

	public override TipDotNodeBase Clone()
	{
		TipDotCanRecycleGemConditionNode tipDotCanRecycleGemConditionNode = new TipDotCanRecycleGemConditionNode();
		tipDotCanRecycleGemConditionNode.Fill(this);
		return tipDotCanRecycleGemConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotCanRecycleGemConditionNode tipDotCanRecycleGemConditionNode = source as TipDotCanRecycleGemConditionNode;
		if (tipDotCanRecycleGemConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotCanRecycleGemConditionNode);
	}

	public void Fill(TipDotCanRecycleGemConditionNode obj)
	{
	}
}

}
