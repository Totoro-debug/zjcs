using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeCartWindowsUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeOutputDetailUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
using DetailGroup = Zjcs.Official.HomeOutputAddition.OutputGroupType;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    internal sealed class OfflineHomeCartWindows : IProfessionWindowService, IPartnerStaticHyperLinkServices, IDisposable
    {
        private sealed class Window
        {
            internal GameObject Root, Mask;
            internal IDisposable Binding;
            internal Coroutine Preparation;
            internal bool Closing;
        }

        private sealed class BlessingHyperLinkHandler : IPartnerStaticHyperLinkHandler
        {
            private readonly Action open;
            internal BlessingHyperLinkHandler(Action open) => this.open = open;
            public void Init(IWindowOperator owner) { }
            public void OnClick() => open?.Invoke();
        }

        private readonly HomeFeatureRuntime home;
        private readonly Canvas canvas;
        private readonly MonoBehaviour host;
        private readonly GameObject blurPrefab;
        private readonly IWorldWindowBlurManager blur;
        private readonly IScreenAdaptService screen;
        private readonly Action<AudioClip, GameObject> sound;
        private readonly OfflineHomeCartServices cartServices;
        private readonly OfflineHomeBedServices bedServices;
        private readonly ITopBarManager topBarManager;
        private readonly OfficialHomeCartWindowAssetCatalog cartAssets;
        private readonly OfficialHomeHangUpWindowAssetCatalog hangUpAssets;
        private readonly FontAssetFontService fonts;
        private readonly GameObject staging;
        private readonly List<Window> windows = new List<Window>();
        private MultiLanguageUIService multiLangUiService;
        private bool disposed;

        public event Action<GameObject> ActiveWindowChanged;
        public event Action<GameObject> WindowOpened;
        public FurnitureCartDialog Cart { get; private set; }
        public HangUpCartItemDetailDialog CartDetail { get; private set; }
        public HangUpLevelUpPanel LevelUpPanel { get; private set; }
        public FurnitureCartRuleDialog RuleDialog { get; private set; }
        public AwardPanel AwardPanelWindow { get; private set; }
        public OfflineHomeOutputDetailServices OutputDetails { get; private set; }
        internal bool HasWindows => windows.Count > 0;
        internal Camera CanvasCamera => canvas ? canvas.worldCamera : null;

        public OfflineHomeCartWindows(HomeFeatureRuntime home, Canvas canvas, MonoBehaviour host,
            GameObject blurPrefab, IWorldWindowBlurManager blur, IScreenAdaptService screen,
            Action<AudioClip, GameObject> sound, OfflineHomeCartServices cartServices,
            OfflineHomeBedServices bedServices, ITopBarManager topBarManager)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.canvas = canvas ? canvas : throw new ArgumentNullException(nameof(canvas));
            this.host = host ? host : throw new ArgumentNullException(nameof(host));
            this.blurPrefab = blurPrefab ? blurPrefab : throw new ArgumentNullException(nameof(blurPrefab));
            this.blur = blur ?? throw new ArgumentNullException(nameof(blur));
            this.screen = screen ?? throw new ArgumentNullException(nameof(screen));
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
            this.cartServices = cartServices ?? throw new ArgumentNullException(nameof(cartServices));
            this.bedServices = bedServices ?? throw new ArgumentNullException(nameof(bedServices));
            this.topBarManager = topBarManager;

            cartAssets = home.CartAssets;
            hangUpAssets = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
            fonts = OfflineHomeHudAssets.LoadDefault().CreateFontService();
            fonts.Initialize();
            staging = new GameObject("HomeCartInactiveBinding", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);

            var langService = home.NativeLanguageTypeService;
            if (langService != null)
                multiLangUiService = new MultiLanguageUIService(langService);
        }

        public void BindOutputDetails(OfflineHomeOutputAddition addition, IHomePointerSnapshot pointer = null, Func<DateTime> now = null)
        {
            if (OutputDetails != null) throw new InvalidOperationException("Cart output-detail services are already bound.");
            OutputDetails = new OfflineHomeOutputDetailServices(addition, (prefab, parent) => CloneObject(prefab, parent), Close, cartServices.Text, pointer, now);
        }

        public void Tick()
        {
            bedServices?.Tick();
            OutputDetails?.Tick();
        }

        public FurnitureCartDialog ShowCart()
        {
            bedServices.Output.Refresh();
            Create(cartAssets.cartDialogPrefab, root =>
            {
                Cart = root.GetComponent<FurnitureCartDialog>();
                Cart.Init(EFurniture.Cart);
            }, useBlur: true, closeOnMask: true);
            return Cart;
        }

        public HangUpCartItemDetailDialog ShowCartDetail(int hangUpId, Vector3 mousePosition)
        {
            bedServices.Output.Refresh();
            Create(cartAssets.itemDetailDialogPrefab, root =>
            {
                CartDetail = root.GetComponent<HangUpCartItemDetailDialog>();
                CartDetail.Init(hangUpId, mousePosition);
            }, useBlur: false, closeOnMask: false);
            return CartDetail;
        }

        public HangUpLevelUpPanel ShowHangUpLevelUp(int hangUpId, EFurniture furniture)
        {
            bedServices.Output.Refresh();
            Create(cartAssets.levelUpPanelPrefab, root =>
            {
                LevelUpPanel = root.GetComponent<HangUpLevelUpPanel>();
                LevelUpPanel.Init(hangUpId, furniture);
            }, useBlur: true, closeOnMask: true);
            return LevelUpPanel;
        }

        public FurnitureCartRuleDialog ShowCartRule()
        {
            Create(cartAssets.ruleDialogPrefab, root =>
            {
                RuleDialog = root.GetComponent<FurnitureCartRuleDialog>();
                RuleDialog.Init();
            }, useBlur: true, closeOnMask: true);
            return RuleDialog;
        }

        public void ShowOutputAddition(DetailGroup type)
        {
            if (OutputDetails == null) throw new InvalidOperationException("The saved output-addition owner is unbound.");
            Create(OfficialHomeOutputDetailAssets.LoadDefault().popup,
                root => root.GetComponent<PopTipOutputAddition>().Init(type), useBlur: false, closeOnMask: true);
        }

        public void ShowBlessingRule()
            => Create(hangUpAssets.blessingRulePrefab, root => root.GetComponent<BlessingLevelRuleDialog>().Init(), useBlur: true, closeOnMask: true);

        public AwardPanel ShowAwardPanel(IHomeHangUpAwardMessage response)
        {
            Create(cartAssets.awardPanelPrefab, root =>
            {
                AwardPanelWindow = root.GetComponent<AwardPanel>();
                AwardPanelWindow.Init(response, "AwardPanel.CommonTitle");
            }, useBlur: true, closeOnMask: false);
            return AwardPanelWindow;
        }

        public AwardPanel ShowAwardPanel(List<AwardData> awards)
        {
            Create(cartAssets.awardPanelPrefab, root =>
            {
                AwardPanelWindow = root.GetComponent<AwardPanel>();
                AwardPanelWindow.Init(awards, "AwardPanel.CommonTitle", true);
            }, useBlur: true, closeOnMask: false);
            return AwardPanelWindow;
        }

        internal GameObject CloneObject(GameObject prefab, Transform parent, bool keepWorld = false, bool activate = true)
        {
            var root = Object.Instantiate(prefab, staging.transform, false);
            root.SetActive(false);

            var defaultFont = OfflineHomeHudAssets.LoadDefault().defaultFont;
            if (defaultFont) fonts.SetupFont(defaultFont);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.font) continue;
                fonts.SetupFont(text.font);
                if (defaultFont && text.font != defaultFont)
                {
                    if (text.font.fallbackFontAssetTable == null)
                        text.font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!text.font.fallbackFontAssetTable.Contains(defaultFont))
                        text.font.fallbackFontAssetTable.Add(defaultFont);
                }
            }
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                text.Localizer = OutputDetails != null ? OutputDetails.Text : cartServices.Text;
            foreach (var area in root.GetComponentsInChildren<SafeArea>(true))
                area.ScreenAdaptService = screen;
            foreach (var button in root.GetComponentsInChildren<WorldButtonSound>(true))
                button.Post = sound;
            foreach (var popup in root.GetComponentsInChildren<WorldPopupWindowSound>(true))
                popup.Post = sound;
            foreach (var progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                progress.Compact = count => CultureNumberArg.Compact(count).ToString();

            if (multiLangUiService != null && home.NativeLanguageTypeService != null)
            {
                foreach (var ui in root.GetComponentsInChildren<MultiLanguageUI>(true))
                    ui.BindLanguageServices(multiLangUiService, home.NativeLanguageTypeService);
                foreach (var ctrl in root.GetComponentsInChildren<MultiLanguageGroupStateControler>(true))
                    ctrl.LanguageGroupConfig = home.NativeLanguageGroupConfig;
                foreach (var tmpCtrl in root.GetComponentsInChildren<MultiLanguageTMPControler>(true))
                {
                    tmpCtrl.LanguageGroupConfig = home.NativeLanguageGroupConfig;
                    tmpCtrl.LanguageTypeService = home.NativeLanguageTypeService;
                }
            }

            foreach (var dialog in root.GetComponentsInChildren<FurnitureCartDialog>(true))
                dialog.Services = cartServices;
            foreach (var item in root.GetComponentsInChildren<FurnitureCartHangUpItem>(true))
                item.Services = cartServices;
            foreach (var outputItem in root.GetComponentsInChildren<FurnitureCartHangUpOutputItem>(true))
                outputItem.Services = cartServices;
            foreach (var detail in root.GetComponentsInChildren<HangUpCartItemDetailDialog>(true))
                detail.Services = cartServices;
            foreach (var levelUp in root.GetComponentsInChildren<HangUpLevelUpPanel>(true))
                levelUp.Services = cartServices;
            foreach (var switchItem in root.GetComponentsInChildren<HangUpLevelUpSwitchItem>(true))
                switchItem.Services = cartServices;
            foreach (var ruleBtn in root.GetComponentsInChildren<FurnitureCartRuleButton>(true))
                ruleBtn.Services = cartServices;
            foreach (var ruleDialog in root.GetComponentsInChildren<FurnitureCartRuleDialog>(true))
                ruleDialog.Services = cartServices;
            foreach (var awardPanel in root.GetComponentsInChildren<AwardPanel>(true))
                awardPanel.Services = cartServices;
            foreach (var awardContent in root.GetComponentsInChildren<AwardContent>(true))
                awardContent.Services = cartServices;
            foreach (var awardItem in root.GetComponentsInChildren<AwardPanelItemBase>(true))
                awardItem.Services = cartServices;

            foreach (var quick in root.GetComponentsInChildren<FurnitureHangUpQuickReceive>(true))
                quick.Services = bedServices;
            foreach (var addition in root.GetComponentsInChildren<OutputAdditionDetailButton>(true))
                addition.Services = bedServices;
            foreach (var ruleButton in root.GetComponentsInChildren<BlessingRuleBtn>(true))
                ruleButton.Services = bedServices;
            foreach (var info in root.GetComponentsInChildren<FurnitureOutputInfo>(true))
                info.OutputServices = bedServices.Output;
            foreach (var rule in root.GetComponentsInChildren<BlessingLevelRuleDialog>(true))
                rule.Services = bedServices;
            foreach (var row in root.GetComponentsInChildren<BlessingLevelRuleItem>(true))
                row.Services = bedServices;

            foreach (var useContent in root.GetComponentsInChildren<PlayerUseItemContent>(true))
                useContent.Configure(cartServices.UpgradeBindings);
            if (ProfessionPropertyBindings.Current != null)
                foreach (var propItem in root.GetComponentsInChildren<ProfessionPlayerPropItem>(true))
                    propItem.Configure(ProfessionPropertyBindings.Current);
            foreach (var itemIcon in root.GetComponentsInChildren<ItemIcon>(true))
                itemIcon.Bind(cartServices.ItemIconContext);
            if (cartServices.ItemIconContext is IItemIconAttachmentManager attachments)
                foreach (var attachment in root.GetComponentsInChildren<ItemIconAttachment>(true))
                    attachment.Bind(attachments);
            foreach (var link in root.GetComponentsInChildren<StaticHyperLinkClickListener>(true))
                link.Configure(this);
            foreach (var pass in root.GetComponentsInChildren<UITouchPass>(true))
                pass.Configure(this);
            if (topBarManager != null)
                foreach (var topBar in root.GetComponentsInChildren<TopBarHandler>(true))
                    topBar.Bind(topBarManager);
            foreach (var clickSound in root.GetComponentsInChildren<UIClickSound>(true))
                if (clickSound.AkEvent != null)
                    clickSound.AkEvent.InstanceHost = cartServices.SoundHost;
            foreach (var ak in root.GetComponentsInChildren<AkEvent>(true))
                if (ak.data != null)
                    ak.data.InstanceHost = cartServices.SoundHost;

            foreach (var notice in root.GetComponentsInChildren<HomeNotice>(true))
                notice.Services = cartServices.FurnitureServices;
            foreach (var popup in root.GetComponentsInChildren<PopTipOutputAddition>(true))
                popup.Services = OutputDetails;
            foreach (var row in root.GetComponentsInChildren<OutputAdditionItem>(true))
                row.Services = OutputDetails;
            foreach (var row in root.GetComponentsInChildren<OutputAdditionPropItem>(true))
                row.Services = OutputDetails;
            if (OutputDetails != null)
                foreach (var timer in root.GetComponentsInChildren<ShowRemainTimeInfo>(true))
                    timer.Bind(OutputDetails);

            home.PartnerTipDots?.BindNativeTree(root);
            home.BindBagTipNativeTree(root);
            home.BindHomeTipNativeTree(root);
            if (home.NativeStorageStoryNodes != null)
            {
                foreach (var node in root.GetComponentsInChildren<Zjcs.Official.StoryNodes.StoryNodeRegister>(true))
                    node.NodeRegisterManager = home.NativeStorageStoryNodes;
                if (home.NativeStorageStoryNodes is IProfessionStoryNodeRegister profStoryNodes)
                    foreach (var node in root.GetComponentsInChildren<Zjcs.Official.ProfessionUI.StoryNodeRegister>(true))
                        node.Init(profStoryNodes);
            }

            foreach (var scroll in root.GetComponentsInChildren<LoopScrollRect>(true))
                if (scroll.DefaultPrefabSource != null)
                    scroll.DefaultPrefabSource.InstantiatePrefab = (template, container, world) => CloneObject(template, container, world);

            root.transform.SetParent(parent, keepWorld);
            if (activate && prefab.activeSelf) root.SetActive(true);
            return root;
        }

        private void Create(GameObject prefab, Action<GameObject> initialize, bool useBlur = true, bool closeOnMask = true)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeCartWindows));
            var window = new Window();
            windows.Add(window);
            try
            {
                window.Root = CloneObject(prefab, canvas.transform, activate: false);
                TMP_FontAsset originalFont = null;
                foreach (var text in window.Root.GetComponentsInChildren<TMP_Text>(true))
                    if (text.font) { originalFont = text.font; break; }
                window.Binding = OfflineUiBindingModule.Register(window.Root.transform, new OfflineUiBindings
                {
                    Localize = OutputDetails != null ? OutputDetails.Text : cartServices.Text,
                    Font = () => originalFont,
                    Images = _ => cartServices.ImageLoader,
                    Pool = (_, template, parent, world) => CloneObject(template, parent, world)
                });
                initialize(window.Root);

                var popup = window.Root.GetComponent<WorldPopupWindow>();
                if (popup && closeOnMask)
                    popup.SetMaskClickAction(() => Close(window.Root));
                if (!useBlur || !popup)
                {
                    ActiveWindowChanged?.Invoke(window.Root);
                    ShowPrepared(window, popup, null);
                    return;
                }

                window.Mask = CloneObject(blurPrefab, canvas.transform);
                var mask = window.Mask.GetComponent<WorldWindowBlurMask>();
                if (!mask || mask.MaskType != 3)
                    throw new InvalidOperationException("Selected cart popup requires its original blur mask.");
                mask.BlurManager = blur;
                ActiveWindowChanged?.Invoke(window.Root);
                window.Preparation = host.StartCoroutine(Prepare(window, popup, mask));
            }
            catch
            {
                Destroy(window);
                throw;
            }
        }

        private IEnumerator Prepare(Window window, WorldPopupWindow popup, WorldWindowBlurMask mask)
        {
            yield return mask.Prepare(popup, false);
            if (!windows.Contains(window)) yield break;
            ShowPrepared(window, popup, mask);
        }

        private void ShowPrepared(Window window, WorldPopupWindow popup, WorldWindowBlurMask mask)
        {
            window.Root.SetActive(true);
            foreach (var close in window.Root.GetComponentsInChildren<CloseWindowBtn>(true))
                close.Init(this);

            if (!popup)
            {
                int order = canvas.sortingOrder;
                foreach (var current in windows)
                {
                    if (current != window && current.Root && !current.Closing)
                    {
                        var currentPopup = current.Root.GetComponent<WorldPopupWindow>();
                        if (currentPopup) order = Math.Max(order, currentPopup.Canvas.sortingOrder);
                    }
                }
                var subCanvas = window.Root.GetComponent<Canvas>();
                if (subCanvas)
                {
                    subCanvas.overrideSorting = true;
                    subCanvas.sortingOrder = order + 10;
                }
                window.Root.transform.SetAsLastSibling();
                window.Preparation = null;
                WindowOpened?.Invoke(window.Root);
                return;
            }

            Action<WorldPopupWindow> showing = null;
            showing = shown =>
            {
                shown.OnShowing -= showing;
                int order = canvas.sortingOrder;
                foreach (var current in windows)
                {
                    if (current != window && current.Root && !current.Closing)
                    {
                        var currentPopup = current.Root.GetComponent<WorldPopupWindow>();
                        if (currentPopup) order = Math.Max(order, currentPopup.Canvas.sortingOrder);
                    }
                }
                shown.SetCanvasSortingOrder(order + 10);
                if (mask) mask.AttachToWindow(shown);
            };
            popup.OnShowing += showing;
            popup.Show();
            window.Preparation = null;
            WindowOpened?.Invoke(window.Root);
        }

        public void Close(GameObject root)
        {
            var window = windows.Find(value => value.Root == root);
            if (window == null || window.Closing) return;
            window.Closing = true;
            if (window.Preparation != null) { Destroy(window); return; }
            var popup = root ? root.GetComponent<WorldPopupWindow>() : null;
            if (popup) popup.Hide(() => Destroy(window));
            else Destroy(window);
        }

        private void Destroy(Window window)
        {
            if (!windows.Remove(window)) return;
            if (window.Preparation != null) host.StopCoroutine(window.Preparation);
            window.Preparation = null;
            if (window.Mask)
            {
                window.Mask.GetComponent<WorldWindowMask>().UnattachToWindow();
                window.Mask.SetActive(false);
                Object.Destroy(window.Mask);
            }
            if (window.Root)
            {
                if (Cart && Cart.gameObject == window.Root) Cart = null;
                if (CartDetail && CartDetail.gameObject == window.Root) CartDetail = null;
                if (LevelUpPanel && LevelUpPanel.gameObject == window.Root) LevelUpPanel = null;
                if (RuleDialog && RuleDialog.gameObject == window.Root) RuleDialog = null;
                if (AwardPanelWindow && AwardPanelWindow.gameObject == window.Root) AwardPanelWindow = null;
                window.Root.SetActive(false);
                Object.Destroy(window.Root);
            }
            window.Binding?.Dispose();
            ActiveWindowChanged?.Invoke(windows.Count == 0 ? null : windows[windows.Count - 1].Root);
        }

        public void ObserveDestroyed(WorldPopupWindow window, Action callback)
        {
            if (window) window.OnWindowDestory += callback;
        }

        public void CloseLast()
        {
            if (windows.Count > 0) Close(windows[windows.Count - 1].Root);
        }

        public void StopObservingDestroyed(WorldPopupWindow window, Action callback)
        {
            if (window) window.OnWindowDestory -= callback;
        }

        public PartnerStaticHyperLinkInfo GetHyperLinkInfo(int id)
        {
            if (id == 1000)
                return new PartnerStaticHyperLinkInfo { Id = id, HyperLinkType = 1, HyperLinkData = "blessing_rule" };
            return null;
        }

        public IPartnerStaticHyperLinkHandler CreateHandler(int type, string data)
        {
            if (type == 1 && data == "blessing_rule")
                return new BlessingHyperLinkHandler(ShowBlessingRule);
            return null;
        }

        public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 point)
        {
            var results = new List<RaycastResult>();
            if (EventSystem.current)
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            while (windows.Count > 0) Destroy(windows[windows.Count - 1]);
            multiLangUiService?.Dispose();
            multiLangUiService = null;
            fonts.Dispose();
            if (staging) Object.Destroy(staging);
            ActiveWindowChanged = null;
            WindowOpened = null;
        }
    }
}
