using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private OfflineHomeTopBar nativeCurrencyBar;
        private OfficialHomeTopBarAssetCatalog nativeCurrencyCatalog;
        private OfflineHomeCurrencyPlayer nativeCurrencyPlayer;
        private OfflineHomeCurrencyWindowPlayer nativeCurrencyWindowPlayer;
        private OfflineHomeCurrencyWindowServices nativeCurrencyServices;
        private OfflineHomeCurrencyWindows nativeCurrencyWindows;
        private OfflineHomePeriodicEvents nativeCurrencyPeriodic;
        private FontAssetFontService nativeCurrencyFonts;
        private PartnerWindowBlurCapture nativeCurrencyCapture;
        private bool NativeCurrencyModalOpen => nativeCurrencyWindows != null && nativeCurrencyWindows.WindowCount > 0;

        private void BuildNativeCurrencyBar()
        {
            try
            {
                var original = homePanel.transform.Find("NewPublicTopBarParent");
                if (!original) throw new InvalidOperationException("Original HomeUI currency parent is unavailable.");
                var parent = original.parent;
                int sibling = original.GetSiblingIndex();
                original.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(original.gameObject);
                var assets = OfflineHomeCurrencyAssets.LoadDefault();
                var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
                var itemRows = configuration.Rows("item").ToDictionary(row => Convert.ToInt32(row["ClassId"]));
                var hud = OfflineHomeHudAssets.LoadDefault();
                nativeCurrencyCatalog = assets.CreateTopBarCatalog();
                nativeCurrencyFonts = hud.CreateFontService(); nativeCurrencyFonts.Initialize();
                nativeCurrencyPlayer = new OfflineHomeCurrencyPlayer(host.Profile);
                var calendar = assets.CreateLocalQuickBuyCalendar();
                var counters = new OfflineHomeQuickBuyCounters(calendar);
                nativeCurrencyWindowPlayer = new OfflineHomeCurrencyWindowPlayer(host.Profile, nativeCurrencyPlayer,
                    counters, (id, count) => new NativeCurrencyItem(id, count, new NativeCurrencyInfo(JObject.FromObject(itemRows[id]))), CurrencyCostIsBanned);
                var icons = new NativeCurrencyIcons(this, assets, nativeCurrencyWindowPlayer);
                nativeCurrencyPeriodic = new OfflineHomePeriodicEvents(calendar, () => host.Profile.Save?.Player != null);
                nativeCurrencyPeriodic.Update(Time.time);
                var conditions = OfficialPartnerAssetCatalog.LoadDefault().Conditions;
                var conditionPlayer = new OfflineHomeConditionPlayer(configuration, () => host.Profile.Save, host.HasFurnitureForSkin);
                var rules = configuration.CreateConditions(conditions);
                var buy = new OfflineHomeMoneyQuickBuy(host.Profile, assets, calendar,
                    (node, save) => conditions.IsMet(node.ToObject<Dictionary<string, object>>(),
                        new OfflineHomeConditionPlayer(configuration, () => save, host.HasFurnitureForSkin)),
                    (_, id) => CurrencyCostIsBanned(id));
                OfflineHomeCurrencyActions actions = null;
                var barHost = new OfflineHomeCurrencyTopBarHost(nativeCurrencyPlayer, nativeCurrencyCatalog,
                    () => actions, () => Input.mousePosition);
                // Original MePlayerItemUI.OnEnable needs the complete window/action graph.
                nativeCurrencyBar = new OfflineHomeTopBar(parent, barHost, hud.defaultFont, assets.Text,
                    nativeCurrencyCatalog, activate: false);
                nativeCurrencyBar.Root.transform.SetSiblingIndex(sibling);
                foreach (var safe in nativeCurrencyBar.Root.GetComponentsInChildren<Zjcs.Official.HomeScreenUI.SafeArea>(true))
                    safe.ScreenAdaptService = nativeFrameScreen;
                var masks = Zjcs.Offline.PartnersUI.OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                nativeCurrencyCapture = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
                nativeCurrencyCapture.Initialize(masks.BlurMaterial);
                nativeCurrencyServices = new OfflineHomeCurrencyWindowServices(assets, nativeCurrencyWindowPlayer,
                    nativeCurrencyPlayer, buy, new OfflineHomeCurrencyExchange(host.Profile, assets),
                    new NativeCurrencyPresentation(this, assets), kind => kind + ":" + Guid.NewGuid().ToString("N"));
                nativeCurrencyWindows = new OfflineHomeCurrencyWindows(new OfflineHomeCurrencyWindowBindings {
                    Canvas = canvas, CoroutineHost = host, TopBar = nativeCurrencyBar.Manager,
                    BlurMaskPrefab = masks.BlurMaskPrefab, BlurManager = nativeCurrencyCapture,
                    Fonts = nativeCurrencyFonts, Screen = nativeFrameScreen, Icons = icons,
                    PostSound = PostPartnerPromptSound }, nativeCurrencyServices, assets);
                nativeCurrencyServices.AttachWindows(nativeCurrencyWindows);
                actions = new OfflineHomeCurrencyActions(host.Profile, nativeCurrencyPlayer, assets, nativeCurrencyWindows,
                    node => conditions.IsMet(node.ToObject<Dictionary<string, object>>(), conditionPlayer),
                    () => configuration.Rows("function_limit").Where(row => Convert.ToInt32(row["Type"]) == 49
                        && Convert.ToInt32(row["Key0"]) == 0 && Convert.ToInt32(row["Key1"]) == 0)
                        .Any(row => !rules.IsMet((Dictionary<string, object>)row["Condition"], conditionPlayer)),
                    ShowNativeCurrencyDetail, () => Notify("离线模式不开放充值"));
                nativeCurrencyBar.Root.SetActive(true);
            }
            catch { ReleaseNativeCurrencyBar(); throw; }
        }

        // Player.IsBanCostItem reads BanUseItemsInSelf / PlanesBanItems. Neither
        // remote restriction source has records in the migrated home scope.
        private static bool CurrencyCostIsBanned(int itemId) => false;
        private void ShowNativeCurrencyDetail(int itemId, long count) => Notify("货币详情页面尚未迁移");

        private void ReleaseNativeCurrencyBar()
        {
            nativeCurrencyWindows?.Dispose(); nativeCurrencyWindows = null;
            nativeCurrencyBar?.Dispose(); nativeCurrencyBar = null;
            nativeCurrencyServices?.Dispose(); nativeCurrencyServices = null;
            nativeCurrencyWindowPlayer?.Dispose(); nativeCurrencyWindowPlayer = null;
            nativeCurrencyPlayer?.Dispose(); nativeCurrencyPlayer = null;
            nativeCurrencyPeriodic?.Dispose(); nativeCurrencyPeriodic = null;
            if (nativeCurrencyCapture) UnityEngine.Object.Destroy(nativeCurrencyCapture);
            nativeCurrencyCapture = null;
            nativeCurrencyFonts?.Dispose(); nativeCurrencyFonts = null;
            if (nativeCurrencyCatalog) UnityEngine.Object.Destroy(nativeCurrencyCatalog);
            nativeCurrencyCatalog = null;
        }

        private sealed class NativeCurrencyPresentation : IOfflineHomeCurrencyWindowPresentation
        {
            private readonly HomeHudView view;
            private readonly OfflineHomeCurrencyAssets assets;
            private readonly List<Func<HomeCurrencyAwardMessage, bool>> awardHandlers = new List<Func<HomeCurrencyAwardMessage, bool>>();
            public NativeCurrencyPresentation(HomeHudView view, OfflineHomeCurrencyAssets assets)
            { this.view = view; this.assets = assets; }
            public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> listener)
                => view.nativeCurrencyPeriodic.SubscribeEnterNewDay(listener);
            public void ShowLackOfItem(int itemId, long count, IWindowOperator owner)
                => view.Notify(assets.Item(itemId).Name + "不足");
            public void ShowError(int error, string itemName) => view.Notify(assets.Text(error.ToString()).Replace("{0}", itemName));
            public void SystemMessage(string text) => view.Notify(text);
            public void PopupItemDetail(IWindowOperator owner, int itemId, long count) => view.ShowNativeCurrencyDetail(itemId, count);
            public void PopupDirectPay(IWindowOperator owner, Action initialized) => view.Notify("离线模式不开放充值");
            public void TransactionFailed(int itemId, ProgressionResult result) => view.Notify("购买未完成：" + result.Message);
            // Local inventory transactions issue no remote pay-award messages.
            public bool AwardServiceAvailable => true;
            public void RegisterAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) => awardHandlers.Add(handler);
            public void RemoveAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) => awardHandlers.Remove(handler);
            public bool IsPayAward(HomeCurrencyAwardMessage message) => false;
            public void PopupAward(HomeCurrencyAwardMessage message) => throw new NotSupportedException("Remote pay awards are unavailable offline.");
        }
    }
}
