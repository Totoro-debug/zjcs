using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using ProfileInfo = Zjcs.Official.Partners.PartnerProfileRules.ProfileInfo;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerProfileNpcData
    {
        int NpcClassId { get; }
        IReadOnlyCollection<int> ReceivedProfileRewards { get; }
    }

    public interface IPartnerProfileUiServices
    {
        List<ProfileInfo> GetNpcProfiles(int npcId);
        bool IsConditionMet(ProfileInfo info);
        string GetUnlockDescription(ProfileInfo info);
        ItemIcon GetIcon(int itemId, Transform slot);
        void RecycleIcon(ItemIcon icon);
        void ConfigureTip(TipDotNodeLinker linker);
        void ReceiveReward(int npcId, int profileId, Action success);
    }
}
