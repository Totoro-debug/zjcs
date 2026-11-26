using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.GachaUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.GachaUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private PetScoreDialogContext petNativeScore;
        private void OpenNativePetScoreDialog(bool? completed = null, ScoreRewardKey? key = null, GachaTextKeyArgWrap? rule = null)
        {
            CloseNativePetScoreDialog();
            var context = new PetScoreDialogContext(this);
            var dataKey = key ?? new ScoreRewardKey(host.PetGachaCatalog.Gacha.ScoreAchievementType, 16);
            var text = rule ?? new GachaTextKeyArgWrap(host.PetGachaCatalog.Gacha.ScoreRuleText,
                host.PetGachaCatalog.Chest.Rewards.First(row => row.ItemId == host.PetGachaCatalog.Gacha.RewardScoreId).Count);
            context.Rewards.TryGetGotRewardMaxIndex(dataKey, out int got);
            bool all = completed ?? got + 1 >= host.PetGacha.AchievementCount(dataKey.GroupId);
            petNativeScore = context;
            nativeCurrencyWindows.WindowClosed += context.OnClosed;
            try { context.Root = nativeCurrencyWindows.Popup(context.Assets.Prefab, context.Assets.Text,
                root => context.Initialize(root, all, dataKey, text)); }
            catch { context.Dispose(); if (ReferenceEquals(petNativeScore, context)) petNativeScore = null; throw; }
        }
        private void CloseNativePetScoreDialog()
        {
            if (petNativeScore?.Root) nativeCurrencyWindows?.Close(petNativeScore.Root);
            petNativeScore = null;
        }
        private sealed class PetScoreDialogContext : IOfflineScoreRewardPresentation, IDisposable
        {
            private readonly HomeHudView view;
            private readonly OfflineProfessionImageCatalog images;
            private readonly OfflineProfessionImageResourceLoader loader;
            private readonly OfflinePetAchievementPanelSource source;
            private IDisposable binding;
            private bool disposed;
            public GameObject Root;
            public readonly OfficialScoreRewardDialogAssetCatalog Assets;
            public readonly OfflinePetScoreRewardServices Rewards;
            public PetScoreDialogContext(HomeHudView view)
            {
                this.view = view;
                Assets = OfficialScoreRewardDialogAssetCatalog.LoadDefault();
                var home = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
                var player = new OfflineHomeConditionPlayer(home, () => view.host.Profile.Save, view.host.HasFurnitureForSkin);
                var configuration = new OfflineScoreRewardConfiguration(home, OfficialPartnerAssetCatalog.LoadDefault().Conditions, () => player);
                images = HomeFeatureRuntime.BuildPartnerImageCatalog(
                    Assets.RewardImages.Select(row => new OfflineProfessionImageCatalog.Entry {
                        Path = row.path, AssetGuid = row.assetGuid, Asset = row.sprite }).ToArray(),
                    OfficialPartnerProfileAssetCatalog.Load().Common.Images.Entries,
                    Resources.Load<OfflineProfessionImageCatalog>("Home/OfflineHomeTaskRewardAttachments").Entries);
                loader = new OfflineProfessionImageResourceLoader(images);
                Rewards = new OfflinePetScoreRewardServices(view.host.PetGacha,
                    configuration.BatchAchievementTypes, () => configuration.IsBatchLimited, this);
                source = new OfflinePetAchievementPanelSource(view.host.PetGacha, Rewards, configuration);
            }
            public void Initialize(GameObject root, bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule)
            {
                binding = OfflineUiBindingModule.Register(root.transform, new OfflineUiBindings {
                    Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont, Images = _ => loader,
                    Pool = (scope, prefab, parent, keepWorld) => UnityEngine.Object.Instantiate(prefab, parent, keepWorld)
                });
                var dialog = root.GetComponent<ScoreRewardDialog>();
                if (!dialog || !dialog.AchievementPanel || !dialog.ScoreRuleText)
                    throw new InvalidOperationException("Original score reward dialog controller or fields are unbound.");
                dialog.AchievementPanel.Configure(source);
                dialog.Init(completed, key, rule);
            }
            public ItemIcon GetIcon(int item, Transform parent)
            {
                var icon = view.host.PartnerGiftBindings.CreateGiftIcon(item, parent, OfficialPartnerProfileAssetCatalog.Load().RewardIconPrefab);
                foreach (var text in icon.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = Assets.Text;
                if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
                return icon;
            }
            public void RecycleIcon(ItemIcon icon)
            {
                view.host.PartnerGiftBindings.RecycleIcon(icon);
                icon.gameObject.SetActive(false); UnityEngine.Object.Destroy(icon.gameObject);
            }
            public void ShowSystemMessage(string key) => view.Notify(Assets.Text(key));
            public void ShowFailure(PetGachaResult result) => view.Notify(result.Message);
            public void OnAchievementCommitted() => view.host.RefreshCounts();
            public void PopupScoreRewardDialog(string path, bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule)
            {
                if (!string.IsNullOrEmpty(path)) throw new NotSupportedException("Unbound custom score reward dialog.");
                view.OpenNativePetScoreDialog(completed, key, rule);
            }
            public void OnClosed(GameObject root)
            {
                if (root != Root) return;
                Dispose();
                if (ReferenceEquals(view.petNativeScore, this)) view.petNativeScore = null;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                view.nativeCurrencyWindows.WindowClosed -= OnClosed;
                source.Dispose(); Rewards.Dispose(); binding?.Dispose();
                UnityEngine.Object.Destroy(images);
            }
        }
    }
}
