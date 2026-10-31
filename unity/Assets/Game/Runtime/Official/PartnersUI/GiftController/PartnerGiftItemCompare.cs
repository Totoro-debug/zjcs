using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // NpcGiftItemCompare + the four consumed ItemComparerBase score branches.
    internal sealed class PartnerGiftItemCompare
    {
        private readonly PartnerGiftNpcInfo npc;
        private readonly IPartnerGiftUiServices services;
        public PartnerGiftItemCompare(PartnerGiftNpcInfo npc, IPartnerGiftUiServices services)
        { this.npc = npc; this.services = services; }
        private static int Result(long difference) => difference == 0 ? 0 : difference <= 0 ? -1 : 1;
        public int CompareShowGift(IProfessionItemData x, IProfessionItemData y) => Compare(x, y, false);
        public int CompareSelectGift(IProfessionItemData x, IProfessionItemData y) => Compare(x, y, true);
        private int Compare(IProfessionItemData x, IProfessionItemData y, bool select)
        {
            int result = Result(Convert.ToInt32(npc.GiftPreferenceDict.ContainsKey(y.Id)) - Convert.ToInt32(npc.GiftPreferenceDict.ContainsKey(x.Id)));
            if (result != 0) return result;
            long first = (long)x.GetQuality(), second = (long)y.GetQuality();
            result = Result(select ? first - second : second - first);
            if (result != 0) return result;
            first = services.GetGiftInfo(x.Info).Friendship;
            second = services.GetGiftInfo(y.Info).Friendship;
            result = Result(select ? first - second : second - first);
            if (result != 0 || !select) return result;
            return Result((long)y.Id - x.Id);
        }
    }
}
