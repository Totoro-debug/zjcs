using Zjcs.Official.Partners;

namespace Zjcs.Official.PartnersUI
{
    // NpcComparer.FriendshipPriorityList/GetScore with the original signed-long
    // subtraction from ItemComparerBase; reversing is not used by this page.
    public static class PartnerStarGodListRules
    {
        public static int Compare(NpcRosterItem x, NpcRosterItem y, bool canPin)
        {
            int result = Desc((long)x.MeetStatus, (long)y.MeetStatus);
            if (result != 0) return result;
            bool xp = canPin && x.IsCollected, yp = canPin && y.IsCollected;
            result = Desc(xp ? 1 : 0, yp ? 1 : 0);
            if (result != 0) return result;
            result = PartnerDialogueListRules.Compare(xp ? x.CollectedTimeTicks : 0,
                yp ? y.CollectedTimeTicks : 0, ComparerOrderType.Asc);
            if (result != 0) return result;
            result = Desc(x.NpcRoleType, y.NpcRoleType);
            if (result != 0) return result;
            result = Desc(x.FriendshipExp, y.FriendshipExp);
            if (result != 0) return result;
            result = Desc(x.ActivateTimeTicks, y.ActivateTimeTicks);
            return result != 0 ? result : Desc(x.NpcClassId, y.NpcClassId);
        }

        static int Desc(long x, long y) => PartnerDialogueListRules.Compare(x, y, ComparerOrderType.Desc);
    }
}
