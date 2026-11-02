// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;

public class TipDotHasFurnitureConditionNode : TipDotConditionNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }

	public int mFurniture;

	private PartnerPanelFurnitureInfo mFurnitureInfo;

	private bool mRegister;

	private PartnerPanelFurnitureInfo FurnitureInfo => mFurnitureInfo ?? (mFurnitureInfo = Services.GetFurniture(mFurniture));

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
				Services.RegisterCondition(FurnitureInfo.UnlockCondition, SetDirty);
				mRegister = true;
			}
			Services.Player.OnRepairFurniture += Player_OnRepairFurniture;
		}
	}

	private void RemoveEventListener()
	{
		if (Services.Player != null)
		{
			if (mRegister && Services.Player != null)
			{
				Services.DeregisterCondition(FurnitureInfo.UnlockCondition, SetDirty);
			}
			Services.Player.OnRepairFurniture -= Player_OnRepairFurniture;
		}
	}

	private void Player_OnRepairFurniture(int furniture)
	{
		if (mFurniture == furniture)
		{
			SetDirty();
			if (mRegister && Services.Player != null)
			{
				Services.DeregisterCondition(FurnitureInfo.UnlockCondition, SetDirty);
			}
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
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHasFurnitureConditionNode);
	}

	public void Fill(TipDotHasFurnitureConditionNode obj)
	{
		mFurniture = obj.mFurniture;
	}
}

}
