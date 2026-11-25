// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{
[Serializable]
public class TipDotHangUpItemCanUnlockConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public EFurniture Furniture;

	public override void Init()
	{
		base.Init();
		Services.Player.OnLevelUpdate += OnLevelUpdate;
		Services.Player.OnHangUpUnlock += OnHangUpUnlock;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnLevelUpdate -= OnLevelUpdate;
			Services.Player.OnHangUpUnlock -= OnHangUpUnlock;
		}
		base.Release();
	}

	private void OnLevelUpdate(int level)
	{
		SetDirty();
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		if (Services.CheckHangUpUnlockMet(Furniture))
		{
			return true;
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHangUpItemCanUnlockConditionNode tipDotHangUpItemCanUnlockConditionNode = new TipDotHangUpItemCanUnlockConditionNode();
		tipDotHangUpItemCanUnlockConditionNode.Fill(this);
		return tipDotHangUpItemCanUnlockConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHangUpItemCanUnlockConditionNode tipDotHangUpItemCanUnlockConditionNode = source as TipDotHangUpItemCanUnlockConditionNode;
		if (tipDotHangUpItemCanUnlockConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHangUpItemCanUnlockConditionNode);
	}

	public void Fill(TipDotHangUpItemCanUnlockConditionNode obj)
	{
		Furniture = obj.Furniture;
	}
}

}
