using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Main-city HUD rebuilt from the official panels: top bar, avatar, five-tab
    /// bottom bar, function menu and the furniture/mail windows.
    /// </summary>
    public sealed partial class HomeHudView
    {
        private static readonly string[] CurrencyIcons = { "Money_1", "Money_2", "Money_15" };
        private static readonly int[] CurrencyItems = { 1, 2, 15 };

        private readonly HomeFeatureRuntime host;
        private readonly HomeAssetCatalog catalog;
        private readonly Transform canvasParent;
        private readonly List<HomeUiRenderer> owned = new List<HomeUiRenderer>();
        private readonly List<GameObject> dialogs = new List<GameObject>();
        private Canvas canvas;
        private GameObject homePanel;
        private HomeUiRenderer homeRenderer;
        private HomeUiRenderer topRenderer;
        private HomeUiRenderer bottomRenderer;
        private HomeUiRenderer functionsRenderer;
        private GameObject functionsPanel;

        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI levelLabel;
        private TextMeshProUGUI toast;
        private GameObject noticeRoot;
        private int selectedTab;
        private GameObject activeDialog;
        private HomeUiRenderer activeDialogRenderer;
        private string activePanelKey;
        private bool cartOpen;
        private bool bedOpen;
        private HomeUiRenderer petGachaRenderer;
        private GameObject petGachaPanel;
        private GameObject petResultLayer;
        private GameObject petConfirmLayer;
        private GameObject petDetailLayer;
        private GameObject petScoreLayer;
        private HomeUiRenderer petDetailRenderer;
        private int petHistoryPage;
        private bool petHundredSuppressChoice;

        private readonly string[] tabNames = { "Character", "TravelNote", "Home", "Legion", "Mainland" };
        private GameObject characterRoot;
        private Zjcs.Presentation.Character.CharacterFeatureRuntime characterRuntime;
        private GameObject inventoryRoot;
        private InventoryFeatureRuntime inventoryRuntime;

        public HomeHudView(HomeFeatureRuntime host, Transform parent)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            catalog = host.Catalog;
            canvasParent = parent;
        }

        public Transform CanvasTransform => canvas == null ? null : canvas.transform;

        public bool AnyModalOpen => activeDialog != null || inventoryRoot != null || host.FeatureModalOpen
            || (functionsPanel != null && functionsPanel.activeSelf) || homeSkin != null && homeSkin.IsOpen;

        public void Build()
        {
            CreateCanvas();
            EnsureEventSystem();
            BuildHomePanel();
            BuildTopBar();
            BuildBottomBar();
            BuildFunctions();
            BuildToast();
            SelectTab(2, false);
        }

        private void CreateCanvas()
        {
            var go = new GameObject("Home Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(null, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("Home Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private HomeUiRenderer Renderer()
        {
            var renderer = new HomeUiRenderer(catalog);
            owned.Add(renderer);
            return renderer;
        }

        private void BuildHomePanel()
        {
            homeRenderer = Renderer();
            homePanel = homeRenderer.Create("home", canvas.transform,
                (path, node) => BindHudButton(path, node, OnHomeButton));
            RectTransform rect = (RectTransform)homePanel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            BuildAvatar();
            // Official fade/screenshot guider nodes start hidden; their white mask must not cover the scene.
            homeRenderer.SetActiveByName("Guider", false);
            homeRenderer.SetActiveByName("HomeFadeProxy", false);
        }

        private void BuildAvatar()
        {
            Transform slot = Find(homePanel.transform, "NewPublicTopBarParent");
            if (slot == null) return;
            HomeUiRenderer avatar = Renderer();
            avatar.Create("player_icon", slot,
                (path, node) => BindHudButton(path, node, OnAvatarButton));
            nameLabel = CreateLabel(slot, "HomeName", 26, new Vector2(96, -8), TextAlignmentOptions.Left);
            levelLabel = CreateLabel(slot, "HomeLevel", 20, new Vector2(96, -42), TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string name, float size, Vector2 offset,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(320, 36);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = catalog.Font;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = new Color32(250, 236, 210, 255);
            return text;
        }

        private void BuildTopBar()
        {
            Transform slot = Find(homePanel.transform, "Slot");
            if (slot == null) slot = homePanel.transform;
            topRenderer = Renderer();
            GameObject bar = topRenderer.Create("top_bar", slot, null);
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(1, 1);
            barRect.anchoredPosition = new Vector2(-16, -12);
            Transform list = Find(bar.transform, "ResourceList") ?? bar.transform;
            for (int index = 0; index < CurrencyItems.Length; index++)
            {
                HomeUiRenderer money = Renderer();
                int itemId = CurrencyItems[index];
                GameObject entry = money.Create("money", list, (path, node) =>
                    BindHudButton(path, node, (clickedPath, clickedNode) => OnCurrencyButton(itemId, clickedPath)));
                string icon = host.Rules.ItemIcon(itemId);
                money.SetSpriteByPrefix("IconImg", string.IsNullOrEmpty(icon) ? CurrencyIcons[index] : icon);
                entry.name = "Money_" + itemId;
                entry.AddComponent<TopBarCurrency>().Renderer = money;
            }
        }

        public Action BuildWishCurrencyBar(Transform slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            HomeUiRenderer barRenderer = Renderer();
            GameObject bar = barRenderer.Create("top_bar", slot, null);
            RectTransform rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-16, -12);
            Transform list = Find(bar.transform, "ResourceList") ?? bar.transform;
            var currencies = new List<(HomeUiRenderer Renderer, int ItemId)>();
            for (int index = 0; index < CurrencyItems.Length; index++)
            {
                int itemId = CurrencyItems[index];
                HomeUiRenderer money = Renderer();
                GameObject entry = money.Create("money", list, (path, node) =>
                    BindHudButton(path, node, (clickedPath, clickedNode) => OnCurrencyButton(itemId, clickedPath)));
                string icon = host.Rules.ItemIcon(itemId);
                money.SetSpriteByPrefix("IconImg", string.IsNullOrEmpty(icon) ? CurrencyIcons[index] : icon);
                entry.name = "Money_" + itemId;
                currencies.Add((money, itemId));
            }

            void Refresh()
            {
                foreach ((HomeUiRenderer renderer, int itemId) in currencies)
                {
                    long count = host.Profile.InventoryCount(itemId);
                    string value = count >= 100000 && count < 100000000
                        ? (count / 10000f).ToString("0.#", CultureInfo.InvariantCulture) + "万"
                        : count.ToString(CultureInfo.InvariantCulture);
                    renderer.SetTextByName("CountTxt", value);
                }
            }

            Refresh();
            return Refresh;
        }

        private void BuildBottomBar()
        {
            bottomRenderer = Renderer();
            GameObject bar = bottomRenderer.Create("bottom_bar", canvas.transform,
                (path, node) => BindHudButton(path, node, OnBottomButton));
            RectTransform rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 8);
        }

        private void BuildFunctions()
        {
            functionsRenderer = Renderer();
            functionsPanel = functionsRenderer.Create("functions", canvas.transform,
                (path, node) => BindHudButton(path, node, OnFunctionButton));
            RectTransform rect = (RectTransform)functionsPanel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            functionsPanel.SetActive(false);
        }

        private void BuildToast()
        {
            noticeRoot = new GameObject("HomeToast", typeof(RectTransform));
            noticeRoot.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)noticeRoot.transform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 210);
            rect.sizeDelta = new Vector2(760, 90);
            Image background = noticeRoot.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.72f);
            toast = CreateLabel(noticeRoot.transform, "ToastText", 26, new Vector2(16, -10),
                TextAlignmentOptions.Center);
            RectTransform textRect = (RectTransform)toast.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            toast.enableWordWrapping = true;
            noticeRoot.SetActive(false);
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform nested = Find(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private void OnHomeButton(string path, GameObject node)
        {
            if (path.Contains("/FunctionEntryFunc/")) { ToggleFunctions(); return; }
            if (path.Contains("/FunctionEntryChatQuick/")) { Notify("离线模式不开放聊天"); return; }
            if (path.Contains("TeamMiniPanel") || path.Contains("ButtonTeam"))
            {
                Notify("组队入口按离线范围关闭");
                return;
            }
            if (path.Contains("GameMatch")) { Notify("多人匹配按离线范围关闭"); return; }
            if (path.Contains("PlayAsYouDownloadEntry")) { Notify("预下载入口在离线模式不可用"); return; }
            if (path.Contains("BattleEndNotify")) { Notify("战斗结算详情由战斗页面提供"); return; }
            Notify("该入口当前不可用");
        }

        private void OnBottomButton(string path, GameObject node)
        {
            if (path.EndsWith("/UnlockObj", StringComparison.Ordinal))
            {
                for (int index = 0; index < tabNames.Length; index++)
                    if (path.Contains("/" + tabNames[index] + "/")) { SelectTab(index, true); return; }
            }
            if (path.EndsWith("/LockBtn", StringComparison.Ordinal)) { Notify("该入口尚未解锁"); return; }
            if (path.EndsWith("/Upgrade", StringComparison.Ordinal)) { SelectTab(0, true); return; }
            if (path.EndsWith("/Battling", StringComparison.Ordinal)) { Notify("当前没有进行中的战斗"); return; }
            Notify("该入口当前不可用");
        }

        private void OnAvatarButton(string path, GameObject node)
        {
            SelectTab(0, true);
        }

        private void OnCurrencyButton(int itemId, string path)
        {
            if (path.EndsWith("/DescBtn", StringComparison.Ordinal))
            {
                Notify(host.Rules.ItemName(itemId) + " x" + Format(host.ItemCount(itemId)));
                return;
            }
            if (path.EndsWith("/AddBtn", StringComparison.Ordinal))
                Notify("离线模式不开放充值或交易入口");
        }

        private static void BindHudButton(string path, GameObject node, Action<string, GameObject> handler)
        {
            if (node == null || handler == null) return;
            Button button = node.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => handler(path, node));
            Toggle toggle = node.GetComponent<Toggle>();
            if (toggle != null) toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) handler(path, node);
            });
        }

        private void OnFunctionButton(string path, GameObject node)
        {
            if (path.EndsWith("CloseBtn", StringComparison.Ordinal)) { ToggleFunctions(false); return; }
            if (path.Contains("FunctionEntryTaskItem"))
            {
                ToggleFunctions(false);
                OpenTaskPanel(host.Profile, NavigateMainlineTask);
                return;
            }
            if (path.Contains("FunctionEntryHomeSkinItem"))
            {
                ToggleFunctions(false);
                ShowHomeSkins();
                return;
            }
            if (path.Contains("FunctionEntrySettingsItem")) { OpenSettings(); return; }
            if (path.Contains("FunctionEntryFriendItem") || path.Contains("FunctionEntrySocialItem")
                || path.Contains("FunctionEntryUserItem") || path.Contains("FunctionEntryTopListItem"))
            {
                Notify("社交与排行榜入口按离线范围关闭");
                return;
            }
            if (path.Contains("FunctionEntryShopItem"))
            {
                Notify("商城活动依赖联网服务，离线模式不开放");
                return;
            }
            if (path.Contains("FunctionEntryAnnounceItem"))
            {
                Notify("公告依赖联网服务，离线模式不开放");
                return;
            }
            if (path.Contains("FunctionEntryPlanesMergeItem"))
            {
                Notify("跨服合并依赖联网服务，离线模式不开放");
                return;
            }
            if (path.Contains("FunctionEntryPredownload"))
            {
                Notify("预下载依赖联网服务，离线模式不开放");
                return;
            }
            Notify("该入口的页面由对应功能模块提供");
        }

        public void ToggleFunctions(bool? open = null)
        {
            bool next = open ?? (functionsPanel != null && !functionsPanel.activeSelf);
            if (functionsPanel != null) functionsPanel.SetActive(next);
        }

        public void SelectTab(int index, bool notify)
        {
            selectedTab = Mathf.Clamp(index, 0, tabNames.Length - 1);
            ApplyTabVisuals();
            if (selectedTab == 0)
            {
                OpenCharacterPage();
                return;
            }
            if (characterRoot != null) characterRoot.SetActive(false);
            if (selectedTab == 4)
            {
                if (!host.EnterMainland())
                {
                    selectedTab = 2;
                    ApplyTabVisuals();
                }
                return;
            }
            if (notify && selectedTab != 2)
            {
                Notify(selectedTab == 1 ? "游记页由游记模块提供" :
                    selectedTab == 3 ? "公会页由单人公会模块提供" : "大陆页由世界地图模块提供");
            }
        }

        private void ApplyTabVisuals()
        {
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (bottomRenderer == null) break;
                bottomRenderer.SetActiveByName(tabNames[i] + "/UnlockObj/Anim/SelectedBgImg", i == selectedTab);
            }
        }

        public bool CharacterPageOpen => characterRoot != null && characterRoot.activeSelf;

        private void CloseCharacterPage()
        {
            if (characterRoot != null) characterRoot.SetActive(false);
            selectedTab = 2;
            ApplyTabVisuals();
        }

        private void OpenCharacterPage()
        {
            if (characterRoot == null)
            {
                characterRoot = new GameObject("HomeCharacterPage", typeof(RectTransform));
                characterRoot.transform.SetParent(canvas.transform, false);
                RectTransform rect = (RectTransform)characterRoot.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var view = characterRoot.AddComponent<Zjcs.Presentation.Character.OfficialCharacterPanelView>();
                characterRuntime = characterRoot.AddComponent<Zjcs.Presentation.Character.CharacterFeatureRuntime>();
                view.CloseRequested += CloseCharacterPage;
            }
            characterRoot.SetActive(true);
            try
            {
                characterRuntime.OpenProfile(host.ActiveProfileSlotId, host.SavesRoot);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home character page unavailable: " + error.Message);
                Notify("人物页打开失败：" + error.Message);
                characterRoot.SetActive(false);
            }
        }
        public bool IsClosedByNetworkPolicy(int type)
        {
            if (type == 13)
            {
                Notify("交易所与拍卖已按离线范围关闭");
                return true;
            }
            return false;
        }

        public void ShowNotice(string message) => Notify(message);

        public void ShowFurniturePlaceholder(int type)
        {
            string name = catalog.FurnitureName(type);
            Notify(name + " 的内部页面由所属功能模块提供");
        }

        public void ShowStorage()
        {
            if (inventoryRoot != null)
            {
                inventoryRoot.SetActive(true);
                inventoryRuntime?.Refresh();
                return;
            }

            GameObject root = new GameObject("HomeStorageInventory", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            inventoryRoot = root;
            Canvas overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 80;
            InventoryFeatureRuntime runtime = root.AddComponent<InventoryFeatureRuntime>();
            inventoryRuntime = runtime;
            runtime.Closed += () =>
            {
                if (inventoryRoot == root)
                {
                    inventoryRoot = null;
                    inventoryRuntime = null;
                }
                UnityEngine.Object.Destroy(root);
            };
            try
            {
                runtime.OpenProfile(host.ActiveProfileSlotId, host.SavesRoot);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("Home storage inventory unavailable: " + error.Message);
                inventoryRoot = null;
                inventoryRuntime = null;
                UnityEngine.Object.Destroy(root);
                Notify("背包打开失败：" + error.Message);
            }
        }

        public void ShowPetGacha()
        {
            OpenDialog("pet_exchange_shell", (renderer, panel) =>
            {
                petGachaRenderer = renderer;
                petGachaPanel = panel;
                BindPetGachaShell(renderer);
                ShowPetTab("gacha");
                RefreshPetGachaPanel();
            });
        }

        private void BindPetGachaShell(HomeUiRenderer renderer)
        {
            Bind(renderer, "/PetGachaPanel/BgPanel1/Content/BottomGo/CloseBtn_dark", CloseDialog);
            Bind(renderer, "/PetGachaPanel/BgPanel1/Content/BottomGo/TabBottom /TabGroup/GachaPanelTabItem",
                () => ShowPetTab("gacha"));
            Bind(renderer, "/PetGachaPanel/BgPanel1/Content/BottomGo/TabBottom /TabGroup/ShopPanelTabItem",
                () => ShowPetTab("shop"));
            Bind(renderer, "/PetGachaPanel/BgPanel1/Content/BottomGo/TabBottom /TabGroup/AchievementTabItem",
                () => ShowPetTab("achievement"));
            Bind(renderer, "/PetGachaPanel/BgPanel1/Content/BottomGo/TabBottom /TabGroup/ActivityGachaPanelTabItem",
                () => Notify("活动结缘未包含在固定版日常结缘范围内"));

            string main = "/PetGachaPanel/BgPanel1/Content/MiddleGo/PetGachaSubPanel/Content/";
            Bind(renderer, main + "GachaBtnContent/GachaOneTimesBtn/CommonBtn_CostItem", () => RequestPetDraw(1));
            Bind(renderer, main + "GachaBtnContent/GachaTenTimesBtn/CommonBtn_CostItem", () => RequestPetDraw(10));
            Bind(renderer, main + "GachaBtnContent/GachaHundredTimesBtn/CommonBtn_CostItem", () => RequestPetDraw(100));
            Bind(renderer, main + "PityBgImage/Txt1/DetailBtn", ShowPetGachaDetails);
            Bind(renderer, main + "ScoreRewardUI/ScoreInfoBtn", ShowPetScoreRewards);
            Bind(renderer, main + "ScoreRewardUI/ImageBg/ImageBg1/GetRewardBtn", ShowPetScoreRewards);
            renderer.SetActive("/PetGachaPanel/BgPanel1/Content/MiddleGo/ShopContent/TopGo/RefreshGo/RefreshInfoGo/RefreshBtn", false);
        }

        private void Bind(HomeUiRenderer renderer, string path, Action action)
        {
            Button button = renderer.Find<Button>(path);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action?.Invoke());
                return;
            }
            Toggle toggle = renderer.Find<Toggle>(path);
            if (toggle == null) return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(value => { if (value) action?.Invoke(); });
        }

        private void ShowPetTab(string tab)
        {
            if (petGachaRenderer == null) return;
            string middle = "/PetGachaPanel/BgPanel1/Content/MiddleGo/";
            petGachaRenderer.SetActive(middle + "PetGachaSubPanel", tab == "gacha");
            petGachaRenderer.SetActive(middle + "ShopContent", tab == "shop");
            petGachaRenderer.SetActive(middle + "AchievementPanel", tab == "achievement");
            if (tab == "shop") RefreshPetShop();
            if (tab == "achievement") RefreshPetCountAchievements();
        }

        private void RefreshPetGachaPanel()
        {
            if (petGachaRenderer == null || host.PetGacha == null) return;
            string prefix = "/PetGachaPanel/BgPanel1/Content/MiddleGo/PetGachaSubPanel/Content/";
            int count = host.PetGacha.DailyCount(DateTime.Now);
            petGachaRenderer.SetText(prefix + "DailyCountText/Text",
                "今日已结缘 " + count.ToString(CultureInfo.InvariantCulture) + "/"
                + host.PetGachaCatalog.Settings.DailyLimit.ToString(CultureInfo.InvariantCulture) + " 次");
            petGachaRenderer.SetText(prefix + "ScoreRewardUI/ScoreInfoBtn/TextScore",
                "<color=#CBCF61>" + host.PetGacha.State.Score.ToString(CultureInfo.InvariantCulture) + "</color>");
            int left = Math.Max(1, 80 - host.PetGacha.PityMissCount(53102));
            petGachaRenderer.SetText(prefix + "PityBgImage/Txt1/TextTip",
                "再结缘 " + left.ToString(CultureInfo.InvariantCulture) + " 次内必得传说幻兽");
            bool hasHundredPool = host.PetGachaCatalog.Chest?.OpenCounts.Contains(100) == true;
            petGachaRenderer.SetActive(prefix + "GachaBtnContent/GachaHundredTimesBtn",
                hasHundredPool && CanPetGachaDraw(100));
        }

        private bool CanPetGachaDraw(int count)
        {
            long owned = host.Profile.InventoryCount(host.PetGachaCatalog.Gacha.CostItemId);
            if (owned >= count) return true;
            if (host.Level < 41) return false;
            long missing = count - owned;
            long price = host.PetGachaCatalog.Settings.QuickBuyPrice;
            long currency = host.Profile.InventoryCount(host.PetGachaCatalog.Settings.QuickBuyCurrencyItemId);
            return missing <= long.MaxValue / price && currency >= missing * price;
        }

        private void RequestPetDraw(int count, bool confirmedHundred = false,
            bool suppressHundredPrompt = false, bool confirmedQuickBuy = false)
        {
            if (host.PetGacha == null) { Notify("结缘规则未载入"); return; }
            DateTime localNow = DateTime.Now;
            if (count == 100 && !confirmedHundred && host.PetGacha.ShouldConfirmHundred(localNow))
            {
                ShowPetConfirm("百次结缘", "本次将连续结缘100次，确认继续吗？", true,
                    suppress => RequestPetDraw(count, true, suppress, false));
                return;
            }

            int missing = Math.Max(0, count - (int)Math.Min(int.MaxValue,
                host.Profile.InventoryCount(host.PetGachaCatalog.Gacha.CostItemId)));
            if (missing > 0 && host.Level >= 41 && !confirmedQuickBuy)
            {
                long cost = (long)missing * host.PetGachaCatalog.Settings.QuickBuyPrice;
                ShowPetConfirm("补足结缘材料", "当前缺少 " + missing + " 个结缘材料，将消耗 "
                    + cost.ToString(CultureInfo.InvariantCulture) + " 晨星补足，确认继续吗？", false,
                    _ => RequestPetDraw(count, confirmedHundred, suppressHundredPrompt, true));
                return;
            }

            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            PetGachaResult result = host.PetGacha.Draw(count,
                "home:pet-gacha:draw:" + slotId() + ":" + Guid.NewGuid().ToString("N"), now,
                confirmedHundred || count != 100 || !host.PetGacha.ShouldConfirmHundred(localNow),
                suppressHundredPrompt, confirmedQuickBuy);
            if (!result.IsApplied && result.Code != PetGachaResultCode.Unchanged)
            {
                Notify(string.IsNullOrEmpty(result.Message) ? PetGachaError(result.Code) : result.Message);
                return;
            }
            RefreshPetGachaPanel();
            ShowPetGachaResult(result.Awards, count);
        }

        private string slotId() => host.ActiveProfileSlotId;

        private static string PetGachaError(PetGachaResultCode code) => code switch
        {
            PetGachaResultCode.InsufficientItems => "结缘材料不足",
            PetGachaResultCode.DailyLimit => "今日结缘次数已达上限",
            PetGachaResultCode.ConditionNotMet => "当前结缘入口不可用",
            PetGachaResultCode.NotReady => "奖励尚未达成或已领取",
            _ => "结缘未能完成"
        };

        private void ShowPetGachaResult(IReadOnlyList<OfflinePetGachaAward> awards, int count)
        {
            PlayPetGachaResultPerformance(awards, count);
        }

        private void ShowPetGachaResultPage(IReadOnlyList<OfflinePetGachaAward> awards, int count)
        {
            QueueNewPetIntroductions(awards);
            ClosePetLayer(ref petResultLayer);
            HomeUiRenderer renderer = CreatePetLayer("pet_gacha_result", out petResultLayer,
                ClosePetResult);
            OfflinePetGachaAward first = awards == null ? null : awards.FirstOrDefault();
            PetGachaItem firstItem = first == null ? null : host.PetGachaCatalog.Item(first.ItemId);
            renderer.SetTextByName("NameText", firstItem?.Name ?? "结缘结果");
            renderer.SetTextByName("TypeText", first == null ? string.Empty : QualityText(first.Quality));
            renderer.SetTextByName("DescText", PetAwardSummary(awards));
            PopulatePetGachaResult(renderer, awards, count);
            renderer.SetTextByName("FreeCount", "今日已结缘 " + host.PetGacha.DailyCount(DateTime.Now)
                + "/" + host.PetGachaCatalog.Settings.DailyLimit);
            renderer.SetActive("/GachaResultPanel/BgPanel1/Content/MiddleGo/GachaBtnContent/GachaHundredTimesBtn",
                CanPetGachaDraw(100));
            Bind(renderer, "/GachaResultPanel/BgPanel1/Content/MiddleGo/CloseBtn", ClosePetResult);
            Bind(renderer, "/GachaResultPanel/BgPanel1/Content/SkipBtn", ClosePetResult);
            Bind(renderer, "/GachaResultPanel/BgPanel1/Content/MiddleGo/GachaBtnContent/GachaOneTimesBtn/CommonBtn_CostItem",
                () => RequestPetDraw(1));
            Bind(renderer, "/GachaResultPanel/BgPanel1/Content/MiddleGo/GachaBtnContent/GachaTenTimesBtn/CommonBtn_CostItem",
                () => RequestPetDraw(10));
            Bind(renderer, "/GachaResultPanel/BgPanel1/Content/MiddleGo/GachaBtnContent/GachaHundredTimesBtn/CommonBtn_CostItem",
                () => RequestPetDraw(100));
            ShowNextPetIntroduction();
        }

        private string PetAwardSummary(IReadOnlyList<OfflinePetGachaAward> awards)
        {
            if (awards == null || awards.Count == 0) return "没有取得奖励记录";
            return string.Join("\n", awards.Select(award =>
            {
                PetGachaItem item = host.PetGachaCatalog.Item(award.ItemId);
                string line = (item?.Name ?? award.ItemId.ToString(CultureInfo.InvariantCulture)) + " x" + award.Count;
                if (award.ConvertedCount > 0)
                {
                    PetGachaItem converted = host.PetGachaCatalog.Item(award.ConvertedItemId);
                    line += "，重复转换为 " + (converted?.Name ?? award.ConvertedItemId.ToString(CultureInfo.InvariantCulture))
                        + " x" + award.ConvertedCount;
                }
                return line;
            }).ToArray());
        }

        private static string QualityText(int quality) => quality switch
        {
            4 => "传说",
            3 => "史诗",
            2 => "精良",
            _ => "普通"
        };

        private void Notify(string message)
        {
            if (toast == null) return;
            toast.text = message ?? string.Empty;
            noticeRoot.SetActive(true);
            noticeRoot.transform.SetAsLastSibling();
            CancelInvoke();
            Invoke(() => { if (noticeRoot != null) noticeRoot.SetActive(false); }, 2.4f);
        }

        private float noticeDeadline;

        private void CancelInvoke() => noticeDeadline = 0f;

        private void Invoke(Action action, float delay) => noticeDeadline = Time.unscaledTime + delay;

        public void Tick()
        {
            TickPetIntroduction();
            TickTreasureGachaPreview();
            if (noticeDeadline > 0f && Time.unscaledTime >= noticeDeadline)
            {
                noticeDeadline = 0f;
                if (noticeRoot != null) noticeRoot.SetActive(false);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) TryHandleBack();
        }

        public bool TryHandleBack()
        {
            if (astrologicalOverlayRoot != null) { CloseAstrologicalOverlay(); return true; }
            if (activeDialog != null) { CloseDialog(); return true; }
            if (homeSkin != null && homeSkin.IsOpen) { homeSkin.Close(); return true; }
            if (functionsPanel != null && functionsPanel.activeSelf) { ToggleFunctions(false); return true; }
            if (inventoryRoot != null && inventoryRoot.activeSelf)
            {
                inventoryRuntime?.Close();
                return true;
            }
            if (characterRoot != null && characterRoot.activeSelf) { CloseCharacterPage(); return true; }
            return false;
        }

        public void RefreshAll()
        {
            RefreshCounts();
            RefreshTasks();
            if (nameLabel != null) nameLabel.text = host.CharacterName;
            if (levelLabel != null) levelLabel.text = "Lv." + host.Level + "  " + host.GuildName;
            if (bedOpen) RefreshBed();
            if (cartOpen) RefreshCart();
        }

        public void RefreshCounts()
        {
            if (canvas == null) return;
            foreach (TopBarCurrency currency in canvas.GetComponentsInChildren<TopBarCurrency>(true))
                currency.Refresh(host);
        }

        // ---------------------------------------------------------------- windows

        private GameObject OpenDialog(string panelKey, Action<HomeUiRenderer, GameObject> build)
        {
            CloseDialog();
            var root = new GameObject("HomeDialog", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image overlay = root.GetComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.62f);
            Button button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(CloseDialog);
            HomeUiRenderer renderer = Renderer();
            GameObject panel = renderer.Create(panelKey, root.transform, null);
            build(renderer, panel);
            activeDialog = root;
            activeDialogRenderer = renderer;
            activePanelKey = panelKey;
            return panel;
        }

        public void CloseDialog()
        {
            bool wasOpen = activeDialog != null;
            if (activePanelKey == "astrological_entry")
            {
                host.EndAstrologicalCharge();
                ClearAstrologicalRows();
            }
            OnDialogClosing();
            ClearPetVirtualList();
            if (activeDialog != null) UnityEngine.Object.Destroy(activeDialog);
            activeDialog = null;
            activeDialogRenderer = null;
            activePanelKey = null;
            cartOpen = false;
            bedOpen = false;
            petGachaRenderer = null;
            petGachaPanel = null;
            petDetailRenderer = null;
            petHistoryPage = 0;
            petHundredSuppressChoice = false;
            petScoreRenderer = null;
            petConfirmLayer = null;
            petDetailLayer = null;
            petScoreLayer = null;
            petResultLayer = null;
            if (wasOpen) OnDialogClosed();
        }

        partial void OnDialogClosing();
        partial void OnDialogClosed();

        public void ShowRepair(int type)
        {
            HomeFurnitureRecord record = host.Rules.FurnitureOf(type);
            if (record == null) return;
            OpenDialog("repair", (renderer, panel) =>
            {
                renderer.SetTextByName("NameTxt", host.Rules.FurnitureNameForType(type));
                renderer.SetTextByName("SubTxt", host.Catalog.FurnitureTypeName(type));
                renderer.SetTextByName("DescTxt", "维修后可启用该设施。");
                Button repair = renderer.Find<Button>("/FurnitureRepairDialog/BgDialog3/BgImg/Mask/RepaireBtn");
                if (repair == null) repair = FirstButton(renderer, "RepaireBtn");
                if (repair != null) repair.onClick.AddListener(() =>
                {
                    if (host.TryRepair(type))
                    {
                        CloseDialog();
                        Notify("维修完成");
                        host.OnFruitTreeRepairDialogClosed(type);
                    }
                    else Notify("维修条件未满足");
                });
            });
        }

        public void ShowBed()
        {
            OpenDialog("bed", (renderer, panel) => { RefreshBed(renderer); });
            bedOpen = activeDialog != null;
        }

        private void RefreshBed() => RefreshBed(activeDialogRenderer);

        private void RefreshBed(HomeUiRenderer renderer)
        {
            if (renderer == null) return;
            int hangUpId = 1;
            HomeHangUpRecord record = host.Rules.HangUp(hangUpId);
            if (record == null) return;
            renderer.SetTextByName("NameTxt", host.Rules.ItemName(host.Rules.FurnitureClassId(0)));
            renderer.SetTextByName("SubTxt", host.Catalog.FurnitureTypeName(0));
            HomePreview(hangUpId, out long count, out long capacity, out double hourly);
            renderer.SetTextByName("ValueTxt", count + "/" + capacity);
            renderer.SetTextByName("DataText", "每小时 " + Format(hourly));
            renderer.SetTextUnderPath("FurniturePropItemBlessLevel", "Value/Text", host.BlessingLevel.ToString());
            renderer.SetTextUnderPath("FurniturePropItemLossGoddessNums", "Value/Text", "0");
            renderer.SetTextUnderPath("FurniturePropItemBase", "Value/Text", Format(hourly));
            renderer.SetTextUnderPath("FurniturePropItem", "Value/Text", Format(hourly));
            renderer.SetTextUnderPath("FurniturePropItem (1)", "Value/Text", Format(capacity));
            renderer.SetTextUnderPath("FurniturePropItemLevelAddOfBase", "Value/Text", "0");
            renderer.SetTextUnderPath("FurniturePropItemGodessAddOfBase", "Value/Text", "0");
            renderer.SetTextUnderPath("FurniturePropItem1", "Value/Text", "0");
            renderer.SetTextUnderPath("FurniturePropItemTreasure", "Value/Text", "0");
            renderer.SetTextUnderPath("FurniturePropItemMonthCard", "Value/Text", "0");
            renderer.SetTextUnderPath("AstrologicalPropItem", "Value/Text", "0");
            renderer.ReplaceExactText("{0}", host.BlessingLevel.ToString());
            string expName = host.Rules.ItemName(50);
            if (!string.IsNullOrEmpty(expName)) renderer.SetTextByName("NameTxt", expName);
            Image ring = renderer.Find<Image>("/FurnitureBedDialog/BgDialog3/Content/HangUpInfo/Display/FillImg");
            if (ring != null)
            {
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Horizontal;
                ring.fillAmount = capacity > 0 ? Mathf.Clamp01((float)count / capacity) : 0f;
            }
            AddFurnitureIcon(renderer, "/FurnitureBedDialog/BgDialog3/Icon/IconImgSlot", "furniture_icon_0");            Button collect = FirstButton(renderer, "CollectBtn");
            if (collect != null)
            {
                collect.onClick.RemoveAllListeners();
                collect.onClick.AddListener(() =>
                {
                    long gained = host.TryCollect(hangUpId);
                    Notify(gained > 0 ? "领取 " + Format(gained) + " 经验" : "暂无可领取产出");
                    RefreshBed();
                });
            }
            Button speed = FirstButton(renderer, "SpeedBtn");
            if (speed != null)
            {
                speed.onClick.RemoveAllListeners();
                speed.onClick.AddListener(() => ShowFastForward(hangUpId));
            }
        }

        private void HomePreview(int hangUpId, out long count, out long capacity, out double hourly)
        {
            HangUpIncomePreview preview = host.Session.Preview(hangUpId);
            count = preview.Count;
            capacity = preview.Capacity;
            hourly = preview.HourlyRate;
        }

        public void ShowCart()
        {
            OpenDialog("cart", (renderer, panel) =>
            {
                renderer.SetTextByName("TitleTxt", host.Rules.FurnitureNameForType(10));
                renderer.SetTextByName("DescText", "小推车资源产出");
                BuildCartRows(renderer, panel);
                Button collect = FirstButton(renderer, "CommonBtn_12");
                if (collect != null)
                {
                    collect.onClick.RemoveAllListeners();
                    collect.onClick.AddListener(() =>
                    {
                        long total = 0;
                        foreach (HomeHangUpRecord record in host.Rules.HangUpsOf(10))
                            if (host.Session.IsHangUpUnlocked(record.id)) total += host.TryCollect(record.id);
                        Notify(total > 0 ? "领取 " + Format(total) : "暂无可领取产出");
                        RefreshCart();
                    });
                }
            });
            cartOpen = activeDialog != null;
        }

        private void RefreshCart()
        {
            if (activeDialogRenderer == null || !cartOpen) return;
            Transform content = Find(activeDialog != null ? activeDialog.transform : null, "Content");
            if (content == null) return;
            for (int index = content.childCount - 1; index >= 0; index--)
                UnityEngine.Object.Destroy(content.GetChild(index).gameObject);
            BuildCartRows(activeDialogRenderer, activeDialog);
        }

        private void BuildCartRows(HomeUiRenderer renderer, GameObject panel)
        {
            Transform content = Find(panel.transform, "HangUpScrollRect");
            Transform list = content == null ? null : Find(content, "Content");
            if (list == null) list = Find(panel.transform, "Content");
            if (list == null) return;
            foreach (HomeHangUpRecord record in host.Rules.HangUpsOf(10))
            {
                HomeUiRenderer rowRenderer = Renderer();
                GameObject row = rowRenderer.Create("cart_row", list, null);
                bool unlocked = host.Session.IsHangUpUnlocked(record.id);
                rowRenderer.SetActiveByName("Unlock", unlocked);
                rowRenderer.SetActiveByName("Lock", !unlocked && !host.Rules.IsMet(record.unlock, host.Context));
                rowRenderer.SetActiveByName("CanUnlock", !unlocked && host.Rules.IsMet(record.unlock, host.Context));
                rowRenderer.SetTextByName("Name", host.Rules.ItemName(host.Rules.HangUpReward(record.rewardId)?.rewardRuleId ?? 0));
                string rowName = host.ItemNameForHangUp(record.id);
                rowRenderer.SetTextByName("Name", rowName);
                rowRenderer.SetTextByName("LevelNumber", host.Session.HangUpLevel(record.id).ToString());
                if (unlocked)
                {
                    HangUpIncomePreview preview = host.Session.Preview(record.id);
                    rowRenderer.SetTextByName("ValueTxt", Format(preview.Count) + "/" + Format(preview.Capacity));
                }
                int id = record.id;
                Button levelUp = FirstButton(rowRenderer, "CommonBtn_LevelUp");
                if (levelUp != null && unlocked && record.canLevelUp)
                {
                    levelUp.onClick.AddListener(() =>
                    {
                        HomeLevelUpResult result = host.TryLevelUpHangUp(id);
                        Notify(result.Success ? "升级到 " + result.Level + " 级" : ErrorText(result.Error));
                        RefreshCart();
                    });
                }
                else if (levelUp != null) levelUp.gameObject.SetActive(false);
                Button unlock = FirstButton(rowRenderer, "UnlockBtn");
                if (unlock != null)
                {
                    unlock.onClick.AddListener(() =>
                    {
                        Notify(host.TryUnlockHangUp(id) ? "已解锁" : "解锁条件未满足");
                        RefreshCart();
                    });
                }
            }
        }

        private static string ErrorText(string error) => error switch
        {
            "max_level" => "已满级",
            "home_level" => "家园等级不足",
            "items" => "升级材料不足",
            "locked" => "尚未解锁",
            "condition" => "条件未满足",
            _ => "升级失败"
        };

        public void ShowMail()
        {
            OpenDialog("mail_mail", (renderer, panel) =>
            {
                IReadOnlyList<OfflineHomeMail> inbox = host.Session.GetInbox(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Transform list = Find(panel.transform, "Content");
                if (list == null) list = Find(panel.transform, "ScrollRect");
                if (list == null) return;
                Transform content = Find(list, "Content") ?? list;
                foreach (OfflineHomeMail mail in inbox)
                {
                    HomeUiRenderer rowRenderer = Renderer();
                    GameObject row = rowRenderer.Create("mail_row", content, null);
                    rowRenderer.SetTextByName("Mail_Title", mail.Subject);
                    rowRenderer.SetTextByName("DateText", string.Empty);
                    rowRenderer.SetActiveByName("MailUnReadImage", mail.Status == 0);
                    rowRenderer.SetActiveByName("MailReadImage", mail.Status != 0);
                    rowRenderer.SetActiveByName("RewardContent", mail.Attachments.Count > 0);
                    long id = mail.Id;
                    Button button = row.GetComponent<Button>();
                    if (button != null) button.onClick.AddListener(() => ShowMailContent(id));
                }
                if (inbox.Count == 0) Notify("邮箱暂无邮件");
                Button deleteRead = FirstButton(renderer, "AllDelBtn");
                if (deleteRead != null)
                    deleteRead.onClick.AddListener(() =>
                    {
                        int removed = host.Session.DeleteReadMail() ? 1 : 0;
                        Notify(removed > 0 ? "已删除已读邮件" : "没有可删除的已读邮件");
                        CloseDialog();
                        ShowMail();
                    });
                Button collectAll = FirstButton(renderer, "PickUpBtn");
                if (collectAll != null)
                    collectAll.onClick.AddListener(() =>
                    {
                        int count = host.Session.CollectAllMailAttachments(
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            "home:mail:" + Guid.NewGuid().ToString("N"));
                        host.DrainHomeInventory();
                        Notify(count > 0 ? "领取 " + count + " 封邮件附件" : "暂无可领取附件");
                        CloseDialog();
                    });
                Button readAll = FirstButton(renderer, "AllReadBtn");
                if (readAll != null)
                    readAll.onClick.AddListener(() =>
                    {
                        host.Session.MarkAllMailRead(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        Notify("已全部标记为已读");
                        CloseDialog();
                    });
            });
        }

        internal void ShowTutorialFixedReward(IReadOnlyList<WishAmount> rewards)
        {
            if (rewards == null || rewards.Count == 0) return;
            OpenDialog("awards", (renderer, panel) =>
            {
                renderer.SetTextByName("TitleTxt", "获得奖励");
                Button close = FirstButton(renderer, "CloseObj");
                if (close != null) close.onClick.AddListener(CloseDialog);

                Transform grid = Find(panel.transform, "Grid");
                if (grid == null || !catalog.HasPanel("award_row"))
                    throw new InvalidOperationException("The official award panel item list is missing.");
                WishAmount[] visible = rewards.Where(value => value != null && value.ItemId > 0 && value.Count > 0).ToArray();
                for (int index = 0; index < visible.Length; index++)
                {
                    WishAmount reward = visible[index];
                    HomeUiRenderer itemRenderer = Renderer();
                    GameObject item = itemRenderer.Create("award_row", grid, null);
                    item.name = "TutorialReward_" + reward.ItemId;
                    RectTransform itemRect = item.transform as RectTransform;
                    itemRect.anchorMin = itemRect.anchorMax = new Vector2(0.5f, 0.5f);
                    itemRect.pivot = new Vector2(0.5f, 0.5f);
                    itemRect.anchoredPosition = new Vector2((index - (visible.Length - 1) * 0.5f) * 188f, 0);

                    var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(item.transform, false);
                    RectTransform iconRect = (RectTransform)iconObject.transform;
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(0, 4);
                    iconRect.sizeDelta = new Vector2(76, 76);
                    Image icon = iconObject.GetComponent<Image>();
                    icon.sprite = host.ItemIcon(reward.ItemId);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;

                    TextMeshProUGUI name = CreateLabel(item.transform, "ItemName", 18,
                        new Vector2(0, 62), TextAlignmentOptions.Center);
                    RectTransform nameRect = name.rectTransform;
                    nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    nameRect.pivot = new Vector2(0.5f, 0.5f);
                    nameRect.anchoredPosition = new Vector2(0, 62);
                    nameRect.sizeDelta = new Vector2(150, 26);
                    name.text = host.ItemName(reward.ItemId);

                    TextMeshProUGUI count = CreateLabel(item.transform, "ItemCount", 20,
                        new Vector2(0, -57), TextAlignmentOptions.Center);
                    RectTransform countRect = count.rectTransform;
                    countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.5f);
                    countRect.pivot = new Vector2(0.5f, 0.5f);
                    countRect.anchoredPosition = new Vector2(0, -57);
                    countRect.sizeDelta = new Vector2(150, 28);
                    count.text = "×" + reward.Count.ToString(CultureInfo.InvariantCulture);
                }
            });
        }

        private void ShowMailContent(long mailId)
        {
            OfflineHomeMail mail = host.Session.GetInbox(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .FirstOrDefault(value => value.Id == mailId);
            if (mail == null) return;
            host.Session.MarkMailRead(mailId);
            OpenDialog("mail_content", (renderer, panel) =>
            {
                renderer.SetTextByName("TitleText", mail.Subject);
                renderer.SetTextByName("TextFrom", mail.Sender);
                renderer.SetTextByName("TextTime", string.Empty);
                renderer.SetTextByName("TextArticleTitle", string.Empty);
                renderer.SetTextByName("TextArticle", mail.Body);
                Button confirm = FirstButton(renderer, "ComfirmBtn");
                if (confirm == null) return;
                confirm.onClick.AddListener(() =>
                {
                    if (mail.Attachments.Count > 0)
                        host.Session.CollectMailAttachments(mail.Id,
                            "home:mail:" + mail.Id + ":" + Guid.NewGuid().ToString("N"));
                    host.DrainHomeInventory();
                    CloseDialog();
                    ShowMail();
                });
            });
            return;

        }

        public void ShowFastForward(int hangUpId)
        {
            OpenDialog("fast_idle", (renderer, panel) =>
            {
                HomeHangUpRecord hang = host.Rules.HangUp(hangUpId);
                HomeFastHangUpRecord fast = host.Rules.FastHangUp(hang == null ? 1 : hang.fastHangUpId);
                HomeFastHangUpItem item = fast?.items?.FirstOrDefault(value => value != null && value.itemId > 0);
                int max = item == null ? 0 : Mathf.Min(100, Mathf.Min(fast.maxUseItemCount, (int)Math.Min(int.MaxValue, host.ItemCount(item.itemId))));
                int selected = max > 0 ? 1 : 0;
                renderer.SetTextByName("SpeedTxt", item == null ? "" : "每个时光结晶 +" + item.hours + "小时");
                renderer.SetTextByName("TitleText", "快速挂机");

                Transform itemSlot = Find(panel.transform, "ItemSlot");
                HomeUiRenderer row = null;
                Slider slider = null;
                if (item != null && itemSlot != null && catalog.HasPanel("fast_item_row"))
                {
                    row = Renderer();
                    row.Create("fast_item_row", itemSlot, null);
                    foreach (GameObject node in row.AllNodes)
                    {
                        if (node.name == "Slider") slider = node.GetComponent<Slider>();
                    }
                    if (slider != null)
                    {
                        slider.minValue = max > 0 ? 1 : 0;
                        slider.maxValue = Mathf.Max(1, max);
                        slider.wholeNumbers = true;
                        slider.value = selected;
                        slider.onValueChanged.AddListener(value =>
                        {
                            selected = Mathf.RoundToInt(value);
                            row.SetTextByName("SelectedCountTxt", "×" + selected);
                        });
                    }
                    row.SetTextByName("SelectedNameTxt", "时光结晶");
                    row.SetTextByName("SelectedCountTxt", max > 0 ? "×1" : "×0");
                    Transform iconSlot = Find(itemSlot, "IconSlot");
                    Sprite icon = host.ItemIcon(item.itemId);
                    if (iconSlot != null && icon != null)
                    {
                        var iconGo = new GameObject("TimeCrystalIcon", typeof(RectTransform), typeof(Image));
                        iconGo.transform.SetParent(iconSlot, false);
                        RectTransform iconRect = (RectTransform)iconGo.transform;
                        iconRect.sizeDelta = new Vector2(64, 64);
                        Image image = iconGo.GetComponent<Image>();
                        image.sprite = icon;
                        image.preserveAspect = true;
                        image.raycastTarget = false;
                    }
                    Button reduce = FirstButton(row, "CommonBtn_Reduce");
                    if (reduce != null && slider != null)
                        reduce.onClick.AddListener(() => slider.value = Mathf.Max(slider.minValue, slider.value - 1));
                    Button add = FirstButton(row, "CommonBtn_Add");
                    if (add != null && slider != null)
                        add.onClick.AddListener(() => slider.value = Mathf.Min(slider.maxValue, slider.value + 1));
                    Button maxButton = FirstButton(row, "MaxBtn");
                    if (maxButton != null && slider != null) maxButton.onClick.AddListener(() => slider.value = slider.maxValue);
                }

                Button itemSpeed = FirstButton(renderer, "SpeedBtn");
                if (itemSpeed != null)
                {
                    itemSpeed.interactable = max > 0;
                    itemSpeed.onClick.AddListener(() =>
                    {
                        int count = slider == null ? selected : Mathf.RoundToInt(slider.value);
                        long total = host.TryFastForwardItems(fast == null ? 1 : fast.fastHangUpId, count);
                        Notify(total > 0 ? "道具加速获得 " + Format(total) : "时光结晶不足或没有可加速产出");
                        CloseDialog();
                    });
                }
                Button free = FirstButton(renderer, "CommonBtn_12");
                if (free != null)
                    free.onClick.AddListener(() =>
                    {
                        long total = host.TryFastForward(hangUpId);
                        Notify(total > 0 ? "免费加速获得 " + Format(total) : "免费加速次数已用完");
                        CloseDialog();
                    });
            });
        }
        private static Image FindImage(HomeUiRenderer renderer, string name)
        {
            foreach (GameObject node in renderer.AllNodes)
                if (node.name == name) return node.GetComponent<Image>();
            return null;
        }

        private void AddFurnitureIcon(HomeUiRenderer renderer, string slotPath, string panelKey)
        {
            GameObject slot = renderer.Find(slotPath);
            if (slot == null || !catalog.HasPanel(panelKey)) return;
            if (slot.transform.Find("OfficialIcon") != null) return;
            HomeUiRenderer icon = Renderer();
            GameObject built = icon.Create(panelKey, slot.transform, null);
            built.name = "OfficialIcon";
            RectTransform rect = (RectTransform)built.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        private static Button FirstButton(HomeUiRenderer renderer, string name)
        {
            foreach (GameObject node in renderer.AllNodes)
            {
                if (!node.name.Equals(name, StringComparison.Ordinal)) continue;
                Button button = node.GetComponent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        private static string Format(long value)
        {
            if (value >= 100000000) return (value / 100000000f).ToString("0.##") + "亿";
            if (value >= 10000) return (value / 10000f).ToString("0.##") + "万";
            return value.ToString();
        }

        private static string Format(double value) => Format((long)Math.Round(value));

        public void Dispose()
        {
            host.EndAstrologicalCharge();
            ClearAstrologicalRows();
            homeSkin?.Close();
            homeSkin = null;
            if (inventoryRoot != null) UnityEngine.Object.Destroy(inventoryRoot);
            inventoryRoot = null;
            inventoryRuntime = null;
            owned.Clear();
            if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
            canvas = null;
        }
    }

    /// <summary>Refreshes one official ``MoneyUI`` entry from the bag.</summary>
    public sealed class TopBarCurrency : MonoBehaviour
    {
        public HomeUiRenderer Renderer;
        private int itemId;

        private void Awake()
        {
            if (int.TryParse(name.Replace("Money_", string.Empty), out int parsed)) itemId = parsed;
        }

        public void Refresh(HomeFeatureRuntime host)
        {
            if (Renderer == null) return;
            long count = host.ItemCount(itemId);
            Renderer.SetTextByName("CountTxt", host.FormatTopBarCurrency(count));
        }
    }
}
