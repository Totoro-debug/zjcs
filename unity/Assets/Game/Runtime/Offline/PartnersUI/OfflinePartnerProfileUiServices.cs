using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using ProfileInfo = Zjcs.Official.Partners.PartnerProfileRules.ProfileInfo;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerProfilePresentation
    {
        ItemIcon GetIcon(int itemId, Transform slot);
        void RecycleIcon(ItemIcon icon);
        void ConfigureTip(TipDotNodeLinker linker);
        bool ContainsText(string key);
        string FormatText(string key, object value);
        void OnProfileCommitted();
        void ShowFailure(ProgressionResult result);
    }

    public sealed class OfflinePartnerProfileUiServices : IPartnerProfileUiServices
    {
        private readonly OfflinePartnerProfileService profile;
        private readonly IOfflinePartnerProfilePresentation presentation;
        public OfflinePartnerProfileUiServices(OfflinePartnerProfileService profile, IOfflinePartnerProfilePresentation presentation)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }
        public IPartnerProfileNpcData GetNpcData(int npcId) => new NpcData(profile, npcId);
        public List<ProfileInfo> GetNpcProfiles(int npcId) => profile.Rules.GetNpcProfileInfos(npcId);
        public bool IsConditionMet(ProfileInfo info) => info.UnlockCondition.IsMet(profile);
        public string GetUnlockDescription(ProfileInfo info) => info.UnlockCondition.GetUnlockDescription(presentation.ContainsText, presentation.FormatText);
        public ItemIcon GetIcon(int itemId, Transform slot) => presentation.GetIcon(itemId, slot);
        public void RecycleIcon(ItemIcon icon) => presentation.RecycleIcon(icon);
        public void ConfigureTip(TipDotNodeLinker linker) => presentation.ConfigureTip(linker);
        public void ReceiveReward(int npcId, int profileId, Action success)
        {
            var result = profile.Claim(npcId, profileId);
            if (result.IsApplied) { presentation.OnProfileCommitted(); success?.Invoke(); }
            else if (result.Status == ProgressionResultStatus.Unchanged) success?.Invoke();
            else presentation.ShowFailure(result);
        }
        private sealed class NpcData : IPartnerProfileNpcData
        {
            private readonly OfflinePartnerProfileService owner;
            public NpcData(OfflinePartnerProfileService owner, int npcId) { this.owner = owner; NpcClassId = npcId; }
            public int NpcClassId { get; }
            public IReadOnlyCollection<int> ReceivedProfileRewards => owner.ReceivedProfileRewards(NpcClassId);
        }
    }
}
