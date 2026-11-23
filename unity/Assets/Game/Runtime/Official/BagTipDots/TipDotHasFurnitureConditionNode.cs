// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotHasFurnitureConditionNode : TipDotConditionNodeBase, IBagTipDotNode
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
		if (Services.Player != null)
		{
			DisposeRegistration();
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
		return Services.Player.HasFurniture(mFurniture);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHasFurnitureConditionNode tipDotHasFurnitureConditionNode = new TipDotHasFurnitureConditionNode();
		tipDotHasFurnitureConditionNode.Fill(this);
		return tipDotHasFurnitureConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHasFurnitureConditionNode tipDotHasFurnitureConditionNode = source as TipDotHasFurnitureConditionNode;
		if (tipDotHasFurnitureConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHasFurnitureConditionNode);
	}

	public void Fill(TipDotHasFurnitureConditionNode obj)
	{
		mFurniture = obj.mFurniture;
	}
}

}
