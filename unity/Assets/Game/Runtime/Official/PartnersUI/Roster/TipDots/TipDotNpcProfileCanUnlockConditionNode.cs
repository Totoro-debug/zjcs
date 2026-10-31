// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
public class TipDotNpcProfileCanUnlockConditionNode : TipDotConditionNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	public bool mIsShowAllProfile;

	private List<PartnerTipDotProfile> mNpcProfileInfos = new List<PartnerTipDotProfile>();

	private NpcProfileNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as NpcProfileNodeInstanceInfo;

	public override void Init()
	{
		base.Init();
		mNpcProfileInfos.Clear();
		if (mIsShowAllProfile)
		{
			foreach (KeyValuePair<long, PartnerTipDotNpcRecord> item in Services.Player.NpcManager)
			{
				AddNpcProfileInfos(item.Value.NpcClassId);
			}
		}
		else if (NodeKey.InstanceInfo is NpcProfileNodeInstanceInfo npcProfileNodeInstanceInfo)
		{
			mNpcProfileInfos.Add(Services.GetNpcProfile(npcProfileNodeInstanceInfo.NpcId, npcProfileNodeInstanceInfo.NpcProfileId));
		}
		Services.Player.OnAddNewNpc += MainPlayer_OnAddNewNpc;
		Services.Player.OnRemoveNpc += MainPlayer_OnRemoveNpc;
		Services.Player.OnReceiveNpcProfileReward += MainPlayer_OnReceiveNpcProfileReward;
		Services.Player.OnNpcFriendshipLevelUpdate += MainPlayer_OnNpcFriendshipLevelUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnAddNewNpc -= MainPlayer_OnAddNewNpc;
			Services.Player.OnRemoveNpc -= MainPlayer_OnRemoveNpc;
			Services.Player.OnReceiveNpcProfileReward -= MainPlayer_OnReceiveNpcProfileReward;
			Services.Player.OnNpcFriendshipLevelUpdate -= MainPlayer_OnNpcFriendshipLevelUpdate;
		}
		base.Release();
	}

	private void AddNpcProfileInfos(int npcClassId)
	{
		List<PartnerTipDotProfile> npcProfileInfos = Services.GetNpcProfiles(npcClassId);
		if (npcProfileInfos == null)
		{
			return;
		}
		foreach (PartnerTipDotProfile item in npcProfileInfos)
		{
			if (!mNpcProfileInfos.Contains(item))
			{
				mNpcProfileInfos.Add(item);
			}
		}
	}

	private void MainPlayer_OnAddNewNpc(int npcClassId, NpcMeetType type)
	{
		if (mIsShowAllProfile)
		{
			AddNpcProfileInfos(npcClassId);
		}
		SetDirty();
	}

	private void MainPlayer_OnRemoveNpc(int npcClassId)
	{
		SetDirty();
	}

	private void MainPlayer_OnReceiveNpcProfileReward(int npcClassId, int profileId)
	{
		if (mIsShowAllProfile || InstanceInfo == null || npcClassId == InstanceInfo.NpcId)
		{
			SetDirty();
		}
	}

	private void MainPlayer_OnNpcFriendshipLevelUpdate(int npcClassId, int fromLevel, int toLevel)
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		IPartnerTipDotPlayer mainPlayer = Services.Player;
		if (mainPlayer.IsFunctionLimit(65))
		{
			return false;
		}
		foreach (PartnerTipDotProfile mNpcProfileInfo in mNpcProfileInfos)
		{
			PartnerTipDotNpcRecord byNpcClassId = mainPlayer.NpcManager.GetByNpcClassId(mNpcProfileInfo.Id);
			if (byNpcClassId != null && !byNpcClassId.ReceivedProfileRewards.Contains(mNpcProfileInfo.ProfileId) && mNpcProfileInfo.UnlockCondition.IsMet(mainPlayer))
			{
				return true;
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotNpcProfileCanUnlockConditionNode tipDotNpcProfileCanUnlockConditionNode = new TipDotNpcProfileCanUnlockConditionNode();
		tipDotNpcProfileCanUnlockConditionNode.Fill(this);
		return tipDotNpcProfileCanUnlockConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotNpcProfileCanUnlockConditionNode tipDotNpcProfileCanUnlockConditionNode = source as TipDotNpcProfileCanUnlockConditionNode;
		if (tipDotNpcProfileCanUnlockConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotNpcProfileCanUnlockConditionNode);
	}

	public void Fill(TipDotNpcProfileCanUnlockConditionNode obj)
	{
		mIsShowAllProfile = obj.mIsShowAllProfile;
	}
}

}
