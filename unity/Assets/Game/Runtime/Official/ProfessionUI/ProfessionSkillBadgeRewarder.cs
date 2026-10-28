namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;

public class ProfessionSkillBadgeRewarder
{
    private IProfessionDetailBindings mBindings;
    private IProfessionDetailBindings Bindings => mBindings ?? ProfessionDetailBindings.Required;
    public void Configure(IProfessionDetailBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	private int mProfession;

	private List<(int itemId, int rank)> mItemWrapInfos;

	private List<int> mSkillIds;

	private UIBase mContainer;

	public void Init(int profession, UIBase container)
	{
		mProfession = profession;
		mContainer = container;
		if (mItemWrapInfos == null)
		{
			mItemWrapInfos = new List<(int, int)>();
		}
		if (mSkillIds == null)
		{
			mSkillIds = new List<int>();
		}
		ClaimSkillRankReward();
	}

	private void ClaimSkillRankReward()
	{
		mSkillIds.Clear();
		mItemWrapInfos.Clear();
        IProfessionDetailPlayer player = Bindings.MainPlayer;
        IReadOnlyCollection<IProfessionDetailSkill> readOnlyCollection = player.FindSkillItems();
        ProfessionDetailDefinition professionBaseInfo = Bindings.GetProfession(mProfession);
		int amount = 0;
		int badgeId = professionBaseInfo.BadgeClassId;
		foreach (IProfessionDetailSkill item in readOnlyCollection)
		{
			if (!professionBaseInfo.Skills.Contains(item.Id))
			{
				continue;
			}
			int rank = item.GetRank();
			int lastRewardedSkillRank = player.GetLastRewardedSkillRank(item.Id);
			amount += Bindings.GetRankBadge(rank);
			amount -= Bindings.GetRankBadge(lastRewardedSkillRank);
			if (lastRewardedSkillRank < rank)
			{
				mSkillIds.Add(item.Id);
				for (int i = lastRewardedSkillRank + 1; i <= rank; i++)
				{
					mItemWrapInfos.Add((item.Id, i));
				}
			}
		}
		mItemWrapInfos.Sort(SkillRankComparer);
		if (mItemWrapInfos.Count <= 0)
		{
			return;
		}
        Bindings.ClaimSkillRankReward(mSkillIds, ret =>
        {
            Bindings.PopupSkillBadge(mContainer, mItemWrapInfos, badgeId, amount);
        });
	}

	private int SkillRankComparer((int itemId, int rank) skillItemA, (int itemId, int rank) skillItemB)
	{
		bool flag = skillItemA.rank > skillItemB.rank;
		if (skillItemA.rank == skillItemB.rank)
		{
			flag = skillItemA.itemId > skillItemB.itemId;
		}
		if (!flag)
		{
			return 1;
		}
		return -1;
	}
}

}
