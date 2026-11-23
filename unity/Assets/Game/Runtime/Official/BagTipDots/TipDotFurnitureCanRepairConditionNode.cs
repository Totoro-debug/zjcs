// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotFurnitureCanRepairConditionNode : TipDotConditionNodeBase, IBagTipDotNode
{
	public IBagTipDotServices Services { get; set; }

	public EFurniture mFurniture;

	private BagTipFurnitureInfo mFurnitureInfo;

	private IDisposable mRegistration;

	private BagTipFurnitureInfo FurnitureInfo => mFurnitureInfo ?? (mFurnitureInfo = Services.GetFurnitureInfo(mFurniture));

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
		if (Services.Player != null)
		{
			if (!Services.Player.HasFurniture(mFurniture))
			{
				mRegistration = Services.SubscribeCondition(FurnitureInfo.UnlockCondition, SetDirty);
			}
			Services.Player.OnRepairFurniture += Player_OnRepairFurniture;
		}
	}

	private void RemoveEventListener()
	{
		DisposeRegistration();
		if (Services.Player != null)
		{
			Services.Player.OnRepairFurniture -= Player_OnRepairFurniture;
		}
	}

	private void Player_OnRepairFurniture(EFurniture furniture)
	{
		if (mFurniture == furniture)
		{
			SetDirty();
			DisposeRegistration();
		}
	}

	private void DisposeRegistration()
	{
		if (mRegistration != null)
		{
			mRegistration.Dispose();
			mRegistration = null;
		}
	}

	public override bool CheckShowTip()
	{
		if (Services.Player.HasFurniture(mFurniture))
		{
			return false;
		}
		if (!FurnitureInfo.UnlockCondition.IsMet(Services.Player))
		{
			return false;
		}
		if (!Services.Player.HasItems(FurnitureInfo.RepairCost))
		{
			return false;
		}
		return true;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotFurnitureCanRepairConditionNode tipDotFurnitureCanRepairConditionNode = new TipDotFurnitureCanRepairConditionNode();
		tipDotFurnitureCanRepairConditionNode.Fill(this);
		return tipDotFurnitureCanRepairConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotFurnitureCanRepairConditionNode tipDotFurnitureCanRepairConditionNode = source as TipDotFurnitureCanRepairConditionNode;
		if (tipDotFurnitureCanRepairConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotFurnitureCanRepairConditionNode);
	}

	public void Fill(TipDotFurnitureCanRepairConditionNode obj)
	{
		mFurniture = obj.mFurniture;
	}
}

}
