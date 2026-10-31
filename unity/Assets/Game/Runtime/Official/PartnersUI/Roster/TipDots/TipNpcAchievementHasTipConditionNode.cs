// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
public class TipNpcAchievementHasTipConditionNode : TipDotAchievementConditionNode, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	public bool IsAllNpc;

	private Dictionary<int, ProfessionTipDotAchievementKey> mAchievementDataKeyDict = new Dictionary<int, ProfessionTipDotAchievementKey>();

	public override void Init()
	{
		base.Init();
		mAchievementDataKeyDict.Clear();
		if (IsAllNpc)
		{
			foreach (KeyValuePair<long, PartnerTipDotNpcRecord> item in Services.Player.NpcManager)
			{
				PartnerTipDotNpcRecord value = item.Value;
				ProfessionTipDotAchievementKey achievementDataKeyNpcFriendshipLevel = Services.NpcFriendshipKey(value.NpcClassId, false);
				mAchievementDataKeyDict.Add(value.NpcClassId, achievementDataKeyNpcFriendshipLevel);
				AddDataKey(achievementDataKeyNpcFriendshipLevel);
			}
		}
		else if (NodeKey.InstanceInfo is NpcNodeInstanceInfo npcNodeInstanceInfo)
		{
			ProfessionTipDotAchievementKey achievementDataKeyNpcFriendshipLevel2 = Services.NpcFriendshipKey(npcNodeInstanceInfo.NpcId, false);
			mAchievementDataKeyDict.Add(npcNodeInstanceInfo.NpcId, achievementDataKeyNpcFriendshipLevel2);
			AddDataKey(achievementDataKeyNpcFriendshipLevel2);
		}
		Services.Player.OnAddNewNpc += MainPlayer_OnAddNewNpc;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnAddNewNpc -= MainPlayer_OnAddNewNpc;
		}
		base.Release();
	}

	private void MainPlayer_OnAddNewNpc(int npcClassId, NpcMeetType type)
	{
		if (IsAllNpc && !mAchievementDataKeyDict.ContainsKey(npcClassId))
		{
			ProfessionTipDotAchievementKey achievementDataKeyNpcFriendshipLevel = Services.NpcFriendshipKey(npcClassId, false);
			mAchievementDataKeyDict.Add(npcClassId, achievementDataKeyNpcFriendshipLevel);
			AddDataKey(achievementDataKeyNpcFriendshipLevel);
			SetDirty();
		}
	}

	public override TipDotNodeBase Clone()
	{
		TipNpcAchievementHasTipConditionNode tipNpcAchievementHasTipConditionNode = new TipNpcAchievementHasTipConditionNode();
		tipNpcAchievementHasTipConditionNode.Fill(this);
		return tipNpcAchievementHasTipConditionNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipNpcAchievementHasTipConditionNode tipNpcAchievementHasTipConditionNode = source as TipNpcAchievementHasTipConditionNode;
		if (tipNpcAchievementHasTipConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipNpcAchievementHasTipConditionNode);
	}

	public void Fill(TipNpcAchievementHasTipConditionNode obj)
	{
		IsAllNpc = obj.IsAllNpc;
		AchievementTypeAndParamList = obj.AchievementTypeAndParamList?.ConvertAll(value => value?.Clone());
	}
}

}
