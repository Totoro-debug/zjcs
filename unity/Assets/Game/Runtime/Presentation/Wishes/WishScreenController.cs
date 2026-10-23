using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.Opening;

namespace Zjcs.Presentation.Wishes
{
    [DisallowMultipleComponent]
    public sealed class WishScreenController : MonoBehaviour
    {
        private const int PageSize = 5;
        private OfflineMainlineProgressSession profile;
        private OfficialWishAssetCatalog assets;
        private OfficialCommerceAssetCatalog commerceAssets;
        private WishRuntimeCatalog rules;
        private OfflineWishService service;
        private Transform canvasRoot;
        private GameObject shell;
        private OfficialCommercePrefabRenderer shellRenderer;
        private Transform loopContent;
        private Transform shopContent;
        private Transform fixedShopContent;
        private Transform achievementContent;
        private GameObject poolPanel;
        private OfficialCommercePrefabRenderer poolRenderer;
        private GameObject modal;
        private GameObject resultRoot;
        private Coroutine resultSequence;
        private Coroutine resultQualitySequence;
        private bool skipResultSequence;
        private bool confirmedHundredReminderSuppressed;
        private Action onClose;
        private Action<Transform> buildCurrencyBar;
        private Action refreshCurrencyBar;
        private TMP_Text buyQuantityLabel;
        private Slider buyQuantitySlider;
        private int buyQuantityMax;
        private string slotId;
        private int poolType;
        private int mainPage;
        private int shopId = 4;
        private int achievementPage;
        private int achievementGroup = 5;
        private int historyPage;
        private int buyCount = 1;
        private float dayCheckTimer;
        private float countdownUpdateTimer;
        private bool isOpen;
        private readonly HashSet<int> shopQualityFilters = new HashSet<int>();
        private readonly HashSet<int> shopElementFilters = new HashSet<int>();
        private readonly HashSet<int> shopTypeFilters = new HashSet<int>();
        private readonly HashSet<int> shopProfessionFilters = new HashSet<int>();
        public bool IsOpen => isOpen && this != null;
        public OfflineWishService Service => service;

        public static WishScreenController Open(OfflineMainlineProgressSession profile, string slotId,
            Transform parent, Action<Transform> buildCurrencyBar, Action refreshCurrencyBar, Action closed = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var host = new GameObject("WishScreenRuntime");
            WishScreenController controller = host.AddComponent<WishScreenController>();
            controller.Initialize(profile, slotId, parent, buildCurrencyBar, refreshCurrencyBar, closed);
            return controller;
        }

        private void Initialize(OfflineMainlineProgressSession activeProfile, string activeSlot, Transform parent,
            Action<Transform> createCurrencyBar, Action updateCurrencyBar, Action closed)
        {
            profile = activeProfile;
            slotId = activeSlot;
            buildCurrencyBar = createCurrencyBar;
            refreshCurrencyBar = updateCurrencyBar;
            onClose = closed;
            profile.Reload();
            assets = OfficialWishAssetCatalog.LoadDefault();
            commerceAssets = OfficialCommerceAssetCatalog.LoadDefault();
            rules = WishRuntimeCatalog.Parse(assets.runtimeCatalog.text);
            service = new OfflineWishService(profile, rules);
            EnsureEventSystem();
            BuildCanvas(parent);
            BuildShell();
            WishResult guideReward = service.ClaimGuideReward(Now(), "wish:guide:" + slotId);
            service.EnsureShopDay(5, CurrentProfession, DateTime.Now);
            RenderPool();
            RefreshChrome();
            isOpen = true;
            if (guideReward.IsApplied) ShowGuideReward(guideReward.Awards);
        }

        public void Tick()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal != null) CloseModal();
                else Close();
            }
            dayCheckTimer += Time.unscaledDeltaTime;
            countdownUpdateTimer += Time.unscaledDeltaTime;
            if (countdownUpdateTimer >= 1f)
            {
                countdownUpdateTimer = 0;
                if (shopId == 5) UpdateShopCountdown();
            }
            if (dayCheckTimer < 4f) return;
            dayCheckTimer = 0;
            int oldDay = DayKey(DateTime.Now.AddSeconds(-4));
            int newDay = DayKey(DateTime.Now);
            if (oldDay != newDay)
            {
                service.EnsureShopDay(5, CurrentProfession, DateTime.Now);
                RefreshCurrentPage();
            }
        }

        public void Close()
        {
            if (!isOpen && this == null) return;
            isOpen = false;
            if (gameObject != null) Destroy(gameObject);
            onClose?.Invoke();
            onClose = null;
        }

        private int CurrentProfession => profile.Save.Player.Wardrobe.Profession;
        private int SelectedProfession
        {
            get
            {
                IReadOnlyList<int> available = service.AvailableDrawProfessions(poolType, CurrentProfession);
                int selected = service.SelectedProfession(poolType, 0);
                if (available.Contains(selected)) return selected;
                if (available.Contains(CurrentProfession)) return CurrentProfession;
                return available.FirstOrDefault();
            }
        }
        private long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private void BuildCanvas(Transform parent)
        {
            if (parent != null) transform.SetParent(parent, false);
            var canvasObject = new GameObject("WishCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 72;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = canvasObject.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("Wish Event System", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void BuildShell()
        {
            CommercePrefab source = assets.Load().Prefab("gacha_shell");
            shellRenderer = NewRenderer();
            shell = shellRenderer.CreateSubtree(source.root, canvasRoot, BindShellButton);
            shell.name = "OfficialWishPanel";
            loopContent = Find(shell.transform,
                "BgPanel1/Content/MiddleGo/Content/LoopView/Viewport/Content");
            shopContent = Find(shell.transform,
                "BgPanel1/Content/MiddleGo/ShopContent/ShopGoodsContent/ShopContentGroup/GoodsGrid/ViewPort/Content");
            fixedShopContent = Find(shell.transform,
                "BgPanel1/Content/MiddleGo/ShopContent/SkillPiecesGroup/ScrollRect/Viewport/Content");
            achievementContent = Find(shell.transform,
                "BgPanel1/Content/MiddleGo/AchievementPanel/ScrollView/Viewport/Content");
            BuildWishTopBar();
            BindPageTabs();
            BindPoolTabs();
            BindShopTabs();
            Button close = FindButton(shell.transform, "CloseBtn_dark");
            if (close != null) close.onClick.AddListener(Close);
            mainPage = 0;
            shopId = 4;
            SetMainPage(0);
        }

        private void BuildWishTopBar()
        {
            Transform slot = Find(shell.transform, "BgPanel1/Content/TopGo/NewPublicTopBarParent/Slot");
            if (slot != null) buildCurrencyBar?.Invoke(slot);
            UpdateWishTopBar();
        }

        private void UpdateWishTopBar() => refreshCurrencyBar?.Invoke();

        private void BindShellButton(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            string name = node.name;
            if (name == "GachaPanelTabItem") button.onClick.AddListener(() => SetMainPage(0));
            else if (name == "HomeShopPanelTabBottomBtn (3)") button.onClick.AddListener(() => SetMainPage(1));
            else if (name == "AchievementTabItem") button.onClick.AddListener(() => OpenAchievementPage(false));
            else if (name == "CloseBtn_dark") button.onClick.AddListener(Close);
            else if (name == "CommonBtn_CostItem" && path.Contains("RefreshBtn"))
                button.onClick.AddListener(ConfirmRefreshShop);
            else if (name == "FilterSortPanel") button.onClick.AddListener(OpenShopFilters);
        }

        private void BindPageTabs()
        {
            foreach (Transform child in shell.transform.GetComponentsInChildren<Transform>(true))
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;
                if (child.name == "GachaPanelTabItem") AddListenerOnce(button, () => SetMainPage(0));
                else if (child.name == "HomeShopPanelTabBottomBtn (3)") AddListenerOnce(button, () => SetMainPage(1));
                else if (child.name == "AchievementTabItem") AddListenerOnce(button, () => OpenAchievementPage(false));
                else if (child.name == "FilterSortPanel") AddListenerOnce(button, OpenShopFilters);
            }
        }

        private void BindPoolTabs()
        {
            Transform content = Find(shell.transform, "BgPanel1/Content/BottomGo/Bottom/GachaOptions/Viewport/Content");
            if (content == null) return;
            foreach (Transform child in content)
            {
                int type = child.name == "SeniorGachaBtn" ? 2
                    : child.name == "FriendGachaBtn" ? 1
                    : child.name == "GachaBtn" ? 0 : -1;
                if (type < 0) continue;
                Button button = child.GetComponent<Button>();
                if (button == null) button = child.gameObject.AddComponent<Button>();
                if (button.targetGraphic == null) button.targetGraphic = child.GetComponentInChildren<Graphic>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPool(type));
            }
        }

        private void BindShopTabs()
        {
            foreach (Transform child in shell.transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Shop4" && child.name != "Shop5") continue;
                Button button = child.GetComponent<Button>();
                if (button == null) button = child.gameObject.AddComponent<Button>();
                if (button.targetGraphic == null) button.targetGraphic = child.GetComponentInChildren<Graphic>();
                int id = child.name == "Shop4" ? 4 : 5;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectShop(id));
            }
        }

        private void SelectPool(int type)
        {
            if (type < 0 || type > 2 || rules.Gacha(type) == null) return;
            poolType = type;
            RenderPool();
            RefreshChrome();
        }

        private void RenderPool()
        {
            if (loopContent == null) return;
            ClearChildren(loopContent);
            CommercePrefab source = assets.Load().Prefab(poolType == 1 ? "gacha_friend"
                : poolType == 2 ? "gacha_senior" : "goddess");
            poolRenderer = NewRenderer();
            poolPanel = poolRenderer.CreateSubtree(source.root, loopContent, BindPoolButton);
            poolPanel.name = poolType == 1 ? "FriendshipWish" : poolType == 2 ? "SeniorWish" : "RegularWish";
            OfficialCommercePrefabRenderer.RebuildLayout(poolPanel);
            if (poolType == 2) SetSeniorProfessionIcon(poolPanel.transform, SelectedProfession);
            if (poolType == 0) RefreshCustomSelectionEntry();
        }

        private void SetSeniorProfessionIcon(Transform panel, int professionId)
        {
            Transform slot = Find(panel, "Content/ProfessionSlot");
            WishProfession profession = rules.Profession(professionId);
            Sprite sprite = profession == null ? null : assets.FindSprite(profession.Icon3File);
            if (slot == null || sprite == null) return;
            ClearChildren(slot);
            var icon = new GameObject("OfficialProfessionIcon", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)icon.transform;
            rect.SetParent(slot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void RefreshCustomSelectionEntry()
        {
            Transform entry = Find(poolPanel.transform, "Content/SkillGachaCustomProfessionSelectionEntry");
            if (entry == null) return;
            WishProfession current = rules.Profession(SelectedProfession);
            bool unlocked = current != null
                && current.Rank >= rules.settings.SkillGachaCustomSelectionSettings.RequiredProfessionRank;
            entry.gameObject.SetActive(unlocked);
            if (!unlocked) return;
            FillProfessionIcon(Find(entry, "Content/CurrentProfesionIcon"), current);
            OfflineWishProfessionSelection saved = service.State.ProfessionSelections.FirstOrDefault(value =>
                value.Profession == current.Profession && value.Completed);
            WishProfession[] selected = (saved?.SelectedProfessions ?? new List<int>()).Select(rules.Profession)
                .Where(value => value != null).OrderByDescending(value => value.Rank)
                .ThenBy(value => value.Profession).ToArray();
            for (int index = 0; index < 4; index++)
            {
                string name = index == 0 ? "SelectedProfessionIcon" : "SelectedProfessionIcon (" + index + ")";
                FillProfessionIcon(Find(entry, "Content/" + name), index < selected.Length ? selected[index] : null);
            }
        }

        private void FillProfessionIcon(Transform root, WishProfession profession)
        {
            if (root == null) return;
            bool populated = profession != null;
            Transform iconNode = Find(root, "Content/ProfessionIcon");
            Transform rankNode = Find(root, "Content/ProfessionRankBg");
            if (iconNode != null)
            {
                Image image = iconNode.GetComponent<Image>();
                if (image != null && populated)
                {
                    image.sprite = assets.FindSprite(profession.Icon1File);
                    image.preserveAspect = true;
                }
                iconNode.gameObject.SetActive(populated);
            }
            if (rankNode != null)
            {
                SetTextDeep(rankNode, "ProfessionRankText", populated
                    ? profession.Rank.ToString(CultureInfo.InvariantCulture) : string.Empty);
                rankNode.gameObject.SetActive(populated);
            }
        }

        private void BindPoolButton(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.Contains("GachaOneTimesBtn/CommonBtn_CostItem"))
                button.onClick.AddListener(() => TryDraw(1));
            else if (path.Contains("GachaTenTimesBtn/CommonBtn_CostItem"))
                button.onClick.AddListener(() => TryDraw(10));
            else if (path.Contains("GachaHundredTimesBtn/CommonBtn_CostItem"))
                button.onClick.AddListener(() => TryDraw(100));
            else if (path.EndsWith("/DetailBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => OpenDetails(false));
            else if (path.EndsWith("/ScoreInfoBtn", StringComparison.Ordinal)
                || path.EndsWith("/GetRewardBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => OpenAchievementPage(true));
            else if (path.EndsWith("/SelectProfessionDropdown/Content", StringComparison.Ordinal))
                button.onClick.AddListener(OpenProfessionPicker);
            else if (path.EndsWith("/SkillGachaCustomProfessionSelectionEntry/ClickMask", StringComparison.Ordinal))
                button.onClick.AddListener(OpenCustomSelection);
        }

        private void TryDraw(int count)
        {
            WishGacha gacha = rules.Gacha(poolType);
            WishChest chest = gacha == null ? null : rules.Chest(gacha.ChestId);
            if (chest == null || !chest.OpenCounts.Contains(count)) return;
            if (!CanAffordDraw(chest, count))
            {
                ShowInsufficient(chest, count);
                return;
            }
            DateTime now = DateTime.Now;
            if (count == 100 && service.ShouldConfirmHundred(poolType, now))
            {
                ShowConfirmation("确认祈愿", "将连续祈愿 100 次。", () =>
                {
                    bool suppress = confirmedHundredReminderSuppressed;
                    confirmedHundredReminderSuppressed = false;
                    if (suppress)
                        service.SetHundredReminder(poolType, DateTime.Now, true, Receipt("hundred-reminder"));
                    ConfirmProfessionThenDraw(count);
                }, true);
                return;
            }
            ConfirmProfessionThenDraw(count);
        }

        private void ConfirmProfessionThenDraw(int count)
        {
            if (poolType != 1 && SelectedProfession != CurrentProfession)
            {
                ShowConfirmation("确认祈愿职业", "当前角色职业为 " + ProfessionName(CurrentProfession)
                    + "，本次将按 " + ProfessionName(SelectedProfession) + " 的奖池抽取。", () => ExecuteDraw(count), false);
                return;
            }
            ExecuteDraw(count);
        }

        private Toggle suppressHundredToggle;

        private void ExecuteDraw(int count)
        {
            WishResult result = service.Draw(poolType, count,
                poolType == 1 ? 0 : SelectedProfession,
                Receipt("draw"), Now(), true);
            if (!result.IsApplied)
            {
                ShowNotice(string.IsNullOrEmpty(result.Message) ? "祈愿未完成" : result.Message);
                RefreshCurrentPage();
                return;
            }
            RefreshCurrentPage();
            ShowResult(result.Awards, count);
        }

        private bool CanAffordDraw(WishChest chest, int count)
        {
            foreach (WishAmount amount in chest.CostItems ?? Array.Empty<WishAmount>())
                if (profile.InventoryCount(amount.ItemId) < amount.Count * count) return false;
            int limit = rules.settings.DailyGachaMaxCount.FirstOrDefault(value =>
                value.GachaClassType == rules.Gacha(poolType).GachaClassType)?.Count ?? int.MaxValue;
            return service.DailyCount(DateTime.Now) + count <= limit;
        }

        private bool CanOpenDrawQuickBuy(WishChest chest, int count)
        {
            int limit = rules.settings.DailyGachaMaxCount.FirstOrDefault(value =>
                value.GachaClassType == rules.Gacha(poolType).GachaClassType)?.Count ?? int.MaxValue;
            if (service.DailyCount(DateTime.Now) + count > limit) return false;
            WishAmount missing = (chest.CostItems ?? Array.Empty<WishAmount>()).FirstOrDefault(value =>
                profile.InventoryCount(value.ItemId) < value.Count * count);
            if (missing == null) return false;
            WishQuickBuy quick = (rules.quickBuys ?? Array.Empty<WishQuickBuy>())
                .FirstOrDefault(value => value.ItemId == missing.ItemId);
            long need = missing.Count * count - profile.InventoryCount(missing.ItemId);
            long packs = quick == null || quick.BaseCount <= 0 ? 0
                : (need + quick.BaseCount - 1) / quick.BaseCount;
            return service.CanQuickBuy(missing.ItemId, packs);
        }

        private void ShowInsufficient(WishChest chest, int count)
        {
            WishAmount cost = (chest.CostItems ?? Array.Empty<WishAmount>()).FirstOrDefault(value =>
                profile.InventoryCount(value.ItemId) < value.Count * count);
            if (cost == null)
            {
                ShowNotice("已达到今日祈愿次数上限");
                return;
            }
            long missing = Math.Max(1, cost.Count * count - profile.InventoryCount(cost.ItemId));
            WishQuickBuy quick = (rules.quickBuys ?? Array.Empty<WishQuickBuy>()).FirstOrDefault(value => value.ItemId == cost.ItemId);
            if (quick == null || quick.BaseCount <= 0)
            {
                ShowNotice("祈愿券不足，暂时没有对应的快捷购买配置");
                return;
            }
            int packs = Mathf.CeilToInt(missing / (float)quick.BaseCount);
            if (!service.CanQuickBuy(cost.ItemId, packs))
            {
                ShowNotice("祈愿券不足，当前不满足快捷购买条件");
                return;
            }
            ShowConfirmation("祈愿券不足", "购买 " + packs + " 份补充祈愿券？", () =>
            {
                WishResult buy = service.QuickBuy(cost.ItemId, packs, Now(), Receipt("quick-buy"));
                RefreshCurrentPage();
                if (buy.IsApplied) ShowAwardPopup("购买完成", "已补充祈愿材料", buy.Awards);
                else ShowNotice(buy.Message);
            }, false);
        }

        private void ShowResult(IReadOnlyList<OfflineWishAward> awards, int drawCount)
        {
            OpenModal("gacha_result", out _, out GameObject root);
            resultRoot = root;
            skipResultSequence = false;
            IReadOnlyList<OfflineWishAward> results = awards ?? Array.Empty<OfflineWishAward>();
            Transform middle = Find(root.transform, "BgPanel1/Content/MiddleGo");
            Transform one = Find(middle, "OneGrid");
            Transform ten = Find(middle, "TenGrid");
            Transform hundred = Find(middle, "HundredGrid");
            Transform buttons = Find(middle, "GachaBtnContent");
            SetActive(one, drawCount == 1);
            SetActive(ten, drawCount > 1 && drawCount <= 10);
            SetActive(hundred, drawCount > 10);
            SetActive(buttons, false);
            SetResultChromeAlpha(root.transform, 0f);

            var cards = new List<GameObject>(results.Count);
            var icons = new List<GameObject>(results.Count);
            if (drawCount == 1 && results.Count > 0)
            {
                OfflineWishAward award = results[0];
                Transform slot = Find(one, "Pos1");
                GameObject icon = CreateAwardIcon(slot, award);
                cards.Add(icon);
                icons.Add(icon);
                SetTextDeep(one, "NameGo/NameText", ItemName(award.ItemId));
                SetTextDeep(one, "DescGo/DescText", AwardDescription(award));
                SetTextDeep(one, "TypeText", AwardType(award));
                ConfigureQualityLight(middle, award.Quality);
            }
            else if (drawCount <= 10)
            {
                for (int index = 0; index < results.Count && index < 10; index++)
                {
                    Transform slot = Find(ten, "Slot_" + index);
                    GameObject card = CreateResultCard(slot, results[index], out GameObject icon);
                    card.SetActive(false);
                    cards.Add(card);
                    icons.Add(icon);
                }
            }
            else
            {
                BuildHundredResultGrid(hundred, results, cards, icons);
            }

            Transform skip = Find(root.transform, "BgPanel1/Content/SkipBtn");
            SetActive(skip, true);
            BindAction(skip, () => skipResultSequence = true);
            OfficialCommercePrefabRenderer.RebuildLayout(root);
            resultSequence = StartCoroutine(RevealResult(root, drawCount, results, cards, icons));
        }

        private IEnumerator RevealResult(GameObject root, int drawCount, IReadOnlyList<OfflineWishAward> awards,
            IReadOnlyList<GameObject> cards, IReadOnlyList<GameObject> icons)
        {
            if (drawCount == 1 && awards.Count > 0)
            {
                WishUiRuntimeCatalog catalog = assets.Load();
                bool piece = IsSkillPiece(awards[0].ItemId);
                if (piece)
                    yield return PlayOfficialAnimation(root, catalog.resultAnimations.onePieces);
                else
                {
                    ConfigureQualityLight(Find(root.transform, "BgPanel1/Content/MiddleGo"), awards[0].Quality);
                    resultQualitySequence = StartCoroutine(
                        PlayOfficialAnimation(root, catalog.resultAnimations.quality));
                    yield return PlayOfficialAnimation(root, catalog.resultAnimations.one,
                        QualitySound(awards[0].Quality), catalog.resultTiming.oneSoundDelayPercent);
                    if (resultQualitySequence != null) yield return resultQualitySequence;
                    resultQualitySequence = null;
                }
                if (!skipResultSequence && HasConversion(awards[0]))
                    yield return new WaitForSecondsRealtime(catalog.resultTiming.oneTransDelay);
                if (HasConversion(awards[0])) ShowAwardTransition(icons[0], awards[0]);
                if (skipResultSequence)
                {
                    if (!piece) yield return PlayOfficialAnimation(root, catalog.resultAnimations.qualityCut);
                    yield return FadeCanvasGroup(Find(root.transform, "BgPanel1/Content/MiddleGo/OneGrid"),
                        catalog.resultTiming.gachaFadeInTime);
                }
                else if (catalog.resultTiming.oneFadeInDelay > 0)
                    yield return new WaitForSecondsRealtime(catalog.resultTiming.oneFadeInDelay);
            }
            else if (drawCount <= 10)
                yield return RevealTenResults(root, awards, cards, icons);
            else
                yield return RevealHundredResults(root, awards, cards, icons);

            if (resultRoot == root)
            {
                Transform skip = Find(root.transform, "BgPanel1/Content/SkipBtn");
                SetActive(skip, false);
                ConfigureResultDrawButtons(root.transform);
                SetActive(Find(root.transform, "BgPanel1/Content/MiddleGo/GachaBtnContent"), true);
                yield return FadeResultChrome(root, assets.Load().resultTiming.gachaFadeInTime);
                float extraDelay = assets.Load().resultTiming.additionalAwardsDelay;
                if (extraDelay > 0) yield return new WaitForSecondsRealtime(extraDelay);
            }
            resultSequence = null;
        }

        private IEnumerator RevealTenResults(GameObject root, IReadOnlyList<OfflineWishAward> awards,
            IReadOnlyList<GameObject> cards, IReadOnlyList<GameObject> icons)
        {
            WishUiRuntimeCatalog catalog = assets.Load();
            List<WishAnimationTrack> tracks = BuildAnimationTracks(root, catalog.resultAnimations.stars,
                out float duration);
            WishStarAnimationEvent[] events = catalog.resultAnimations.starEvents;
            int eventIndex = 0;
            float startedAt = Time.unscaledTime;
            while (eventIndex < events.Length)
            {
                if (skipResultSequence) break;
                float elapsed = Mathf.Min(duration, Time.unscaledTime - startedAt);
                foreach (WishAnimationTrack track in tracks)
                    ApplyAnimationValue(track.Target, track.Property, EvaluateAnimationKeys(track.Keys, elapsed));
                while (eventIndex < events.Length && events[eventIndex].time <= elapsed)
                {
                    WishStarAnimationEvent animationEvent = events[eventIndex++];
                    if (animationEvent.functionName == "PlayStarEffect")
                        PlayStarEffect(root, animationEvent.index);
                    else if (animationEvent.functionName == "OnStarEffectEnd")
                        CompleteStarEffect(root, animationEvent.index, awards, cards);
                }
                if (elapsed >= duration) break;
                yield return null;
            }

            if (skipResultSequence)
            {
                for (int index = 0; index < 10; index++) StopStarEffect(root, index);
                for (int index = 0; index < cards.Count; index++)
                {
                    cards[index].SetActive(true);
                    PlayResultSound(root, awards[index].Quality);
                }
            }
            int quality = awards.Where((award, index) => !IsSkillPiece(award.ItemId))
                .Select(award => award.Quality).DefaultIfEmpty(0).Max();
            if (quality > 0)
            {
                ConfigureQualityLight(Find(root.transform, "BgPanel1/Content/MiddleGo"), quality);
                StartCoroutine(PlayOfficialAnimation(root, catalog.resultAnimations.qualityCut));
            }
            StartCoroutine(TransitionTenAwards(icons, awards, catalog.resultTiming));
            if (catalog.resultTiming.tenFadeInDelay > 0)
                yield return new WaitForSecondsRealtime(catalog.resultTiming.tenFadeInDelay);
        }

        private IEnumerator TransitionTenAwards(IReadOnlyList<GameObject> icons,
            IReadOnlyList<OfflineWishAward> awards, WishResultTiming timing)
        {
            if (timing.tenTransDelay > 0) yield return new WaitForSecondsRealtime(timing.tenTransDelay);
            for (int index = 0; index < icons.Count; index++)
            {
                if (!HasConversion(awards[index])) continue;
                ShowAwardTransition(icons[index], awards[index]);
                if (timing.tenTransTimeRate > 0)
                    yield return new WaitForSecondsRealtime(timing.tenTransTimeRate);
            }
        }

        private void PlayStarEffect(GameObject root, int index)
        {
            Transform star = Find(root.transform, "BgPanel1/Content/AnimPlayer/Stars/Start_" + index);
            if (star == null) return;
            star.gameObject.SetActive(true);
            Coffee.UIExtensions.UIParticle particle = star.GetComponent<Coffee.UIExtensions.UIParticle>();
            if (particle != null) particle.Play();
        }

        private void CompleteStarEffect(GameObject root, int index, IReadOnlyList<OfflineWishAward> awards,
            IReadOnlyList<GameObject> cards)
        {
            StopStarEffect(root, index);
            if (index >= awards.Count || index >= cards.Count) return;
            cards[index].SetActive(true);
            PlayResultSound(root, awards[index].Quality);
        }

        private void StopStarEffect(GameObject root, int index)
        {
            Transform star = Find(root.transform, "BgPanel1/Content/AnimPlayer/Stars/Start_" + index);
            if (star == null) return;
            Coffee.UIExtensions.UIParticle particle = star.GetComponent<Coffee.UIExtensions.UIParticle>();
            if (particle != null) particle.Stop();
            star.gameObject.SetActive(false);
        }

        private IEnumerator RevealHundredResults(GameObject root, IReadOnlyList<OfflineWishAward> awards,
            IReadOnlyList<GameObject> cards, IReadOnlyList<GameObject> icons)
        {
            WishResultTiming timing = assets.Load().resultTiming;
            Transform grid = Find(root.transform, "BgPanel1/Content/MiddleGo/HundredGrid");
            ScrollRect scroll = grid == null ? null : grid.GetComponent<ScrollRect>();
            if (scroll == null) throw new InvalidOperationException("Official hundred result scroll is missing.");
            List<object> curve = OpeningJson.Array(OpeningJson.Parse(
                System.Text.Encoding.UTF8.GetBytes(timing.hundredScrollCurve)), "hundred draw scroll curve");
            scroll.verticalNormalizedPosition = EvaluateAnimationKeys(curve, 0);
            Coroutine scrollSequence = StartCoroutine(ScrollHundredResults(scroll, cards.Count, timing, curve));
            for (int index = 0; index < cards.Count; index++)
            {
                if (skipResultSequence) break;
                cards[index].SetActive(true);
                PlayResultSound(root, awards[index].Quality);
                if (HasConversion(awards[index]))
                    StartCoroutine(ShowAwardTransitionAfter(icons[index], awards[index], timing.hundredTransDelay));
                if (index + 1 < cards.Count && timing.hundredShowUpDelay > 0)
                    yield return new WaitForSecondsRealtime(timing.hundredShowUpDelay);
            }
            if (skipResultSequence)
            {
                for (int index = 0; index < cards.Count; index++)
                {
                    cards[index].SetActive(true);
                    if (HasConversion(awards[index])) ShowAwardTransition(icons[index], awards[index]);
                }
                scroll.verticalNormalizedPosition = EvaluateAnimationKeys(curve, 1f);
            }
            else if (scrollSequence != null) yield return scrollSequence;
        }

        private IEnumerator ScrollHundredResults(ScrollRect scroll, int count,
            WishResultTiming timing, IReadOnlyList<object> curve)
        {
            float delay = Mathf.Max(0, (count - 3) * timing.hundredShowUpDelay * timing.hundredScrollDelayThread);
            float duration = Mathf.Max(0.001f, count * timing.hundredShowUpDelay - delay);
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float startedAt = Time.unscaledTime;
            while (scroll != null)
            {
                float progress = skipResultSequence ? 1f
                    : Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                scroll.verticalNormalizedPosition = EvaluateAnimationKeys(curve, progress);
                if (progress >= 1f) break;
                yield return null;
            }
        }

        private GameObject CreateResultCard(Transform parent, OfflineWishAward award, out GameObject icon)
        {
            if (parent == null) throw new InvalidOperationException("Official result slot is missing.");
            OfficialCommercePrefabRenderer renderer = NewRenderer();
            GameObject card = renderer.CreateSubtree(assets.Load().Prefab("gacha_ten_reward").root, parent);
            Image light = Find(card.transform, "Content/LightImg")?.GetComponent<Image>();
            if (light != null)
            {
                Sprite sprite = ResultQualitySprite(award.Quality);
                if (sprite != null) light.sprite = sprite;
            }
            Transform iconSlot = Find(card.transform, "Content/IconContent");
            icon = CreateAwardIcon(iconSlot, award);
            return card;
        }

        private void BuildHundredResultGrid(Transform grid, IReadOnlyList<OfflineWishAward> awards,
            List<GameObject> cards, List<GameObject> icons)
        {
            Transform viewport = Find(grid, "View");
            Transform content = Find(viewport, "Content");
            if (content == null) throw new InvalidOperationException("Official hundred result content is missing.");
            ClearChildren(content);
            CommerceUiNode rowSource = assets.Load().Prefab("gacha_hundred_row").root;
            float[] rowSize = rowSource.size_delta;
            float rowHeight = Mathf.Max(1, rowSize != null && rowSize.Length > 1 ? rowSize[1] : 0f);
            RectTransform contentRect = content as RectTransform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x,
                rowHeight * Mathf.CeilToInt(awards.Count / 3f));

            int rowCount = Mathf.CeilToInt(awards.Count / 3f);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                GameObject row = NewRenderer().CreateSubtree(rowSource, content);
                row.name = "WishResultRow_" + rowIndex;
                RectTransform rowRect = row.transform as RectTransform;
                rowRect.anchorMin = new Vector2(0.5f, 1);
                rowRect.anchorMax = new Vector2(0.5f, 1);
                rowRect.pivot = new Vector2(0.5f, 1);
                rowRect.anchoredPosition = new Vector2(0, -rowIndex * rowHeight);
                row.SetActive(true);
                string[] names = { "Left", "Middle", "Right" };
                for (int slotIndex = 0; slotIndex < names.Length; slotIndex++)
                {
                    int awardIndex = rowIndex * 3 + slotIndex;
                    if (awardIndex >= awards.Count) break;
                    Transform slot = Find(row.transform, names[slotIndex]);
                    GameObject card = CreateResultCard(slot, awards[awardIndex], out GameObject icon);
                    card.SetActive(false);
                    cards.Add(card);
                    icons.Add(icon);
                }
            }

            ScrollRect scroll = grid.GetComponent<ScrollRect>();
            if (scroll == null) scroll = grid.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.viewport = viewport as RectTransform;
            scroll.content = contentRect;
            scroll.verticalNormalizedPosition = 1f;
        }

        private GameObject CreateAwardIcon(Transform parent, OfflineWishAward award)
        {
            if (parent == null) throw new InvalidOperationException("Official result item slot is missing.");
            OfficialCommercePrefabRenderer renderer = NewRenderer();
            GameObject root = renderer.CreateSubtree(assets.Load().Prefab("gacha_award_icon").root, parent);
            root.name = "WishAward_" + award.ItemId;
            Transform anchor = Find(root.transform, "Anchor");
            if (anchor != null)
            {
                var iconObject = new GameObject("OfficialItemIcon", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)iconObject.transform;
                rect.SetParent(anchor, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(IsSkillPiece(award.ItemId) ? 112 : 136,
                    IsSkillPiece(award.ItemId) ? 112 : 136);
                Image image = iconObject.GetComponent<Image>();
                image.sprite = assets.FindItemSprite(award.ItemId);
                image.preserveAspect = true;
                image.raycastTarget = true;
                Button button = iconObject.AddComponent<Button>();
                button.targetGraphic = image;
                WishItem item = rules.Item(award.ItemId);
                button.onClick.AddListener(() => { if (item != null) ShowItemDetails(item); });
            }
            if (award.Count > 1 || IsSkillPiece(award.ItemId))
                AddText(root.transform, "WishAwardCount", award.Count.ToString(CultureInfo.InvariantCulture),
                    new Vector2(55, -70), new Vector2(120, 38), 25, TextAlignmentOptions.Right);
            if (!HasConversion(award)) SetActiveDeep(root.transform, "AgioObj", false);
            else
            {
                Transform agio = Find(root.transform, "AgioObj");
                if (agio != null) agio.gameObject.SetActive(true);
                Transform pieces = Find(root.transform, "AgioObj/AgiotagePiece");
                if (pieces != null) pieces.gameObject.SetActive(award.ConvertedCount > 0);
                Transform recycled = Find(root.transform, "AgioObj/RecyclePiece");
                if (recycled != null) recycled.gameObject.SetActive(award.RecycledCount > 0);
                Transform divider = Find(root.transform, "AgioObj/DividerLine");
                if (divider != null) divider.gameObject.SetActive(award.ConvertedCount > 0 && award.RecycledCount > 0);
                SetAwardOverlay(root.transform, "AgiotagePiece", award.ConvertedItemId, award.ConvertedCount);
                SetAwardOverlay(root.transform, "RecyclePiece", award.RecycledItemId, award.RecycledCount);
            }
            return root;
        }

        private void SetAwardOverlay(Transform root, string path, int itemId, long count)
        {
            if (itemId <= 0 || count <= 0) return;
            Transform node = Find(root, "AgioObj/" + path);
            if (node == null) return;
            Image image = Find(node, path == "AgiotagePiece" ? "Icon/Mask/IconImage" : "IconImage")
                ?.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = assets.FindItemSprite(itemId);
                image.enabled = image.sprite != null;
                image.preserveAspect = true;
            }
            SetTextDeep(node, "CountText", count.ToString(CultureInfo.InvariantCulture));
        }

        private IEnumerator ShowAwardTransitionAfter(GameObject icon, OfflineWishAward award, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (resultRoot != null && !skipResultSequence) ShowAwardTransition(icon, award);
        }

        private void ShowAwardTransition(GameObject icon, OfflineWishAward award)
        {
            if (icon == null || !icon) return;
            PlayResultSound(resultRoot, "GoddessPray_Animation_3");
            Transform agio = Find(icon.transform, "AgioObj");
            if (agio != null) agio.gameObject.SetActive(true);
            Transform pieces = Find(icon.transform, "AgioObj/AgiotagePiece");
            if (pieces != null) pieces.gameObject.SetActive(award.ConvertedCount > 0);
            Transform recycled = Find(icon.transform, "AgioObj/RecyclePiece");
            if (recycled != null) recycled.gameObject.SetActive(award.RecycledCount > 0);
            Transform divider = Find(icon.transform, "AgioObj/DividerLine");
            if (divider != null) divider.gameObject.SetActive(award.ConvertedCount > 0 && award.RecycledCount > 0);
            SetAwardOverlay(icon.transform, "AgiotagePiece", award.ConvertedItemId, award.ConvertedCount);
            SetAwardOverlay(icon.transform, "RecyclePiece", award.RecycledItemId, award.RecycledCount);
        }

        private void ConfigureResultDrawButtons(Transform root)
        {
            Transform content = Find(root, "BgPanel1/Content/MiddleGo/GachaBtnContent");
            if (content == null) return;
            WishChest chest = rules.Chest(rules.Gacha(poolType).ChestId);
            if (chest == null) return;
            foreach (Transform node in content.GetComponentsInChildren<Transform>(true))
            {
                int count = node.name.Contains("GachaOneTimesBtn") ? 1
                    : node.name.Contains("GachaTenTimesBtn") ? 10
                    : node.name.Contains("GachaHundredTimesBtn") ? 100 : 0;
                if (count == 0) continue;
                bool supported = chest.OpenCounts.Contains(count);
                node.gameObject.SetActive(supported);
                Button button = node.GetComponent<Button>();
                if (button == null) continue;
                button.interactable = CanAffordDraw(chest, count) || CanOpenDrawQuickBuy(chest, count);
                int drawCount = count;
                BindAction(node, () =>
                {
                    CloseModal();
                    if (CanAffordDraw(chest, drawCount)) TryDraw(drawCount);
                    else ShowInsufficient(chest, drawCount);
                });
                WishAmount cost = (chest.CostItems ?? Array.Empty<WishAmount>()).FirstOrDefault();
                SetTextDeep(node, "ItemContent/Text", cost == null ? string.Empty
                    : (cost.Count * count).ToString(CultureInfo.InvariantCulture));
                if (cost != null) SetIconDeep(node, cost.ItemId);
            }
        }

        private IEnumerator PlayOfficialAnimation(GameObject root, string json, string soundEvent = null,
            float soundDelayPercent = 0.45f)
        {
            List<WishAnimationTrack> tracks = BuildAnimationTracks(root, json, out float duration);
            float startedAt = Time.unscaledTime;
            bool soundPlayed = string.IsNullOrEmpty(soundEvent);
            float soundAt = duration * Mathf.Clamp01(soundDelayPercent);
            while (true)
            {
                float time = skipResultSequence ? duration : Mathf.Min(duration, Time.unscaledTime - startedAt);
                foreach (WishAnimationTrack track in tracks) ApplyAnimationValue(track.Target, track.Property,
                    EvaluateAnimationKeys(track.Keys, time));
                if (!soundPlayed && time >= soundAt)
                {
                    PlayResultSound(root, soundEvent);
                    soundPlayed = true;
                }
                if (time >= duration) break;
                yield return null;
            }
        }

        private static List<WishAnimationTrack> BuildAnimationTracks(GameObject root, string json,
            out float duration)
        {
            Dictionary<string, object> animation = OpeningJson.Object(
                OpeningJson.Parse(System.Text.Encoding.UTF8.GetBytes(json)), "wish result animation");
            List<object> sourceTracks = OpeningJson.Array(animation["tracks"], "wish animation tracks");
            var tracks = new List<WishAnimationTrack>(sourceTracks.Count);
            foreach (object raw in sourceTracks)
            {
                Dictionary<string, object> source = OpeningJson.Object(raw, "wish animation track");
                string path = OpeningJson.String(source, "path").Trim('/');
                int separator = path.IndexOf('/');
                if (separator >= 0) path = path.Substring(separator + 1);
                Transform target = Find(root.transform, path);
                if (target == null) continue;
                tracks.Add(new WishAnimationTrack(target, OpeningJson.String(source, "property"),
                    OpeningJson.Array(source["keys"], "wish animation keys")));
            }
            duration = (float)OpeningJson.Number(animation, "duration");
            return tracks;
        }

        private static float EvaluateAnimationKeys(IReadOnlyList<object> keys, float time)
        {
            if (keys.Count == 0) return 0;
            List<object> first = OpeningJson.Array(keys[0], "wish animation key");
            if (time <= (float)OpeningJson.ToNumber(first[0])) return (float)OpeningJson.ToNumber(first[1]);
            for (int index = 0; index + 1 < keys.Count; index++)
            {
                List<object> left = OpeningJson.Array(keys[index], "wish animation key");
                List<object> right = OpeningJson.Array(keys[index + 1], "wish animation key");
                float t0 = (float)OpeningJson.ToNumber(left[0]);
                float t1 = (float)OpeningJson.ToNumber(right[0]);
                if (time > t1) continue;
                float duration = Mathf.Max(0.0001f, t1 - t0);
                float t = Mathf.Clamp01((time - t0) / duration);
                float y0 = (float)OpeningJson.ToNumber(left[1]);
                float y1 = (float)OpeningJson.ToNumber(right[1]);
                float m0 = (float)OpeningJson.ToNumber(left[3]);
                float m1 = (float)OpeningJson.ToNumber(right[2]);
                float t2 = t * t, t3 = t2 * t;
                return (2 * t3 - 3 * t2 + 1) * y0 + (t3 - 2 * t2 + t) * duration * m0
                    + (-2 * t3 + 3 * t2) * y1 + (t3 - t2) * duration * m1;
            }
            List<object> last = OpeningJson.Array(keys[keys.Count - 1], "wish animation key");
            return (float)OpeningJson.ToNumber(last[1]);
        }

        private static void ApplyAnimationValue(Transform target, string property, float value)
        {
            if (property == "m_Alpha")
            {
                CanvasGroup group = target.GetComponent<CanvasGroup>();
                if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
                group.alpha = value;
            }
            else if ((property == "m_AnchoredPosition.x" || property == "m_AnchoredPosition.y")
                && target is RectTransform rect)
                rect.anchoredPosition = property.EndsWith(".x", StringComparison.Ordinal)
                    ? new Vector2(value, rect.anchoredPosition.y)
                    : new Vector2(rect.anchoredPosition.x, value);
            else if (property == "scale.x" || property == "m_LocalScale.x")
                target.localScale = new Vector3(value, target.localScale.y, target.localScale.z);
            else if (property == "scale.y" || property == "m_LocalScale.y")
                target.localScale = new Vector3(target.localScale.x, value, target.localScale.z);
            else if (property.StartsWith("m_Color.", StringComparison.Ordinal)
                || property == "m_fontColor.a")
            {
                Graphic graphic = target.GetComponent<Graphic>();
                if (graphic == null) return;
                Color color = graphic.color;
                switch (property.Substring(property.LastIndexOf('.') + 1))
                {
                    case "r": color.r = value; break;
                    case "g": color.g = value; break;
                    case "b": color.b = value; break;
                    case "a": color.a = value; break;
                }
                graphic.color = color;
            }
            else if (property == "m_Spacing")
            {
                HorizontalOrVerticalLayoutGroup group = target.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (group != null) group.spacing = value;
            }
        }

        private void ConfigureQualityLight(Transform middle, int quality)
        {
            string[] names = { "", "White", "Blue", "Purple", "Orange" };
            Transform lights = Find(middle, "QualityLight");
            if (lights == null) return;
            foreach (Transform child in lights)
                child.gameObject.SetActive(quality > 0 && quality < names.Length
                    && child.name == "GachaQualityLight" + names[quality]);
        }

        private Sprite ResultQualitySprite(int quality)
        {
            if (resultRoot == null) return null;
            Transform group = Find(resultRoot.transform,
                "BgPanel1/Content/MiddleGo/QualityLight/GachaQualityLight" + QualityName(quality));
            return Find(group, "Quality")?.GetComponent<Image>()?.sprite;
        }

        private static string QualityName(int quality)
        {
            switch (quality)
            {
                case 1: return "White";
                case 2: return "Blue";
                case 3: return "Purple";
                case 4: return "Orange";
                default: return "White";
            }
        }

        private bool IsSkillPiece(int itemId) => rules.Item(itemId)?.ItemType == 111;
        private bool HasConversion(OfflineWishAward award) => award.ConvertedCount > 0 || award.RecycledCount > 0;

        private string AwardDescription(OfflineWishAward award)
        {
            if (award.ConvertedCount > 0) return "重复获得，转化为 " + ItemName(award.ConvertedItemId)
                + " ×" + award.ConvertedCount;
            if (award.RecycledCount > 0) return "碎片回收获得 " + ItemName(award.RecycledItemId)
                + " ×" + award.RecycledCount;
            return IsSkillPiece(award.ItemId) ? "技能碎片" : string.Empty;
        }

        private string AwardType(OfflineWishAward award)
        {
            if (rules.Skill(award.ItemId) != null) return "職業技能";
            if (IsSkillPiece(award.ItemId)) return "技能碎片";
            return ItemName(award.ItemId);
        }

        private string QualitySound(int quality)
        {
            switch (quality)
            {
                case 1: return "GoddessPray_Rank_White";
                case 2: return "GoddessPray_Rank_Blue";
                case 3: return "GoddessPray_Rank_Purple";
                case 4: return "GoddessPray_Rank_Orange";
                default: return null;
            }
        }

        private void PlayResultSound(GameObject root, int quality) =>
            PlayResultSound(root, QualitySound(quality));

        private void PlayResultSound(GameObject root, string eventName)
        {
            AudioClip clip = string.IsNullOrEmpty(eventName) ? null : assets.FindAudio(eventName);
            if (clip == null || root == null) return;
            AudioSource source = root.GetComponent<AudioSource>();
            if (source == null) source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.PlayOneShot(clip);
        }

        private void OpenDetails(bool history)
        {
            historyPage = 0;
            if (history) ShowHistory();
            else ShowRuleDetails();
        }

        private void ShowRuleDetails()
        {
            OpenModal("gacha_rules", out _, out GameObject root);
            Transform content = Find(root.transform, "BgDialog1/Content");
            if (content == null) content = root.transform;
            AddScrollableText(content, "WishRuleText", BuildRuleDescription(), new Vector2(800, 820), 20);
            AddModalClose(root.transform);
            CreateModalButton(content, "历史记录", new Vector2(0, -460), ShowHistory);
        }

        private string BuildRuleDescription()
        {
            WishGacha gacha = rules.Gacha(poolType);
            WishChest chest = rules.Chest(gacha.ChestId);
            var lines = new List<string>
            {
                poolType == 0 ? "常驻祈愿" : poolType == 1 ? "友情祈愿" : "高阶祈愿",
                "当前职业：" + ProfessionName(poolType == 1 ? CurrentProfession : SelectedProfession),
                "累计抽取：" + service.PoolCount(poolType),
                "每日已祈愿：" + service.DailyCount(DateTime.Now),
                "概率按当前职业过滤后的固定版官方掉落配置计算；品质记录仅在抽中对应品质时重置。"
            };
            WishRewardRule sourceRule = (chest.RewardRules ?? Array.Empty<int>())
                .Select(rules.RewardRule).FirstOrDefault(value => value != null);
            if (sourceRule != null)
            {
                WishRewardRule rule = sourceRule;
                if (int.TryParse(sourceRule.RecordSharedId, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int sharedId) && sharedId > 0)
                    rule = rules.RewardRule(sharedId) ?? sourceRule;
                IReadOnlyList<WishProbabilityEntry> entries = BaseProbabilities(rule,
                    poolType == 1 ? CurrentProfession : SelectedProfession);
                lines.Add("基础概率（已按当前职业与自选过滤）");
                foreach (IGrouping<(int ItemType, int Quality), WishProbabilityEntry> group in entries
                    .GroupBy(value => (ItemType: poolType == 1 ? 0 : value.Item.ItemType, Quality: value.Item.Quality))
                    .OrderByDescending(value => value.Key.Quality).ThenBy(value => value.Key.ItemType))
                {
                    double groupChance = group.Sum(value => value.Probability) * 100d;
                    lines.Add((poolType == 1 ? "奖励" : ProbabilityTypeName(group.Key.ItemType)) + "·品质 "
                        + group.Key.Quality + "　" + groupChance.ToString("0.####", CultureInfo.InvariantCulture) + "%");
                    foreach (WishProbabilityEntry entry in group.OrderBy(value => value.Item.ClassId)
                        .ThenBy(value => value.Count))
                        lines.Add("  " + entry.Item.Name + (entry.Count < 0 ? "　数量随机" : " ×" + entry.Count)
                            + "　" +
                            (entry.Probability * 100d).ToString("0.####", CultureInfo.InvariantCulture) + "%");
                }
            }
            var pity = (rules.pityProbabilities ?? Array.Empty<WishPityProbability>())
                .Where(value => value.GachaType == poolType).OrderBy(value => value.Quality).ToArray();
            foreach (WishPityProbability row in pity)
            {
                int miss = service.QualityMissCount(poolType, row.Quality);
                lines.Add("品质 " + row.Quality + " 保底展示概率 "
                    + (row.PityProbability / 100d).ToString("0.####", CultureInfo.InvariantCulture)
                    + "%；当前连续未命中 " + miss);
            }
            return string.Join("\n", lines);
        }

        private IReadOnlyList<WishProbabilityEntry> BaseProbabilities(WishRewardRule rule, int profession)
        {
            var expanded = new List<WishProbabilityEntry>();
            int[] ids = rule.IdList ?? Array.Empty<int>();
            int[] weights = rule.WeightList ?? Array.Empty<int>();

            OfflineWishProfessionSelection selection = poolType == 0
                ? service.State.ProfessionSelections.FirstOrDefault(value => value.Profession == profession && value.Completed)
                : null;
            int[] customProfessions = selection?.SelectedProfessions?.ToArray() ?? Array.Empty<int>();
            for (int index = 0; index < ids.Length; index++)
            {
                double outerWeight = index < weights.Length ? weights[index] : 0;
                if (outerWeight <= 0) continue;
                WishItem direct = rules.Item(ids[index]);
                WishRandomReward reward = rules.RandomReward(ids[index]);
                if (direct != null && direct.ItemType == 110)
                    reward = rules.RandomReward(direct.ClassId);
                int baseCount = ScaleRuleCount(rule, index);
                if (reward == null)
                {
                    if (direct != null && ProbabilityItemAllowed(direct, profession, customProfessions))
                        expanded.Add(new WishProbabilityEntry(direct, baseCount, outerWeight));
                    continue;
                }

                List<WishItem> candidates = ProbabilityCandidates(reward,
                    poolType == 1 ? 0 : profession, customProfessions);
                if (candidates.Count == 0) continue;
                int count = reward.MinCount == reward.MaxCount
                    ? checked(baseCount * reward.MaxCount) : -1;
                int[] rewardIds = reward.IdList ?? Array.Empty<int>();
                if (rewardIds.Length > 0)
                {
                    var weighted = rewardIds.Select((id, itemIndex) => new
                    {
                        Item = rules.Item(id),
                        Weight = reward.WeightOfIdList != null && reward.WeightOfIdList.Length > itemIndex
                            ? reward.WeightOfIdList[itemIndex] : 100
                    }).Where(value => value.Item != null && candidates.Contains(value.Item) && value.Weight > 0).ToArray();
                    double innerTotal = weighted.Sum(value => (double)value.Weight);
                    if (innerTotal <= 0) continue;
                    foreach (var value in weighted)
                        expanded.Add(new WishProbabilityEntry(value.Item, count,
                            outerWeight * value.Weight / innerTotal));
                }
                else
                {
                    double chance = outerWeight / candidates.Count;
                    foreach (WishItem item in candidates)
                        expanded.Add(new WishProbabilityEntry(item, count, chance));
                }
            }
            double totalWeight = expanded.Sum(value => value.Probability);
            if (totalWeight <= 0) return Array.Empty<WishProbabilityEntry>();
            return expanded.GroupBy(value => (value.Item.ClassId, value.Count))
                .Select(group => new WishProbabilityEntry(group.First().Item, group.Key.Count,
                    group.Sum(value => value.Probability) / totalWeight))
                .OrderBy(value => value.Item.ClassId).ThenBy(value => value.Count).ToArray();
        }

        private int ScaleRuleCount(WishRewardRule rule, int index)
        {
            double count = rule.CountList != null && rule.CountList.Length > index ? rule.CountList[index] : 1d;
            int levelCountId = rule.LevelCountId != null && rule.LevelCountId.Length > index
                ? rule.LevelCountId[index] : 0;
            int subRankCountId = rule.SubRankCountId != null && rule.SubRankCountId.Length > index
                ? rule.SubRankCountId[index] : 0;
            count *= rules.LevelMultiplier(levelCountId, profile.Save.Player.Level);
            count *= rules.LevelMultiplier(subRankCountId, profile.Save.Player.SubRank);
            return (int)Math.Truncate(count);
        }

        private bool ProbabilityItemAllowed(WishItem item, int profession, int[] customProfessions)
        {
            if (item.ItemType != 100 && item.ItemType != 101
                && item.ItemType != 111 && item.ItemType != 112) return true;
            int skillId = item.ItemType == 111 || item.ItemType == 112 ? item.ClassId - 2000 : item.ClassId;
            if (poolType == 2) return rules.Skill(skillId)?.unlockProfession == profession;
            return rules.SkillAvailable(skillId, profession)
                || customProfessions.Any(value => rules.SkillAvailable(skillId, value));
        }

        private static string ProbabilityTypeName(int itemType)
        {
            switch (itemType)
            {
                case 100: return "主动技能整技";
                case 101: return "被动技能整技";
                case 111: return "主动技能碎片";
                case 112: return "被动技能碎片";
                default: return "物品类型 " + itemType;
            }
        }

        private List<WishItem> ProbabilityCandidates(WishRandomReward reward, int profession,
            int[] customProfessions)
        {
            IEnumerable<WishItem> candidates;
            if (reward.IdList != null && reward.IdList.Length > 0)
            {
                candidates = reward.IdList.Select(rules.Item).Where(value => value != null);
            }
            else
            {
                candidates = rules.items.Where(item => (reward.ItemTypes ?? Array.Empty<int>()).Contains(item.ItemType)
                    && (reward.Quality <= 0 || item.Quality == reward.Quality)
                    && (reward.Rank < 0 || item.Rank == reward.Rank)
                    && ((reward.ElementTypes ?? Array.Empty<int>()).Length == 0
                        || reward.ElementTypes.Contains(item.ElementType))
                    && !(reward.BlackList ?? Array.Empty<int>()).Contains(item.ClassId));
            }
            return candidates.Where(value => ProbabilityItemAllowed(value, profession, customProfessions))
                .OrderBy(value => value.ClassId).ToList();
        }

        private TMP_Text AddScrollableText(Transform parent, string name, string value, Vector2 size, float fontSize)
        {
            var scrollObject = new GameObject(name + "Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            RectTransform scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = new Vector2(0, -5);
            scrollRect.sizeDelta = size;
            Image background = scrollObject.GetComponent<Image>();
            background.color = new Color(0, 0, 0, 0);
            background.raycastTarget = true;

            var viewportObject = new GameObject(name + "Viewport", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;

            TMP_Text text = AddText(viewport, name, value, Vector2.zero,
                new Vector2(size.x - 40, size.y), fontSize, TextAlignmentOptions.TopLeft);
            RectTransform content = text.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-40, size.y);
            text.overflowMode = TextOverflowModes.Overflow;
            text.ForceMeshUpdate();
            content.sizeDelta = new Vector2(-40, Mathf.Max(size.y, text.preferredHeight + 24));

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            scroll.verticalNormalizedPosition = 1;
            return text;
        }

        private void ShowHistory()
        {
            OpenModal("gacha_history", out OfficialCommercePrefabRenderer renderer, out GameObject root);
            const int pageSize = 10;
            long cutoff = Math.Max(0, Now() - 180L * 86400L);
            int recordCount = service.State.History.Count(value => value.GachaType == poolType
                && value.UnixSeconds >= cutoff);
            int pageCount = recordCount == 0 ? 0 : (recordCount + pageSize - 1) / pageSize;
            historyPage = pageCount == 0 ? 0 : Mathf.Clamp(historyPage, 0, pageCount - 1);
            Transform rows = Find(root.transform, "GoItems/HistoryItemSlot");
            if (rows == null) rows = Find(root.transform, "HistoryItemSlot");
            if (rows != null)
            {
                ClearChildren(rows);
                IReadOnlyList<OfflineWishDrawRecord> records = service.History(poolType, Now(), historyPage, pageSize);
                foreach (OfflineWishDrawRecord record in records)
                {
                    CommercePrefab rowPrefab = assets.Load().Prefab("gacha_history_row");
                    var rowRenderer = NewRenderer();
                    GameObject row = rowRenderer.CreateSubtree(rowPrefab.root, rows);
                    OfflineWishAward award = record.Rewards.FirstOrDefault(value => value.ItemId != rules.Gacha(poolType).RewardScoreId)
                        ?? record.Rewards[0];
                    SetTextDeep(row.transform, "ItemTypeText", award.Quality > 0 ? "品质 " + award.Quality : "祈愿奖励");
                    SetTextDeep(row.transform, "ItemNameText", ItemName(award.ItemId) + " ×" + award.Count);
                    SetTextDeep(row.transform, "GotTimeText", DateTimeOffset.FromUnixTimeSeconds(record.UnixSeconds)
                        .ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    SetIconDeep(row.transform, award.ItemId);
                }
                OfficialCommercePrefabRenderer.RebuildLayout(rows.gameObject);
            }
            Transform prev = Find(root.transform, "TurnPageUI/PrevBtn");
            Transform next = Find(root.transform, "TurnPageUI/NextBtn");
            BindAction(prev, () => { if (historyPage > 0) { historyPage--; ShowHistory(); } });
            BindAction(next, () => { if (historyPage + 1 < pageCount) { historyPage++; ShowHistory(); } });
            if (prev != null && prev.GetComponent<Button>() != null)
                prev.GetComponent<Button>().interactable = historyPage > 0;
            if (next != null && next.GetComponent<Button>() != null)
                next.GetComponent<Button>().interactable = historyPage + 1 < pageCount;
            SetTextDeep(root.transform, "CurPageText", pageCount == 0 ? "0/0" :
                (historyPage + 1).ToString(CultureInfo.InvariantCulture) + "/" + pageCount.ToString(CultureInfo.InvariantCulture));
            AddModalClose(root.transform);
        }

        private void OpenAchievementPage(bool score)
        {
            achievementPage = 0;
            achievementGroup = GroupFor(poolType, score);
            SetMainPage(score ? 0 : 2);
            ShowAchievements();
        }

        private void ShowAchievements()
        {
            OpenModal("gacha_score", out _, out GameObject root);
            Transform list = Find(root.transform, "BgDialog1/Content/RewardItemContent/Scroll View/Viewport/RewardItemSlot");
            if (list == null) list = Find(root.transform, "RewardItemSlot");
            if (list != null)
            {
                ClearChildren(list);
                IReadOnlyList<WishAchievementEntry> entries = service.Achievements(achievementGroup, achievementPage, PageSize);
                foreach (WishAchievementEntry entry in entries)
                {
                    CommercePrefab itemPrefab = assets.Load().Prefab("gacha_score_item");
                    var itemRenderer = NewRenderer();
                    GameObject row = itemRenderer.CreateSubtree(itemPrefab.root, list);
                    SetTextDeep(row.transform, "Titletext", "祈愿累计 " + entry.Target);
                    SetTextDeep(row.transform, "text", entry.CurrentValue + "/" + entry.Target);
                    SetActiveDeep(row.transform, "GetAwardBtn", entry.IsReady && !entry.IsClaimed);
                    SetActiveDeep(row.transform, "UnFinishedBtn", !entry.IsReady || entry.IsClaimed);
                    if (entry.IsClaimed) SetTextDeep(FindDeep(row.transform, "UnFinishedBtn"), "Text", "已领取");
                    Transform awardGrid = Find(row.transform, "AwardGrid");
                    if (awardGrid != null)
                        foreach (WishAmount amount in entry.Rewards ?? Array.Empty<WishAmount>())
                            AddAwardLine(awardGrid, amount.ItemId,
                                ItemName(amount.ItemId) + " ×" + amount.Count);
                    BindAction(Find(row.transform, "GetAwardBtn"), () =>
                    {
                        WishResult claim = service.ClaimAchievement(achievementGroup, entry.Index,
                            Receipt("achievement"), Now());
                        ShowAchievements();
                        if (claim.IsApplied) ShowAwardPopup("领取成功", "获得以下奖励", claim.Awards);
                        else ShowNotice(claim.Message);
                    });
                }
                OfficialCommercePrefabRenderer.RebuildLayout(list.gameObject);
            }
            SetTextDeep(root.transform, "RuleText", "祈愿积分与馈赠进度：" + service.PoolCount(poolType));
            SetTextDeep(root.transform, "EmptyText", "当前页没有可显示的奖励");
            AddModalClose(root.transform);
            CreateModalButton(Find(root.transform, "BgDialog1/Content") ?? root.transform,
                "上一页", new Vector2(-170, -490), () => { if (achievementPage > 0) achievementPage--; ShowAchievements(); });
            CreateModalButton(Find(root.transform, "BgDialog1/Content") ?? root.transform,
                "下一页", new Vector2(170, -490), () => { achievementPage++; ShowAchievements(); });
        }

        private void OpenProfessionPicker()
        {
            if (poolType == 1) return;
            OpenModal("gacha_professions", out _, out GameObject root);
            SetTextDeep(root.transform, "TtleText", "选择祈愿职业");
            Transform grid = Find(root.transform, "BgDialog1/Content/ScrollGo/GridSlot");
            if (grid != null)
            {
                ClearChildren(grid);
                foreach (WishProfession profession in AvailableProfessions())
                {
                    GameObject row = CreateProfessionChoice(grid, profession);
                    BindAction(row.transform, () =>
                    {
                        WishResult changed = service.SetDrawProfession(poolType, profession.Profession,
                            Receipt("profession"));
                        if (changed.Code == WishResultCode.Applied || changed.Code == WishResultCode.Unchanged)
                        {
                            CloseModal();
                            RenderPool();
                        }
                        else ShowNotice(changed.Message);
                    });
                }
            }
            BindAction(Find(root.transform, "ConfirmBtn"), CloseModal);
            BindAction(Find(root.transform, "CancelBtn"), CloseModal);
        }

        private void OpenCustomSelection()
        {
            if (poolType != 0) return;
            WishProfession sourceProfession = rules.Profession(SelectedProfession);
            if (sourceProfession == null || sourceProfession.Rank < rules.settings.SkillGachaCustomSelectionSettings.RequiredProfessionRank)
            {
                ShowNotice("达到指定职业阶级后开启自选");
                return;
            }
            OpenModal("gacha_professions", out _, out GameObject root);
            SetTextDeep(root.transform, "TtleText", "常驻祈愿职业技能自选");
            Transform grid = Find(root.transform, "BgDialog1/Content/ScrollGo/GridSlot");
            Transform selectedGroup = Find(root.transform, "BgDialog1/Content/SelectionGroup");
            var selected = new HashSet<int>();
            OfflineWishProfessionSelection saved = service.State.ProfessionSelections
                .FirstOrDefault(value => value.Profession == sourceProfession.Profession);
            if (saved != null) foreach (int value in saved.SelectedProfessions) selected.Add(value);
            HashSet<int> predecessors = new HashSet<int>(rules.ProfessionChain(sourceProfession.Profession).Skip(1));
            if (grid != null)
            {
                ClearChildren(grid);
                foreach (WishProfession profession in UnlockedProfessions()
                    .Where(value => predecessors.Contains(value.Profession)))
                {
                    GameObject row = CreateProfessionChoice(grid, profession);
                    Button button = row.GetComponent<Button>();
                    SetTextDeep(row.transform, "ProfessionNameText", ProfessionName(profession.Profession));
                    SetSelectedChoice(row, selected.Contains(profession.Profession));
                    button.onClick.AddListener(() =>
                    {
                        if (selected.Contains(profession.Profession)) selected.Remove(profession.Profession);
                        else if (selected.Count >= rules.settings.SkillGachaCustomSelectionSettings.SelectionProfessionCount)
                        {
                            ShowNotice("最多选择 " + rules.settings.SkillGachaCustomSelectionSettings.SelectionProfessionCount + " 个职业");
                            return;
                        }
                        else selected.Add(profession.Profession);
                        SetSelectedChoice(row, selected.Contains(profession.Profession));
                        SetTextDeep(root.transform, "TitleText", "已选择 " + selected.Count + "/4 个职业");
                        RefreshCustomSelectionBadges(selectedGroup, sourceProfession, selected);
                    });
                }
            }
            if (selectedGroup != null) selectedGroup.gameObject.SetActive(true);
            SetTextDeep(root.transform, "TitleText", "已选择 " + selected.Count + "/4 个职业");
            RefreshCustomSelectionBadges(selectedGroup, sourceProfession, selected);
            Button confirm = FindButton(root.transform, "ConfirmBtn");
            if (confirm != null) confirm.onClick.AddListener(() =>
            {
                int required = rules.settings.SkillGachaCustomSelectionSettings.SelectionProfessionCount;
                if (selected.Count != required)
                {
                    ShowNotice("請選擇 " + required + " 個職業");
                    return;
                }
                WishResult savedResult = service.SetProfessionSelection(sourceProfession.Profession,
                    selected, Receipt("custom-selection"));
                if (savedResult.Code == WishResultCode.Applied || savedResult.Code == WishResultCode.Unchanged)
                {
                    CloseModal();
                    RenderPool();
                }
                else ShowNotice(savedResult.Message);
            });
            Button cancel = FindButton(root.transform, "CancelBtn");
            if (cancel != null) cancel.onClick.AddListener(CloseModal);
        }

        private GameObject CreateProfessionChoice(Transform parent, WishProfession profession)
        {
            CommercePrefab prefab = assets.Load().Prefab("gacha_profession_choice");
            var renderer = NewRenderer();
            GameObject row = renderer.CreateSubtree(prefab.root, parent);
            row.name = "Profession_" + profession.Profession;
            SetTextDeep(row.transform, "ProfessionNameText", ProfessionName(profession.Profession));
            SetProfessionLabelColor(row.transform, "ProfessionNameText", profession);
            SetTextDeep(row.transform, "ProfessionLevelText", "階級 " + profession.Rank);
            SetTextDeep(row.transform, "ProfessionRankText", profession.Rank.ToString(CultureInfo.InvariantCulture));
            Sprite icon = assets.FindSprite(profession.Icon1File);
            if (icon != null) SetSpriteDeep(row.transform, "ProfessionIcon", icon);
            Button button = row.GetComponent<Button>();
            if (button == null) button = row.AddComponent<Button>();
            if (button.targetGraphic == null) button.targetGraphic = row.GetComponentInChildren<Graphic>();
            return row;
        }

        private static void SetProfessionLabelColor(Transform root, string name, WishProfession profession)
        {
            Transform node = FindDeep(root, name);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text != null && profession != null
                && ColorUtility.TryParseHtmlString("#" + profession.TextColor, out Color color)) text.color = color;
        }

        private void RefreshCustomSelectionBadges(Transform group, WishProfession current, HashSet<int> selected)
        {
            if (group == null) return;
            FillProfessionIcon(Find(group, "SkillGachaPanelProfessionIcon"), current);
            WishProfession[] ordered = selected.Select(rules.Profession).Where(value => value != null)
                .OrderByDescending(value => value.Rank).ThenBy(value => value.Profession).ToArray();
            for (int index = 0; index < 4; index++)
            {
                string name = index == 0 ? "SkillGachaPanelProfessionIcon (1)"
                    : "SkillGachaPanelProfessionIcon (" + (index + 1) + ")";
                FillProfessionIcon(Find(group, name), index < ordered.Length ? ordered[index] : null);
            }
        }

        private void SetSelectedChoice(GameObject row, bool selected)
        {
            Transform toggle = Find(row.transform, "Content/Toggle");
            if (toggle != null)
            {
                SetActiveDeep(toggle, "On", selected);
                SetActiveDeep(toggle, "Off", !selected);
            }
        }

        private IEnumerable<WishProfession> AvailableProfessions()
        {
            return service.AvailableDrawProfessions(poolType, CurrentProfession)
                .Select(rules.Profession).Where(value => value != null);
        }

        private IEnumerable<WishProfession> UnlockedProfessions()
        {
            int rank = Math.Max(1, profile.Save.Player.MaxProfessionRank);
            return rules.professions.Where(value => value != null && value.Rank <= rank)
                .OrderByDescending(value => value.Rank).ThenBy(value => value.Profession);
        }

        private void SelectShop(int id)
        {
            shopId = id;
            service.EnsureShopDay(shopId, CurrentProfession, DateTime.Now);
            RefreshShop();
        }

        private void RefreshShop()
        {
            UpdateWishTopBar();
            IReadOnlyList<WishShopStockView> goods = service.ShopGoods(shopId, CurrentProfession, DateTime.Now);
            goods = goods.Where(MatchesShopFilters).ToArray();
            if (shopContent != null) shopContent.gameObject.SetActive(shopId == 5);
            if (fixedShopContent != null) fixedShopContent.gameObject.SetActive(shopId == 4);
            if (shopId == 5)
            {
                if (fixedShopContent != null) ClearChildren(fixedShopContent);
                PopulateGoods(shopContent, goods);
            }
            else
            {
                if (shopContent != null) ClearChildren(shopContent);
                PopulateGoods(fixedShopContent, goods);
            }
            WishShop shop = rules.Shop(shopId);
            OfflineWishShopState state = service.State.Shops.FirstOrDefault(value => value.ShopId == shopId);
            Transform refreshGo = Find(shell.transform,
                "BgPanel1/Content/MiddleGo/ShopContent/ShopGoodsContent/ShopContentTopGo/RefreshGo");
            SetActive(refreshGo, shopId == 5);
            SetActive(Find(shell.transform,
                "BgPanel1/Content/MiddleGo/ShopContent/LineImgWhenCanRefresh"),
                shopId == 5 && state != null && state.ManualRefreshCount < shop.RefreshCountsLimit);
            SetTextDeep(shell.transform, "RefreshLimitTxt", shopId == 5
                ? Math.Max(0, shop.RefreshCountsLimit - (state?.ManualRefreshCount ?? 0)).ToString(CultureInfo.InvariantCulture)
                : string.Empty);
            SetTextDeep(shell.transform, "RefreshCountTxt", service.ManualRefreshCost(shopId,
                state?.ManualRefreshCount ?? 0).ToString(CultureInfo.InvariantCulture));
            Button refreshButton = FindButton(shell.transform,
                "BgPanel1/Content/MiddleGo/ShopContent/ShopGoodsContent/ShopContentTopGo/RefreshGo/RefreshInfoGo/RefreshBtn");
            if (refreshButton != null)
            {
                int refreshCurrency = shop.RefreshCurrency == null || shop.RefreshCurrency.Length == 0
                    ? 0 : shop.RefreshCurrency[0];
                int refreshCost = service.ManualRefreshCost(shopId, state?.ManualRefreshCount ?? 0);
                refreshButton.interactable = shopId == 5 && state != null
                    && state.ManualRefreshCount < shop.RefreshCountsLimit && refreshCost > 0
                    && refreshCurrency > 0 && profile.InventoryCount(refreshCurrency) >= refreshCost;
                if (shop.RefreshCurrency != null && shop.RefreshCurrency.Length > 0)
                    SetIconDeep(refreshButton.transform, shop.RefreshCurrency[0]);
            }
            if (shopId == 5) UpdateShopCountdown();
        }

        private void UpdateShopCountdown()
        {
            DateTime now = DateTime.Now;
            TimeSpan remaining = now.Date.AddDays(1) - now;
            string value = remaining.Days > 0
                ? remaining.Days.ToString(CultureInfo.InvariantCulture) + "天 "
                    + remaining.Hours.ToString("00", CultureInfo.InvariantCulture) + ":"
                    + remaining.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":"
                    + remaining.Seconds.ToString("00", CultureInfo.InvariantCulture)
                : remaining.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            SetTextDeep(shell == null ? null : shell.transform, "CountDownText", value);
        }

        private void OpenShopFilters()
        {
            IReadOnlyList<WishShopStockView> stocks = service.ShopGoods(shopId, CurrentProfession, DateTime.Now);
            var items = stocks.Select(value => rules.ShopGood(value.GoodsId))
                .Where(value => value != null).Select(value => rules.Item(value.ItemId))
                .Where(value => value != null).Distinct().ToArray();
            var qualities = items.Select(value => value.Quality).Distinct().OrderBy(value => value).ToArray();
            var elements = items.Select(value => value.ElementType).Distinct().OrderBy(value => value).ToArray();
            var types = items.Select(value => value.ItemType).Distinct().OrderBy(value => value).ToArray();
            var professions = items.Select(ItemProfession).Where(value => value > 0).Distinct().OrderBy(value => value).ToArray();
            var nextQuality = new HashSet<int>(shopQualityFilters);
            var nextElement = new HashSet<int>(shopElementFilters);
            var nextType = new HashSet<int>(shopTypeFilters);
            var nextProfession = new HashSet<int>(shopProfessionFilters);

            OpenModal("gacha_professions", out _, out GameObject root);
            SetTextDeep(root.transform, "TtleText", "筛选商品");
            SetActiveDeep(root.transform, "ScrollGo", false);
            SetActiveDeep(root.transform, "SelectionGroup", false);
            Transform body = Find(root.transform, "BgDialog1/Content") ?? root.transform;
            Transform list = CreateScrollList(body, "WishShopFilterList", new Vector2(760, 650), new Vector2(0, -5));
            AddFilterOptions(list, "品质", qualities, nextQuality, value => "品质 " + value);
            AddFilterOptions(list, "元素", elements, nextElement, value => value <= 0 ? "无元素" : "元素 " + value);
            AddFilterOptions(list, "类型", types, nextType, value => value switch
            {
                100 => "主动技能", 101 => "被动技能", 111 => "主动技能碎片", 112 => "被动技能碎片",
                _ => "物品类型 " + value
            });
            AddFilterOptions(list, "职业", professions, nextProfession, ProfessionName);

            Button confirm = FindButton(root.transform, "ConfirmBtn");
            if (confirm != null)
            {
                confirm.onClick.RemoveAllListeners();
                confirm.onClick.AddListener(() =>
                {
                    CopyFilters(nextQuality, shopQualityFilters);
                    CopyFilters(nextElement, shopElementFilters);
                    CopyFilters(nextType, shopTypeFilters);
                    CopyFilters(nextProfession, shopProfessionFilters);
                    CloseModal();
                    RefreshShop();
                });
                SetTextDeep(confirm.transform, "Text", "应用");
            }
            BindAction(Find(root.transform, "CancelBtn"), CloseModal);
        }

        private void AddFilterOptions(Transform content, string category, IEnumerable<int> values,
            HashSet<int> selected, Func<int, string> label)
        {
            TMP_Text heading = AddText(content, "WishFilterHeading_" + category, category,
                Vector2.zero, new Vector2(700, 48), 23, TextAlignmentOptions.Left);
            LayoutElement headingLayout = heading.gameObject.AddComponent<LayoutElement>();
            headingLayout.preferredHeight = 48;
            foreach (int value in values)
            {
                Toggle toggle = AddToggle(content, label(value));
                if (toggle == null) continue;
                LayoutElement rowLayout = toggle.gameObject.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 56;
                toggle.isOn = selected.Contains(value);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) selected.Add(value);
                    else selected.Remove(value);
                });
            }
        }

        private Transform CreateScrollList(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var scrollObject = new GameObject(name + "Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            RectTransform scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = position;
            scrollRect.sizeDelta = size;
            scrollObject.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var viewportObject = new GameObject(name + "Viewport", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;

            var contentObject = new GameObject(name + "Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform content = (RectTransform)contentObject.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            return content;
        }

        private bool MatchesShopFilters(WishShopStockView stock)
        {
            WishShopGood good = rules.ShopGood(stock.GoodsId);
            WishItem item = good == null ? null : rules.Item(good.ItemId);
            if (item == null) return false;
            return (shopQualityFilters.Count == 0 || shopQualityFilters.Contains(item.Quality))
                && (shopElementFilters.Count == 0 || shopElementFilters.Contains(item.ElementType))
                && (shopTypeFilters.Count == 0 || shopTypeFilters.Contains(item.ItemType))
                && (shopProfessionFilters.Count == 0 || shopProfessionFilters.Contains(ItemProfession(item)));
        }

        private int ItemProfession(WishItem item)
        {
            if (item == null) return 0;
            int skillId = item.ItemType == 111 || item.ItemType == 112 ? item.ClassId - 2000 : item.ClassId;
            return rules.Skill(skillId)?.unlockProfession ?? 0;
        }

        private static void CopyFilters(HashSet<int> source, HashSet<int> destination)
        {
            destination.Clear();
            destination.UnionWith(source);
        }

        private void PopulateGoods(Transform content, IReadOnlyList<WishShopStockView> stocks)
        {
            if (content == null) return;
            ClearChildren(content);
            foreach (WishShopStockView stock in stocks)
            {
                WishShopGood good = rules.ShopGood(stock.GoodsId);
                WishItem item = good == null ? null : rules.Item(good.ItemId);
                if (good == null || item == null) continue;
                CommercePrefab prefab = assets.Load().Prefab("shop_goods");
                var renderer = NewRenderer();
                GameObject row = renderer.CreateSubtree(prefab.root, content);
                row.name = "WishGoods_" + good.Id + "_" + stock.Slot;
                SetTextDeep(row.transform, "PresentPriceText", ItemName(good.Currency) + " ×" +
                    good.OriginalPrice.ToString("0.##", CultureInfo.InvariantCulture));
                SetTextDeep(row.transform, "Inventory", stock.RemainingInventory < 0
                    ? "不限购" : "库存 " + stock.RemainingInventory);
                SetTextDeep(row.transform, "BottomTxt", item.Name + " ×" + good.Count);
                SetTextDeep(row.transform, "GotTxt", profile.InventoryCount(good.ItemId) > 0 ? "已持有" : string.Empty);
                SetIconDeep(row.transform, good.ItemId);
                Transform buyButton = Find(row.transform, "BuyBtn");
                Button purchase = buyButton == null ? null : buyButton.GetComponent<Button>();
                if (purchase != null)
                    purchase.interactable = stock.RemainingInventory != 0
                        && (good.TotalBuyCountLimit < 0 || stock.BoughtCount < good.TotalBuyCountLimit)
                        && (good.OriginalPrice <= 0 || profile.InventoryCount(good.Currency) >= Math.Ceiling(good.OriginalPrice));
                BindAction(buyButton, () => OpenBuyDialog(good, stock));
                BindAction(Find(row.transform, "ItemInfoBtn"), () => ShowItemDetails(item));
            }
            OfficialCommercePrefabRenderer.RebuildLayout(content.gameObject);
        }

        private void OpenBuyDialog(WishShopGood good, WishShopStockView stock)
        {
            int max = good.SupportBuyMultiCount
                ? (good.Inventory < 0 ? int.MaxValue : good.Inventory) : 1;
            if (stock.RemainingInventory >= 0) max = Math.Min(max, stock.RemainingInventory);
            if (good.TotalBuyCountLimit >= 0) max = Math.Min(max, good.TotalBuyCountLimit - stock.BoughtCount);
            if (good.OriginalPrice > 0)
                max = (int)Math.Min(max, Math.Floor(profile.InventoryCount(good.Currency) / good.OriginalPrice));
            if (max <= 0)
            {
                ShowNotice("货币不足或商品已达到购买上限");
                return;
            }
            buyCount = 1;
            buyQuantityMax = max;
            ShowConfirmation("购买确认", ItemName(good.ItemId) + " ×" + good.Count
                + "\n单价 " + ItemName(good.Currency) + " ×" +
                good.OriginalPrice.ToString("0.##", CultureInfo.InvariantCulture), () =>
            {
                WishResult result = service.BuyShopGood(shopId, good.Id, stock.Slot, buyCount,
                    CurrentProfession, Now(), Receipt("shop-buy"));
                RefreshShop();
                if (result.IsApplied) ShowAwardPopup("购买完成", "获得以下物品", result.Awards);
                else ShowNotice(result.Message);
            }, false);
            AddStepper(modal.transform, max, good);
            RefreshBuyText(good);
        }

        private void AddStepper(Transform parent, int max, WishShopGood good)
        {
            buyQuantityLabel = Find(parent, "WishBuyQuantity")?.GetComponent<TMP_Text>();
            CreateModalButton(parent, "−", new Vector2(-200, -50), () =>
            {
                buyCount = Mathf.Max(1, buyCount - 1);
                RefreshBuyText(good);
            });
            CreateModalButton(parent, "+", new Vector2(200, -50), () =>
            {
                buyCount = Mathf.Min(max, buyCount + 1);
                RefreshBuyText(good);
            });
            if (good.SupportBuyMultiCount && max > 1)
            {
                buyQuantitySlider = CreateQuantitySlider(parent, max);
                buyQuantitySlider.onValueChanged.AddListener(value =>
                {
                    buyCount = Mathf.Clamp(Mathf.RoundToInt(value), 1, buyQuantityMax);
                    RefreshBuyText(good);
                });
                CreateModalButton(parent, "最大", new Vector2(0, -112), () =>
                {
                    buyCount = buyQuantityMax;
                    RefreshBuyText(good);
                });
            }
            else buyQuantitySlider = null;
            RefreshBuyText(good);
        }

        private void RefreshBuyText(WishShopGood good)
        {
            if (buyQuantityLabel != null) buyQuantityLabel.text = "购买数量 " + buyCount + "；合计 " +
                ItemName(good.Currency) + " ×" + Math.Ceiling(good.OriginalPrice * buyCount)
                    .ToString(CultureInfo.InvariantCulture);
            else SetTextDeep(modal.transform, "WishBuyQuantity", "购买数量 " + buyCount + "；合计 " +
                ItemName(good.Currency) + " ×" + Math.Ceiling(good.OriginalPrice * buyCount)
                    .ToString(CultureInfo.InvariantCulture));
            if (buyQuantitySlider != null && Mathf.RoundToInt(buyQuantitySlider.value) != buyCount)
                buyQuantitySlider.SetValueWithoutNotify(buyCount);
        }

        private Slider CreateQuantitySlider(Transform parent, int max)
        {
            var root = new GameObject("WishBuyQuantitySlider", typeof(RectTransform), typeof(Slider));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -50);
            rect.sizeDelta = new Vector2(300, 32);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.SetParent(rect, false);
            backgroundRect.anchorMin = new Vector2(0, 0.35f);
            backgroundRect.anchorMax = new Vector2(1, 0.65f);
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color32(54, 57, 62, 255);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.SetParent(rect, false);
            fillAreaRect.anchorMin = new Vector2(0, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1, 0.65f);
            fillAreaRect.offsetMin = new Vector2(2, 0);
            fillAreaRect.offsetMax = new Vector2(-2, 0);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color32(210, 176, 102, 255);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.SetParent(rect, false);
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(12, 0);
            handleAreaRect.offsetMax = new Vector2(-12, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = new Vector2(28, 28);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color32(245, 228, 190, 255);
            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 1;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = 1;
            slider.direction = Slider.Direction.LeftToRight;
            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            return slider;
        }

        private void ConfirmRefreshShop()
        {
            int count = service.State.Shops.FirstOrDefault(value => value.ShopId == shopId)?.ManualRefreshCount ?? 0;
            int cost = service.ManualRefreshCost(shopId, count);
            if (cost < 0)
            {
                ShowNotice("該商店不能手動刷新");
                return;
            }
            ShowConfirmation("刷新貨架", "消耗 " + cost + " 枚祈願幣刷新全部商品？", () =>
            {
                WishResult result = service.RefreshShop(shopId, CurrentProfession, DateTime.Now,
                    Receipt("shop-refresh"));
                ShowNotice(result.IsApplied ? "貨架已刷新" : result.Message);
                RefreshShop();
            }, false);
        }

        private void ShowItemDetails(WishItem item)
        {
            OpenModal("gacha_rules", out _, out GameObject root);
            Transform content = Find(root.transform, "BgDialog1/Content") ?? root.transform;
            AddText(content, "WishItemDetails", item.Name + "\n品質 " + item.Quality + "　階級 " + item.Rank
                + "\n持有數量 " + profile.InventoryCount(item.ClassId), new Vector2(0, -20), new Vector2(760, 450), 28);
            SetIconDeep(root.transform, item.ClassId);
            AddModalClose(root.transform);
        }

        private void ShowGuideReward(IReadOnlyList<OfflineWishAward> awards)
        {
            ShowAwardPopup("祈愿引导奖励", "获得以下祈愿奖励", awards);
        }

        private void ShowAwardPopup(string title, string message, IReadOnlyList<OfflineWishAward> awards)
        {
            OpenModal("gacha_professions", out _, out GameObject root);
            SetTextDeep(root.transform, "TtleText", title);
            SetTextDeep(root.transform, "BgDialog1/Content/Title/TitleText", message);
            Transform content = Find(root.transform, "BgDialog1/Content") ?? root.transform;
            SetActive(Find(content, "ScrollGo"), false);
            SetActive(Find(content, "SelectionGroup"), false);
            float listHeight = Mathf.Clamp((awards == null ? 0 : awards.Count) * 72f, 120f, 360f);
            Transform list = CreateVertical(content, "WishAwardItems", new Vector2(0, 80),
                new Vector2(760, listHeight), 8);
            foreach (OfflineWishAward award in awards ?? Array.Empty<OfflineWishAward>())
            {
                string text = ItemName(award.ItemId) + " ×" + award.Count;
                if (award.ConvertedItemId > 0)
                    text += " → " + ItemName(award.ConvertedItemId) + " ×" + award.ConvertedCount;
                if (award.RecycledItemId > 0)
                    text += " → " + ItemName(award.RecycledItemId) + " ×" + award.RecycledCount;
                AddAwardLine(list, award.ItemId, text);
            }
            Button close = FindButton(root.transform, "ConfirmBtn");
            SetActive(FindDeep(root.transform, "CancelBtn"), false);
            if (close == null) throw new InvalidOperationException("The official wish award dialog has no close button.");
            SetTextDeep(close.transform, "Text", "关闭");
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(CloseModal);
            OfficialCommercePrefabRenderer.RebuildLayout(list.gameObject);
        }

        private void ShowConfirmation(string title, string message, Action confirm, bool hundredPrompt)
        {
            OpenModal("gacha_professions", out _, out GameObject root);
            Transform scroll = Find(root.transform, "BgDialog1/Content/ScrollGo");
            Transform selection = Find(root.transform, "BgDialog1/Content/SelectionGroup");
            if (scroll != null) scroll.gameObject.SetActive(false);
            if (selection != null) selection.gameObject.SetActive(false);
            SetTextDeep(root.transform, "TtleText", title);
            Transform target = Find(root.transform, "BgDialog1/Content/Title/TitleText");
            if (target != null && target.GetComponent<TMP_Text>() != null)
                target.GetComponent<TMP_Text>().text = message;
            Transform content = Find(root.transform, "BgDialog1/Content") ?? root.transform;
            AddText(content, "WishBuyQuantity", message, new Vector2(0, -180), new Vector2(700, 90), 22);
            Button ok = FindButton(root.transform, "ConfirmBtn");
            Button cancel = FindButton(root.transform, "CancelBtn");
            if (ok != null)
            {
                ok.onClick.RemoveAllListeners();
                ok.onClick.AddListener(() =>
                {
                    confirmedHundredReminderSuppressed = hundredPrompt
                        && suppressHundredToggle != null && suppressHundredToggle.isOn;
                    CloseModal();
                    confirm?.Invoke();
                });
            }
            if (cancel != null)
            {
                cancel.onClick.RemoveAllListeners();
                cancel.onClick.AddListener(() =>
                {
                    confirmedHundredReminderSuppressed = false;
                    CloseModal();
                });
            }
            if (hundredPrompt)
            {
                suppressHundredToggle = AddToggle(content, "今日不再提示");
                if (suppressHundredToggle != null) suppressHundredToggle.isOn = false;
            }
            else suppressHundredToggle = null;
        }

        private Toggle AddToggle(Transform parent, string label)
        {
            CommercePrefab choicePrefab = assets.Load().Prefab("gacha_profession_choice");
            CommerceUiNode choiceSource = FindNode(choicePrefab.root, "Toggle");
            if (choiceSource == null) return null;
            GameObject go = NewRenderer().CreateSubtree(choiceSource, parent);
            go.name = "WishToggle";
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -150);
            rect.sizeDelta = new Vector2(350, 54);
            Image off = Find(go.transform, "Off")?.GetComponent<Image>();
            Image on = Find(go.transform, "On")?.GetComponent<Image>();
            Toggle toggle = go.GetComponent<Toggle>();
            if (toggle == null) toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = off;
            toggle.graphic = on;
            TMP_Text text = AddText(go.transform, "WishToggleLabel", label,
                new Vector2(155, 0), new Vector2(270, 50), 22, TextAlignmentOptions.Left);
            text.transform.SetAsLastSibling();
            return toggle;
        }

        private void CreateModalButton(Transform parent, string label, Vector2 position, Action action)
        {
            CommercePrefab buttonPrefab = assets.Load().Prefab("gacha_professions");
            CommerceUiNode source = FindNode(buttonPrefab.root,
                label == "关闭" || label == "取消" ? "CancelBtn" : "ConfirmBtn");
            GameObject go = NewRenderer().CreateSubtree(source, parent);
            go.name = "WishAction_" + label;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            Button button = go.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => action?.Invoke());
            SetTextDeep(go.transform, "Text", label);
        }

        private void OpenModal(string prefabKey, out OfficialCommercePrefabRenderer renderer, out GameObject root)
        {
            CloseModal();
            modal = new GameObject("WishModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(canvasRoot, false);
            RectTransform dimRect = (RectTransform)modal.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            Image dim = modal.GetComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.62f);
            renderer = NewRenderer();
            CommercePrefab source = assets.Load().Prefab(prefabKey);
            root = renderer.CreateSubtree(source.root, modal.transform);
            root.name = "OfficialWish" + prefabKey;
            AddModalClose(root.transform);
        }

        private void AddModalClose(Transform root)
        {
            bool boundClose = false;
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.IndexOf("CloseBtn", StringComparison.OrdinalIgnoreCase) < 0) continue;
                boundClose = true;
                Button button = node.GetComponent<Button>();
                if (button == null) button = node.gameObject.AddComponent<Button>();
                if (button.targetGraphic == null) button.targetGraphic = node.GetComponentInChildren<Graphic>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(CloseModal);
            }
            bool needsFallback = root.name == "OfficialWishgacha_rules"
                || root.name == "OfficialWishgacha_history"
                || root.name == "OfficialWishgacha_score";
            if (!boundClose && needsFallback && Find(root, "WishAction_关闭") == null)
            {
                Transform dialog = Find(root, "BgDialog1") ?? root;
                RectTransform dialogRect = dialog as RectTransform;
                float closeY = dialogRect == null ? -630 : dialogRect.rect.yMin + 120;
                CreateModalButton(dialog, "关闭", new Vector2(0, closeY), CloseModal);
            }
        }

        private void CloseModal()
        {
            StopAllCoroutines();
            resultSequence = null;
            resultQualitySequence = null;
            resultRoot = null;
            skipResultSequence = true;
            buyQuantityLabel = null;
            buyQuantitySlider = null;
            buyQuantityMax = 0;
            if (modal == null) return;
            Destroy(modal);
            modal = null;
            suppressHundredToggle = null;
        }

        private void SetMainPage(int page)
        {
            mainPage = page;
            Transform gacha = Find(shell.transform, "BgPanel1/Content/MiddleGo/Content");
            Transform shop = Find(shell.transform, "BgPanel1/Content/MiddleGo/ShopContent");
            Transform gifts = Find(shell.transform, "BgPanel1/Content/MiddleGo/AchievementPanel");
            if (gacha != null) gacha.gameObject.SetActive(page == 0);
            if (shop != null) shop.gameObject.SetActive(page == 1);
            if (gifts != null) gifts.gameObject.SetActive(page == 2);
            if (page == 1) RefreshShop();
        }

        private void RefreshChrome()
        {
            if (!IsOpen && shell == null) return;
            UpdateWishTopBar();
            WishGacha gacha = rules.Gacha(poolType);
            WishChest chest = rules.Chest(gacha.ChestId);
            int profession = poolType == 1 ? CurrentProfession : SelectedProfession;
            SetTextDeep(poolPanel == null ? shell.transform : poolPanel.transform, "TextScore",
                profile.InventoryCount(gacha.RewardScoreId).ToString(CultureInfo.InvariantCulture));
            SetTextDeep(poolPanel == null ? shell.transform : poolPanel.transform, "DailyCountText",
                "今日已祈愿" + service.DailyCount(DateTime.Now) + "/" + DailyLimit(gacha.GachaClassType) + "次");
            SetTextDeep(poolPanel == null ? shell.transform : poolPanel.transform, "ProfessionText", ProfessionName(profession));
            SetProfessionLabelColor(poolPanel == null ? shell.transform : poolPanel.transform,
                "ProfessionText", rules.Profession(profession));
            SetTextDeep(shell.transform, "DataText", service.PoolCount(poolType).ToString(CultureInfo.InvariantCulture));
            SetTextDeep(shell.transform, "RefreshInfoGo", string.Empty);
            SetPoolCostButtons(chest);
        }

        private void SetPoolCostButtons(WishChest chest)
        {
            if (poolPanel == null) return;
            foreach (Transform node in poolPanel.GetComponentsInChildren<Transform>(true))
            {
                int count = node.name.Contains("GachaOneTimesBtn") ? 1
                    : node.name.Contains("GachaTenTimesBtn") ? 10
                    : node.name.Contains("GachaHundredTimesBtn") ? 100 : 0;
                if (count == 0) continue;
                if (node.name == "GachaOneTimesBtn" || node.name == "GachaTenTimesBtn"
                    || node.name == "GachaHundredTimesBtn")
                    SetActive(node, chest.OpenCounts.Contains(count));
                Button button = node.GetComponent<Button>();
                if (button != null) button.interactable = CanAffordDraw(chest, count)
                    || CanOpenDrawQuickBuy(chest, count);
                string costText = string.Join(" / ", (chest.CostItems ?? Array.Empty<WishAmount>())
                    .Select(value => (value.Count * count).ToString(CultureInfo.InvariantCulture)));
                SetTextDeep(node, "ItemContent/Text", costText);
                WishAmount amount = (chest.CostItems ?? Array.Empty<WishAmount>()).FirstOrDefault();
                if (amount != null) SetIconDeep(node, amount.ItemId);
            }
            int miss = service.QualityMissCount(poolType, 4);
            SetTextDeep(poolPanel.transform, "TextTip", poolType == 1
                ? string.Empty : "傳說品質未命中 " + miss + " 次");
        }

        private int DailyLimit(int classType) => rules.settings.DailyGachaMaxCount.FirstOrDefault(value =>
            value.GachaClassType == classType)?.Count ?? 0;

        private void RefreshCurrentPage()
        {
            if (mainPage == 0) { RenderPool(); RefreshChrome(); }
            else if (mainPage == 1) RefreshShop();
            else ShowAchievements();
        }

        private void ShowNotice(string message)
        {
            if (canvasRoot == null) return;
            Transform old = Find(canvasRoot, "WishNotice");
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("WishNotice", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -660);
            rect.sizeDelta = new Vector2(780, 90);
            AddText(rect, "NoticeText", message ?? string.Empty, Vector2.zero, new Vector2(740, 72), 23);
            Destroy(go, 2.2f);
        }

        private void AddAwardLine(Transform parent, int itemId, string label)
        {
            var line = new GameObject("Award_" + itemId, typeof(RectTransform));
            RectTransform rect = (RectTransform)line.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(700, 64);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(34, 0);
            iconRect.sizeDelta = new Vector2(48, 48);
            icon.GetComponent<Image>().sprite = assets.FindItemSprite(itemId);
            icon.GetComponent<Image>().preserveAspect = true;
            AddText(rect, "Name", label, new Vector2(80, 0), new Vector2(590, 56), 23,
                TextAlignmentOptions.Left);
        }

        private void ShowNoticePage(Transform target, string text)
        {
            AddText(target, "WishNoticeText", text, Vector2.zero, new Vector2(720, 220), 28);
        }

        private string ItemName(int itemId)
        {
            WishItem item = rules.Item(itemId);
            return item == null ? "物品 " + itemId : item.Name;
        }

        private string ProfessionName(int id)
        {
            WishProfession profession = rules.Profession(id);
            return profession == null ? "当前职业" : "职业 " + profession.Profession + " · 阶级 " + profession.Rank;
        }

        private static int GroupFor(int type, bool score)
        {
            if (type == 0) return score ? 6 : 5;
            if (type == 2) return score ? 8 : 7;
            return score ? 19 : 18;
        }

        private string Receipt(string action) => "wish:" + action + ":" + slotId + ":" + Guid.NewGuid().ToString("N");
        private OfficialCommercePrefabRenderer NewRenderer() =>
            new OfficialCommercePrefabRenderer(commerceAssets, assets.FindSprite);

        private void CreateModalButton(Transform parent, string label, Action action) =>
            CreateModalButton(parent, label, new Vector2(0, -440), action);

        private TMP_Text AddText(Transform parent, string name, string value, Vector2 position,
            Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = commerceAssets.ChineseTmpFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private Transform CreateVertical(Transform parent, string name, Vector2 position, Vector2 size, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(RectMask2D));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private void BindAction(Transform node, Action action)
        {
            if (node == null) return;
            Button button = node.GetComponent<Button>();
            if (button == null) button = node.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null) button.targetGraphic = node.GetComponentInChildren<Graphic>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private static void AddListenerOnce(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private Button FindButton(Transform root, string name) => FindDeep(root, name)?.GetComponent<Button>();

        private static Transform Find(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            string relative = path.Trim('/');
            Transform found = root.Find(relative);
            if (found != null) return found;
            string[] parts = relative.Split('/');
            Transform current = root;
            foreach (string part in parts)
            {
                Transform next = null;
                foreach (Transform child in current)
                    if (child.name == part) { next = child; break; }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform result = FindDeep(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static CommerceUiNode FindNode(CommerceUiNode root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (CommerceUiNode child in root.children ?? Array.Empty<CommerceUiNode>())
            {
                CommerceUiNode result = FindNode(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static void SetTextDeep(Transform root, string name, string value)
        {
            if (root == null) return;
            Transform node = name.IndexOf('/') >= 0 ? Find(root, name) : FindDeep(root, name);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private void SetIconDeep(Transform root, int itemId)
        {
            if (root == null) return;
            Sprite sprite = assets.FindItemSprite(itemId);
            if (sprite == null) return;
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name != "ItemImg" && node.name != "ItemIcon" && node.name != "Icon") continue;
                Image image = node.GetComponent<Image>();
                if (image == null) continue;
                image.sprite = sprite;
                image.enabled = true;
                image.preserveAspect = true;
            }
        }

        private static void SetSpriteDeep(Transform root, string name, Sprite sprite)
        {
            Transform node = FindDeep(root, name);
            Image image = node == null ? null : node.GetComponent<Image>();
            if (image != null) image.sprite = sprite;
        }

        private static void SetActiveDeep(Transform root, string name, bool active)
        {
            Transform node = FindDeep(root, name);
            if (node != null) node.gameObject.SetActive(active);
        }

        private static void SetActive(Transform node, bool active)
        {
            if (node != null) node.gameObject.SetActive(active);
        }

        private static CanvasGroup EnsureCanvasGroup(Transform node)
        {
            if (node == null) return null;
            CanvasGroup group = node.GetComponent<CanvasGroup>();
            return group == null ? node.gameObject.AddComponent<CanvasGroup>() : group;
        }

        private static void SetResultChromeAlpha(Transform root, float alpha)
        {
            string[] paths =
            {
                "BgPanel1/Content/MiddleGo/GachaBtnContent",
                "BgPanel1/Content/MiddleGo/CloseBtn",
                "BgPanel1/Content/MiddleGo/TodayCountGo",
            };
            foreach (string path in paths)
            {
                CanvasGroup group = EnsureCanvasGroup(Find(root, path));
                if (group != null) group.alpha = alpha;
            }
        }

        private static IEnumerator FadeResultChrome(GameObject root, float duration)
        {
            string[] paths =
            {
                "BgPanel1/Content/MiddleGo/GachaBtnContent",
                "BgPanel1/Content/MiddleGo/CloseBtn",
                "BgPanel1/Content/MiddleGo/TodayCountGo",
            };
            var groups = new List<CanvasGroup>(paths.Length);
            foreach (string path in paths)
            {
                CanvasGroup group = EnsureCanvasGroup(Find(root.transform, path));
                if (group != null)
                {
                    group.alpha = 0;
                    groups.Add(group);
                }
            }
            if (duration <= 0)
            {
                foreach (CanvasGroup group in groups) group.alpha = 1;
                yield break;
            }
            float startedAt = Time.unscaledTime;
            while (true)
            {
                float alpha = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                foreach (CanvasGroup group in groups)
                    if (group != null) group.alpha = alpha;
                if (alpha >= 1) break;
                yield return null;
            }
        }

        private static IEnumerator FadeCanvasGroup(Transform node, float duration)
        {
            CanvasGroup group = EnsureCanvasGroup(node);
            if (group == null) yield break;
            group.alpha = 0;
            if (duration <= 0)
            {
                group.alpha = 1;
                yield break;
            }
            float startedAt = Time.unscaledTime;
            while (true)
            {
                group.alpha = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                if (group.alpha >= 1) break;
                yield return null;
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }

        private static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        private sealed class WishProbabilityEntry
        {
            public readonly WishItem Item;
            public readonly int Count;
            public readonly double Probability;

            public WishProbabilityEntry(WishItem item, int count, double probability)
            {
                Item = item;
                Count = count;
                Probability = probability;
            }
        }

        private sealed class WishAnimationTrack
        {
            public readonly Transform Target;
            public readonly string Property;
            public readonly IReadOnlyList<object> Keys;

            public WishAnimationTrack(Transform target, string property, IReadOnlyList<object> keys)
            {
                Target = target;
                Property = property;
                Keys = keys;
            }
        }
    }
}
