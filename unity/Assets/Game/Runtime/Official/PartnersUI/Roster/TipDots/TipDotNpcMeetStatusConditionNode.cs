// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using UnityEngine;
public class TipDotNpcMeetStatusConditionNode : TipDotConditionNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	[Header("任意NPC满足条件")]
	public bool IsAnyNpcMeet;

	public NpcMeetStatus MeetStatus;

	private NpcNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as NpcNodeInstanceInfo;

	public override void Init()
	{
		base.Init();
		Services.Player.OnAddNewNpc += MainPlayer_OnAddNewNpc;
		Services.Player.OnRemoveNpc += MainPlayer_OnRemoveNpc;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnAddNewNpc -= MainPlayer_OnAddNewNpc;
			Services.Player.OnRemoveNpc -= MainPlayer_OnRemoveNpc;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	private void MainPlayer_OnAddNewNpc(int npcId, NpcMeetType type)
	{
		SetDirty();
	}

	private void MainPlayer_OnRemoveNpc(int npcClassId)
	{
		SetDirty();
	}

	private void MainPlayer_OnItemCountUpdate(PartnerTipDotItem itemData, long preCount, object sourceData)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		if (IsAnyNpcMeet)
		{
			foreach (PartnerTipDotNpc npc in Services.MapExploreNpcs())
			{
				if (Services.GetNpcMeetStatus(Services.Player, npc.Id) == MeetStatus)
				{
					return true;
				}
			}
			return false;
		}
		if (InstanceInfo == null)
		{
			return false;
		}
		return Services.GetNpcMeetStatus(Services.Player, InstanceInfo.NpcId) == MeetStatus;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotNpcMeetStatusConditionNode tipDotNpcMeetStatusConditionNode = new TipDotNpcMeetStatusConditionNode();
		tipDotNpcMeetStatusConditionNode.Fill(this);
		return tipDotNpcMeetStatusConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotNpcMeetStatusConditionNode tipDotNpcMeetStatusConditionNode = source as TipDotNpcMeetStatusConditionNode;
		if (tipDotNpcMeetStatusConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotNpcMeetStatusConditionNode);
	}

	public void Fill(TipDotNpcMeetStatusConditionNode obj)
	{
		IsAnyNpcMeet = obj.IsAnyNpcMeet;
		MeetStatus = obj.MeetStatus;
	}
}

}
