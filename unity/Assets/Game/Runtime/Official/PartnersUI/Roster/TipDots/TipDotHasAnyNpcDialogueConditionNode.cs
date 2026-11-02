// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;

public class TipDotHasAnyNpcDialogueConditionNode : TipDotConditionNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public override void Init()
	{
		base.Init();
		Services.Player.OnNpcDialogueAdd += MainPlayer_OnNpcDialogueAdd;
		Services.Player.OnNpcDialogueUpdate += MainPlayer_OnNpcDialogueUpdate;
		Services.Player.OnNpcDialogueRemove += MainPlayer_OnNpcDialogueRemove;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnNpcDialogueAdd -= MainPlayer_OnNpcDialogueAdd;
			Services.Player.OnNpcDialogueUpdate -= MainPlayer_OnNpcDialogueUpdate;
			Services.Player.OnNpcDialogueRemove -= MainPlayer_OnNpcDialogueRemove;
		}
		base.Release();
	}

	private void MainPlayer_OnNpcDialogueUpdate(object obj)
	{
		SetDirty();
	}

	private void MainPlayer_OnNpcDialogueAdd(object obj)
	{
		SetDirty();
	}

	private void MainPlayer_OnNpcDialogueRemove(object obj)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		if (Services.Player.HasAnyNpcDialogueRedDot())
		{
			return true;
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHasAnyNpcDialogueConditionNode tipDotHasAnyNpcDialogueConditionNode = new TipDotHasAnyNpcDialogueConditionNode();
		tipDotHasAnyNpcDialogueConditionNode.Fill(this);
		return tipDotHasAnyNpcDialogueConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHasAnyNpcDialogueConditionNode tipDotHasAnyNpcDialogueConditionNode = source as TipDotHasAnyNpcDialogueConditionNode;
		if (tipDotHasAnyNpcDialogueConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHasAnyNpcDialogueConditionNode);
	}

	public void Fill(TipDotHasAnyNpcDialogueConditionNode obj)
	{
	}
}

}
