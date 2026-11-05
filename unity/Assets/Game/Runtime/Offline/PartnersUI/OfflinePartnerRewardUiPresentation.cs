using System;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerRewardUiPresentation
    {
        private readonly OfflinePartnerGiftRuntimeBindings icons;
        private readonly OfficialPartnerProfileAssetCatalog profileAssets;
        private readonly OfficialPartnerAchievementAssetCatalog achievementAssets;
        private readonly Func<string, string> localize;
        private readonly Func<string, bool> contains;
        private readonly Action<TipDotNodeLinker> bindTip;
        private readonly Action committed;
        private readonly Action<ProgressionResult> failure;
        private readonly Action<string> message;
        public IOfflinePartnerProfilePresentation Profile { get; }
        public IOfflinePartnerAchievementPresentation Achievement { get; }
        public OfflinePartnerRewardUiPresentation(OfflinePartnerGiftRuntimeBindings icons,
            OfficialPartnerProfileAssetCatalog profileAssets, OfficialPartnerAchievementAssetCatalog achievementAssets,
            Func<string, string> localize, Func<string, bool> contains, Action<TipDotNodeLinker> bindTip,
            Action committed, Action<ProgressionResult> failure, Action<string> message)
        {
            this.icons = icons ?? throw new ArgumentNullException(nameof(icons));
            this.profileAssets = profileAssets ? profileAssets : throw new ArgumentNullException(nameof(profileAssets));
            this.achievementAssets = achievementAssets ? achievementAssets : throw new ArgumentNullException(nameof(achievementAssets));
            this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
            this.contains = contains ?? throw new ArgumentNullException(nameof(contains));
            this.bindTip = bindTip ?? throw new ArgumentNullException(nameof(bindTip));
            this.committed = committed ?? throw new ArgumentNullException(nameof(committed));
            this.failure = failure ?? throw new ArgumentNullException(nameof(failure));
            this.message = message ?? throw new ArgumentNullException(nameof(message));
            Profile = new ProfilePresentation(this); Achievement = new AchievementPresentation(this);
        }
        private static void Recycle(ItemIcon icon)
        {
            if (!icon) return;
            icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject);
        }
        private sealed class ProfilePresentation : IOfflinePartnerProfilePresentation
        {
            private readonly OfflinePartnerRewardUiPresentation owner;
            internal ProfilePresentation(OfflinePartnerRewardUiPresentation value) { owner = value; }
            public ItemIcon GetIcon(int itemId, Transform slot) => owner.icons.CreateGiftIcon(itemId, slot, owner.profileAssets.RewardIconPrefab);
            public void RecycleIcon(ItemIcon icon) => Recycle(icon);
            public void ConfigureTip(TipDotNodeLinker linker) => owner.bindTip(linker);
            public bool ContainsText(string key) => owner.contains(key);
            public string FormatText(string key, object value) => string.Format(owner.localize(key), value);
            public void OnProfileCommitted() => owner.committed();
            public void ShowFailure(ProgressionResult result) => owner.failure(result);
        }
        private sealed class AchievementPresentation : IOfflinePartnerAchievementPresentation
        {
            private readonly OfflinePartnerRewardUiPresentation owner;
            internal AchievementPresentation(OfflinePartnerRewardUiPresentation value) { owner = value; }
            public ItemIcon GetIcon(int itemId, Transform parent) => owner.icons.CreateGiftIcon(itemId, parent, owner.achievementAssets.RewardIconPrefab);
            public void RecycleIcon(ItemIcon icon) => Recycle(icon);
            public string Localize(string key) => owner.localize(key);
            public void ShowSystemMessage(string key) => owner.message(key);
            public void ShowFailure(ProgressionResult result) => owner.failure(result);
            public void OnAchievementCommitted() => owner.committed();
        }
    }
}
