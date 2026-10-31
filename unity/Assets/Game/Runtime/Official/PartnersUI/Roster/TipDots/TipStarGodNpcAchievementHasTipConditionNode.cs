// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
public class TipStarGodNpcAchievementHasTipConditionNode : TipDotAchievementConditionNode, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	private Dictionary<int, ProfessionTipDotAchievementKey> mAchievementDataKeyDict = new Dictionary<int, ProfessionTipDotAchievementKey>();

	public override void Init()
    {
        base.Init();
        mAchievementDataKeyDict.Clear();
        if (!(NodeKey.InstanceInfo is NpcNodeInstanceInfo instanceInfo))
        {
            UnityEngine.Debug.LogError("TipStarGodNpcAchievementHasTipConditionNode: NpcNodeInstanceInfo required");
            return;
        }
        var npcInfo = Services.GetNpc(instanceInfo.NpcId);
        if (npcInfo == null)
            UnityEngine.Debug.LogError("TipStarGodNpcAchievementHasTipConditionNode: missing NPC " + instanceInfo.NpcId);
        else if (npcInfo.IsStarGodNpc)
        {
            var key = Services.NpcFriendshipKey(instanceInfo.NpcId, true);
            mAchievementDataKeyDict.Add(instanceInfo.NpcId, key);
            AddDataKey(key);
        }
    }

	public override TipDotNodeBase Clone()
	{
		TipStarGodNpcAchievementHasTipConditionNode tipStarGodNpcAchievementHasTipConditionNode = new TipStarGodNpcAchievementHasTipConditionNode();
		tipStarGodNpcAchievementHasTipConditionNode.Fill(this);
		return tipStarGodNpcAchievementHasTipConditionNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipStarGodNpcAchievementHasTipConditionNode tipStarGodNpcAchievementHasTipConditionNode = source as TipStarGodNpcAchievementHasTipConditionNode;
		if (tipStarGodNpcAchievementHasTipConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipStarGodNpcAchievementHasTipConditionNode);
	}

	public void Fill(TipStarGodNpcAchievementHasTipConditionNode obj)
	{
		AchievementTypeAndParamList = obj.AchievementTypeAndParamList?.ConvertAll(value => value?.Clone());
	}
}

}
