using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private GameObject travelNotesRoot;
        private GameObject dailyDungeonRoot;
        private GameObject dailyDungeonDialogRoot;
        private GameObject dailyDungeonLeaderboardRoot;
        private GameObject dailyDungeonLeaveConfirmRoot;
        private GameObject dailyDungeonClaimConfirmRoot;
        private DailyDungeonResultPanelAdapter dailyDungeonResultPanelAdapter;
        private GameObject dailyDungeonBuyCountRoot;
        private Slider dailyDungeonBuyCountSlider;
        private int dailyDungeonBuyCountLimit;
        private GameObject arenaConfirmRoot;
        private GameObject arenaRoot;
        private GameObject dailyChallengeRoot;
        private GameObject fantasyTowerRoot;
        private GameObject fantasyTowerRowsRoot;
        private ScrollRect fantasyTowerScroll;
        private readonly List<GameObject> fantasyTowerStageRows = new List<GameObject>();
        private int fantasyTowerEntryIndex;
        private int fantasyTowerFirstVisibleLayer;
        private float fantasyTowerStageRowHeight;
        private DailyDungeonBattleRunner activeDungeonBattle;
        private ArenaBattleRunner activeArenaBattle;
        private readonly Dictionary<string, Sprite> dungeonUiSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private DailyDungeonUiBindingModule dungeonUiBindingModule;
        private int selectedDungeonId;
        private int selectedDifficulty;
        private int selectedLeaderboardGrade;
        private bool dailyDungeonShowingGifts;
        private bool dailyDungeonAchievementButtonsBound;
        private readonly List<DailyDungeonAchievementMilestone> dailyDungeonVisibleAchievements =
            new List<DailyDungeonAchievementMilestone>();

        private bool DungeonModalOpen => travelNotesRoot != null || dailyDungeonRoot != null
            || dailyDungeonLeaderboardRoot != null || arenaRoot != null
            || dailyChallengeRoot != null || dailyChallengeSelectionRoot != null
            || dailyChallengeAssistRoot != null
            || dailyChallengeResultRoot != null
            || dailyDungeonClaimConfirmRoot != null || dailyDungeonLeaveConfirmRoot != null
            || dailyDungeonBuyCountRoot != null
            || fantasyTowerRoot != null || NpcTravelModalOpen || MaterialGatheringModalOpen;
        private bool IsDailyDungeonBattleActive => activeDungeonBattle != null || activeArenaBattle != null;

        private void OpenTravelNotes()
        {
            if (travelNotesRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Travel/TravelNotesPanel");
            if (prefab == null) throw new InvalidOperationException("Official TravelNotesPanel is missing.");
            travelNotesRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            travelNotesRoot.name = "TravelNotesPanel";
            travelNotesRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(travelNotesRoot);
            ApplyOfficialDungeonBindings(travelNotesRoot, "TravelNotesPanel");
            Transform mapPlayEntry = Find(travelNotesRoot.transform, "MapGamePlayEntry");
            SetOfficialPrefabSprite(travelNotesRoot,
                "MapGamePlayEntry/NoteUnlockContent/Icon", LoadDungeonUiSprite(
                    "defaultpackage_assets_image_ui_notes_travelnotes.bundle", "note_MapGamePlayEntry"));
            SetOfficialPrefabSprite(travelNotesRoot,
                "DailyDungeonEntry/NoteUnlockContent/Icon", LoadDungeonUiSprite(
                    "defaultpackage_assets_image_ui_notes_travelnotes.bundle", "note_DailyDungeonEntry"));
            Button dungeonButton = OriginalTravelEntryButton("DailyDungeonEntry");
            dungeonButton.onClick.AddListener(OpenDailyDungeons);
            Transform dailyActivityEntry = Find(travelNotesRoot.transform, "DailyActivityEntry");
            if (dailyActivityEntry != null) BindPrefabButton(dailyActivityEntry.gameObject, "EntryBtn", OpenDailyChallenges);
            BindPrefabButton(travelNotesRoot, "NormalMaterialEntry", OpenMaterialGatheringEntries);
            AddNpcAdventureTravelEntry();
            BindPrefabButton(travelNotesRoot, "CloseBtn2", CloseDungeonPages);
            Button arenaButton = OriginalTravelEntryButton("ArenaAreaEntry");
            if (arenaButton != null)
            {
                bool preview = IsArenaPreviewAvailable();
                SetPrefabActive(travelNotesRoot, "ArenaAreaEntry", preview);
                arenaButton.onClick.AddListener(() =>
                {
                    if (IsArenaUnlocked()) OpenArena();
                    else Notify("竞技场尚未开放");
                });
            }
            bool towerUnlocked = !host.Rules.IsFunctionLimited(2);
            var towerLevels = Enumerable.Range(1, 4).Where(entry => !host.Rules.IsFunctionLimited(2, entry))
                .Select(entry => new KeyValuePair<string, int>(TowerEntryName(entry),
                    host.FantasyTower.GetEntry(entry).clearedLayer)).ToArray();
            OfficialDungeonEntryBindings.RefreshTowerTravelEntry(travelNotesRoot, towerUnlocked,
                !host.Rules.IsFunctionLimited(2, -1), towerLevels);
            OfficialDungeonEntryBindings.TowerEntryButton(travelNotesRoot).onClick.AddListener(() =>
            {
                if (towerUnlocked) OpenFantasyTowerEntries();
            });
        }

        private Button OriginalTravelEntryButton(string type)
        {
            DungeonOfficialEntryButtonBinding binding = LoadDungeonOfficialBindings("TravelNotesPanel")
                .entryButtons?.FirstOrDefault(value => value.type == type);
            Transform target = binding == null ? null
                : FindOfficialBindingNode(travelNotesRoot.transform, binding.button, null);
            Button button = target == null ? null : target.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Original travel entry Button is missing: " + type);
            return button;
        }

        private void OpenFantasyTowerEntries()
        {
            CloseDungeonPages();
            fantasyTowerRoot = Renderer().Create("fantasy_tower_entry", canvas.transform);
            fantasyTowerRoot.name = "StageDemonBattlefieldEntryPanel";
            fantasyTowerRoot.transform.SetAsLastSibling();
            BindPrefabButton(fantasyTowerRoot, "CloseBtn_dark", CloseFantasyTower);
            Transform scrollNode = Find(fantasyTowerRoot.transform, "ScrollRect");
            Transform viewport = Find(fantasyTowerRoot.transform, "Viewport");
            fantasyTowerRowsRoot = Find(fantasyTowerRoot.transform, "Content")?.gameObject;
            fantasyTowerScroll = scrollNode == null ? null : scrollNode.GetComponent<ScrollRect>();
            if (fantasyTowerScroll == null || viewport == null || fantasyTowerRowsRoot == null)
                throw new InvalidOperationException("Official Fantasy Tower entry ScrollRect is incomplete.");
            fantasyTowerScroll.viewport = viewport as RectTransform;
            fantasyTowerScroll.content = fantasyTowerRowsRoot.transform as RectTransform;
            HomeUiPanel panel = catalog.Panel("fantasy_tower_entry_item_1");
            if (panel == null) throw new InvalidOperationException("Official Fantasy Tower entry row is missing.");
            float entryRowHeight = panel.root.size_delta != null && panel.root.size_delta.Length > 1
                ? panel.root.size_delta[1] : 0f;
            if (entryRowHeight <= 0f) throw new InvalidOperationException("Official Fantasy Tower entry row has no height.");
            int[] visibleEntries = Enumerable.Range(1, 4).Where(entry => !host.Rules.IsFunctionLimited(2, entry)
                || !host.Rules.IsFunctionLimited(123, entry)).ToArray();
            ConfigureTowerContent(fantasyTowerRowsRoot.transform as RectTransform, visibleEntries.Length * entryRowHeight);
            for (int index = 0; index < visibleEntries.Length; index++)
            {
                int entry = visibleEntries[index];
                int selectedEntry = entry;
                GameObject row = Renderer().CreateNode(panel.root, fantasyTowerRowsRoot.transform);
                row.name = "FantasyTowerEntry_" + entry;
                RectTransform rect = row.transform as RectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -entryRowHeight * index);
                SetNamedText(row.transform, "ChallengeText", "当前 "
                    + host.FantasyTower.GetEntry(entry).clearedLayer + "/" + TowerLayerCount(entry) + " 层");
                SetNamedText(row.transform, "TimeTxt", "已前进 "
                    + host.FantasyTower.GetEntry(entry).clearedLayer + " 层");
                SetNamedText(row.transform, "TItleText", TowerEntryName(entry));
                FantasyTowerPushResult pending = host.FantasyTower.EarliestPendingResult();
                SetPrefabActive(row, "RedDot", pending != null && pending.entryIndex == entry);
                Image background = Find(row.transform, "ImgBG")?.GetComponent<Image>();
                Image icon = Find(row.transform, "ImgIcon")?.GetComponent<Image>();
                if (background != null)
                    background.sprite = catalog.FindDynamicSprite("fantasy_tower_entry_" + entry + "_background");
                if (icon != null)
                    icon.sprite = catalog.FindDynamicSprite("fantasy_tower_entry_" + entry + "_icon");
                Slider progress = Find(row.transform, "Slider")?.GetComponent<Slider>();
                if (progress != null)
                {
                    progress.minValue = 0;
                    progress.maxValue = TowerLayerCount(entry);
                    progress.SetValueWithoutNotify(host.FantasyTower.GetEntry(entry).clearedLayer);
                }
                Button button = row.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() =>
                {
                    FantasyTowerPushResult result = host.FantasyTower.EarliestPendingResult();
                    if (result != null && result.entryIndex == selectedEntry)
                    {
                        CloseFantasyTower();
                        ShowFantasyTowerPendingResult();
                    }
                    else OpenFantasyTowerStages(selectedEntry);
                });
            }
        }

        private void OpenFantasyTowerStages(int entry)
        {
            if (entry < 1 || entry > 4 || host.Rules.IsFunctionLimited(2, entry)) return;
            DestroyFantasyTowerPage();
            fantasyTowerEntryIndex = entry;
            fantasyTowerRoot = Renderer().Create("fantasy_tower_stage_list", canvas.transform);
            fantasyTowerRoot.name = "StageDemonBattlefieldPanel";
            fantasyTowerRoot.transform.SetAsLastSibling();
            BindPrefabButton(fantasyTowerRoot, "CloseBtn_dark", CloseFantasyTower);
            SetPrefabActive(fantasyTowerRoot, "TopTabItem", false);
            SetPrefabActive(fantasyTowerRoot, "AchievementTabItem", false);
            SetPrefabActive(fantasyTowerRoot, "DemonBattlefieldPersonalTopPanel", false);
            SetPrefabActive(fantasyTowerRoot, "AchievementPanelWithTypes", false);
            Transform middle = Find(fantasyTowerRoot.transform, "MiddleGo");
            string[] sections = { "MoonStageContent", "PrisonStageContent", "SeaStageContent", "FurnaceStageContent" };
            for (int index = 0; index < sections.Length; index++)
            {
                Transform section = Find(middle, sections[index]);
                if (section != null) section.gameObject.SetActive(index == entry - 1);
            }
            Transform selectedSection = Find(middle, sections[entry - 1]);
            Transform scrollNode = Find(selectedSection, "ScrollRect");
            Transform viewport = Find(scrollNode, "ViewPort") ?? Find(scrollNode, "Viewport");
            fantasyTowerRowsRoot = Find(scrollNode, "Content")?.gameObject;
            fantasyTowerScroll = scrollNode == null ? null : scrollNode.GetComponent<ScrollRect>();
            if (fantasyTowerScroll == null || viewport == null || fantasyTowerRowsRoot == null)
                throw new InvalidOperationException("Official Fantasy Tower layer ScrollRect is incomplete.");
            fantasyTowerScroll.viewport = viewport as RectTransform;
            fantasyTowerScroll.content = fantasyTowerRowsRoot.transform as RectTransform;
            VerticalLayoutGroup layout = fantasyTowerRowsRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            ContentSizeFitter fitter = fantasyTowerRowsRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            RectTransform contentRect = fantasyTowerRowsRoot.transform as RectTransform;
            HomeUiPanel rowPanel = catalog.Panel("fantasy_tower_stage_item_" + entry);
            if (rowPanel == null) throw new InvalidOperationException("Official Fantasy Tower layer row is missing.");
            fantasyTowerStageRowHeight = rowPanel.root.size_delta != null && rowPanel.root.size_delta.Length > 1
                ? rowPanel.root.size_delta[1] : 0f;
            if (fantasyTowerStageRowHeight <= 0f)
                throw new InvalidOperationException("Official Fantasy Tower layer row has no height.");
            ConfigureTowerContent(contentRect, TowerLayerCount(entry) * fantasyTowerStageRowHeight);
            fantasyTowerStageRows.Clear();
            for (int index = 0; index < 10; index++)
            {
                GameObject row = Renderer().CreateNode(rowPanel.root, fantasyTowerRowsRoot.transform);
                row.name = "FantasyTowerLayerRow_" + index;
                fantasyTowerStageRows.Add(row);
            }
            fantasyTowerScroll.onValueChanged.RemoveListener(OnFantasyTowerScrollChanged);
            fantasyTowerScroll.onValueChanged.AddListener(OnFantasyTowerScrollChanged);
            int currentLayer = host.FantasyTowerCatalog.CurrentLayer(host.FantasyTower, entry);
            fantasyTowerFirstVisibleLayer = Mathf.Clamp(currentLayer - 2, 1,
                Mathf.Max(1, TowerLayerCount(entry) - fantasyTowerStageRows.Count + 1));
            contentRect.anchoredPosition = new Vector2(0,
                (fantasyTowerFirstVisibleLayer - 1) * fantasyTowerStageRowHeight);
            RefreshFantasyTowerStageRows();
            SetNamedText(fantasyTowerRoot.transform, "TitleText", TowerEntryName(entry));
        }

        private void OnFantasyTowerScrollChanged(Vector2 _)
        {
            if (fantasyTowerRowsRoot == null) return;
            RectTransform content = fantasyTowerRowsRoot.transform as RectTransform;
            int first = Mathf.FloorToInt(content.anchoredPosition.y / fantasyTowerStageRowHeight) + 1;
            fantasyTowerFirstVisibleLayer = Mathf.Clamp(first, 1,
                Mathf.Max(1, TowerLayerCount(fantasyTowerEntryIndex) - fantasyTowerStageRows.Count + 1));
            RefreshFantasyTowerStageRows();
        }

        private void RefreshFantasyTowerStageRows()
        {
            int currentLayer = host.FantasyTowerCatalog.CurrentLayer(host.FantasyTower, fantasyTowerEntryIndex);
            int maxLayer = TowerLayerCount(fantasyTowerEntryIndex);
            for (int index = 0; index < fantasyTowerStageRows.Count; index++)
            {
                int layer = fantasyTowerFirstVisibleLayer + index;
                GameObject row = fantasyTowerStageRows[index];
                row.SetActive(layer <= maxLayer);
                if (layer > maxLayer) continue;
                RectTransform rect = row.transform as RectTransform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, fantasyTowerStageRowHeight);
                rect.anchoredPosition = new Vector2(0, -(layer - 1) * fantasyTowerStageRowHeight);
                SetNamedText(row.transform, "TxtLayNumber", "F" + layer);
                SetPrefabActive(row, "TxtTip", layer < currentLayer);
                SetPrefabActive(row, "ImgBGPass", layer < currentLayer);
                SetPrefabActive(row, "ImgSelected", layer == currentLayer);
                SetPrefabActive(row, "CurrentLayerNumberGo", layer == currentLayer);
                SetPrefabActive(row, "NextLayerNumberGo", layer == currentLayer + 1);
                SetPrefabActive(row, "StartBtn", layer == currentLayer);
                SetPrefabActive(row, "Right/AutoPush", layer == currentLayer);
                SetPrefabActive(row, "Right/SuppressCurrent", false);
                SetPrefabActive(row, "Right/SuppressNext", false);
                Button button = row.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.interactable = layer == currentLayer;
                    int selectedLayer = layer;
                    if (selectedLayer == currentLayer)
                        button.onClick.AddListener(() => StartFantasyTowerLayer(selectedLayer));
                }
                Button start = FindPath(row.transform, "StartBtn/CommonBtn_12")?.GetComponent<Button>();
                if (start != null)
                {
                    start.onClick.RemoveAllListeners();
                    start.interactable = layer == currentLayer;
                    int selectedLayer = layer;
                    if (selectedLayer == currentLayer)
                        start.onClick.AddListener(() => StartFantasyTowerLayer(selectedLayer));
                }
                Button autoPush = FindPath(row.transform, "Right/AutoPush/CommonBtn_12 (1)")?.GetComponent<Button>();
                if (autoPush != null)
                {
                    autoPush.onClick.RemoveAllListeners();
                    int selectedEntry = fantasyTowerEntryIndex;
                    int selectedLayer = layer;
                    autoPush.onClick.AddListener(() => StartFantasyTowerAutoPush(selectedEntry, selectedLayer));
                }
            }
        }

        private void StartFantasyTowerAutoPush(int entry, int selectedLayer)
        {
            FantasyTowerEntrySave progress = host.FantasyTower.GetEntry(entry);
            if (progress.clearedLayer < 3)
            {
                Notify(catalog.LocalizedText("ui_DemonBattlefield_AutoPushLockedTip",
                    "通过幻想阶梯第3层解锁自动推关"));
                return;
            }
            FantasyTowerResult enabled = host.FantasyTower.SetAutoPush(entry, true);
            if (enabled != FantasyTowerResult.Applied && enabled != FantasyTowerResult.Unchanged)
            {
                Notify("当前入口无法开启自动推关");
                return;
            }
            StartFantasyTowerLayer(selectedLayer);
        }

        private void StartFantasyTowerLayer(int selectedLayer)
        {
            if (selectedLayer != host.FantasyTowerCatalog.CurrentLayer(host.FantasyTower, fantasyTowerEntryIndex))
            {
                Notify("只能挑战当前层");
                return;
            }
            int entry = fantasyTowerEntryIndex;
            CloseFantasyTower();
            try { host.StartFantasyTower(entry); }
            catch (Exception error) { Notify(error.Message); }
        }

        private void CloseFantasyTower()
        {
            fantasyTowerScroll?.onValueChanged.RemoveListener(OnFantasyTowerScrollChanged);
            DestroyFantasyTowerPage();
        }

        private void DestroyFantasyTowerPage()
        {
            fantasyTowerScroll?.onValueChanged.RemoveListener(OnFantasyTowerScrollChanged);
            if (fantasyTowerRoot != null) UnityEngine.Object.Destroy(fantasyTowerRoot);
            fantasyTowerRoot = null;
            fantasyTowerRowsRoot = null;
            fantasyTowerScroll = null;
            fantasyTowerStageRows.Clear();
        }

        private static void ConfigureTowerContent(RectTransform content, float height)
        {
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        private int TowerLayerCount(int entry) => host.FantasyTowerCatalog.LayerCount(entry);

        private static string TowerEntryName(int entry) => entry switch
        {
            1 => "月影之森", 2 => "魔能邃渊", 3 => "云流漩涡", 4 => "锁妖塔",
            _ => string.Empty
        };

        private void OpenDailyDungeons()
        {
            if (travelNotesRoot != null) UnityEngine.Object.Destroy(travelNotesRoot);
            travelNotesRoot = null;
            if (dailyDungeonRoot != null) UnityEngine.Object.Destroy(dailyDungeonRoot);
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonEntryPanel");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon entry panel is missing.");
            dailyDungeonRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonRoot.name = "DailyDungeonEntryPanel";
            dailyDungeonShowingGifts = false;
            dailyDungeonAchievementButtonsBound = false;
            dailyDungeonRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonRoot);
            ApplyOfficialDungeonBindings(dailyDungeonRoot, "DailyDungeonEntryPanel");
            BindPrefabButton(dailyDungeonRoot, "CloseBtn2", CloseDungeonPages);
            BindPrefabButton(dailyDungeonRoot, "GoToBtn", OpenDailyDungeonAction);
            BindPrefabButton(dailyDungeonRoot, "DailyDungeonEntryPanelTabGroupItem_11",
                () => ShowDailyDungeonGiftPage(false));
            BindPrefabButton(dailyDungeonRoot, "AchievementTabGroupItem_11",
                () => ShowDailyDungeonGiftPage(true));
            RefreshDailyDungeonEntryPage();
        }

        private void ShowDailyDungeonGiftPage(bool showGifts)
        {
            dailyDungeonShowingGifts = showGifts;
            RefreshDailyDungeonEntryPage();
        }

        private void RefreshDailyDungeonEntryPage()
        {
            if (dailyDungeonRoot == null) return;
            SetPrefabText(dailyDungeonRoot, "FreeCount", host.DailyDungeons.RemainEntries.ToString());
            Transform selectContent = Find(dailyDungeonRoot.transform, "SelectContent");
            if (selectContent != null) selectContent.gameObject.SetActive(!dailyDungeonShowingGifts);
            Transform achievementPanel = Find(dailyDungeonRoot.transform, "AchievementPanel");
            if (achievementPanel != null) achievementPanel.gameObject.SetActive(dailyDungeonShowingGifts);
            if (dailyDungeonShowingGifts)
            {
                RefreshDailyDungeonAchievementPage(achievementPanel);
                Canvas.ForceUpdateCanvases();
                return;
            }
            if (host.DailyDungeons.ActiveRun != null)
            {
                SetNamedText(dailyDungeonRoot.transform, "DescTxt", "存在未完成路线 · 今日剩余领奖次数 "
                    + host.DailyDungeons.RemainEntries);
                SetNamedText(dailyDungeonRoot.transform, "GoToBtn", "继续副本");
                SetPrefabActive(dailyDungeonRoot, "GoToBtn", true);
            }
            else if (host.DailyDungeons.PendingClaims.Count > 0)
            {
                SetNamedText(dailyDungeonRoot.transform, "GoToBtn",
                    "领取副本奖励（" + host.DailyDungeons.PendingClaims.Count + "）");
                SetPrefabActive(dailyDungeonRoot, "GoToBtn", true);
            }
            else SetPrefabActive(dailyDungeonRoot, "GoToBtn", false);
            int publicCount = 0;
            DailyDungeonEntryPageModel page = null;
            try
            {
                page = host.DailyDungeons.BuildEntryPageModel(IsMapRegionPublic,
                    (dungeonId, conditionId) => IsDailyDungeonConditionMet(dungeonId, conditionId));
                publicCount = page.Entries.Count;
            }
            catch (InvalidOperationException)
            {
                publicCount = 0;
            }
            SetPrefabText(dailyDungeonRoot, "DescTxt", host.DailyDungeons.ActiveRun != null
                ? "存在未完成路线 · 今日剩余领奖次数 " + host.DailyDungeons.RemainEntries
                : host.DailyDungeons.PendingClaims.Count > 0
                    ? "有 " + host.DailyDungeons.PendingClaims.Count + " 份副本奖励待领取 · 今日剩余领奖次数 "
                        + host.DailyDungeons.RemainEntries
                : publicCount == 0 ? "尚无已公开的日常副本入口区域"
                    : "已公开副本 " + publicCount + " 个 · 今日剩余领奖次数 " + host.DailyDungeons.RemainEntries);
            Transform list = FindPath(dailyDungeonRoot.transform,
                "SelectContent/ScrollRect/Viewport/Content");
            if (list == null) return;
            foreach (Transform child in list) UnityEngine.Object.Destroy(child.gameObject);
            GameObject itemPrefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonEntryPanelItem");
            if (itemPrefab == null) throw new InvalidOperationException("Official daily dungeon list item is missing.");
            foreach (DailyDungeonEntryModel dungeon in page?.Entries
                ?? Array.Empty<DailyDungeonEntryModel>())
            {
                GameObject item = UnityEngine.Object.Instantiate(itemPrefab, list, false);
                item.name = "Dungeon_" + dungeon.DungeonId;
                ApplyOfficialDungeonBindings(item, "DailyDungeonEntryPanelItem");
                BindDungeonListArtwork(item, dungeon.DungeonId);
                SetNamedText(item.transform, "TitleGo", dungeon.Title);
                SetNamedText(item.transform, "DescTxt", "最高评级：" + BestGradeText(dungeon.BestGrade));
                Button select = item.GetComponent<Button>();
                if (select == null)
                {
                    select = item.AddComponent<Button>();
                    select.targetGraphic = item.GetComponent<Graphic>();
                }
                int id = dungeon.DungeonId;
                select.onClick.AddListener(() => OpenDailyDungeonDetail(id));
            }
            RectTransform listRect = list as RectTransform;
            if (listRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
            Canvas.ForceUpdateCanvases();
        }

        private void RefreshDailyDungeonAchievementPage(Transform panel)
        {
            if (panel == null) return;
            Transform grid = Find(panel, "Grid");
            if (grid == null) return;
            List<Transform> rows = grid.Cast<Transform>()
                .Where(value => value.name == "AchievementPanelItem"
                    || value.name.StartsWith("AchievementPanelItem (", StringComparison.Ordinal))
                .OrderBy(value => value.GetSiblingIndex()).ToList();
            if (rows.Count == 0) return;

            DailyDungeonGiftModel gift = host.DailyDungeons.BuildGiftModel(rows.Count);
            IReadOnlyList<DailyDungeonAchievementMilestone> visible = gift.Achievements;
            dailyDungeonVisibleAchievements.Clear();
            dailyDungeonVisibleAchievements.AddRange(visible);
            if (!dailyDungeonAchievementButtonsBound)
            {
                for (int index = 0; index < rows.Count; index++)
                {
                    Transform receive = Find(rows[index], "ReceiveBtn");
                    if (receive == null) continue;
                    Button button = receive.GetComponent<Button>();
                    if (button == null)
                    {
                        button = receive.gameObject.AddComponent<Button>();
                        button.targetGraphic = receive.GetComponent<Graphic>();
                    }
                    int rowIndex = index;
                    button.onClick.AddListener(() => ClaimDailyDungeonAchievement(rowIndex));
                }
                dailyDungeonAchievementButtonsBound = true;
            }

            long progress = gift.ClaimProgress;
            for (int index = 0; index < rows.Count; index++)
            {
                Transform row = rows[index];
                bool hasMilestone = index < visible.Count;
                row.gameObject.SetActive(hasMilestone);
                if (!hasMilestone) continue;
                DailyDungeonAchievementMilestone milestone = visible[index];
                SetNamedText(row, "TitleTxt", "累计领取 " + milestone.RequiredCount + " 次");
                SetNamedText(row, "ProgressTxt", Math.Min(progress, milestone.RequiredCount)
                    + "/" + milestone.RequiredCount);
                Slider slider = row.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    slider.minValue = 0;
                    slider.maxValue = milestone.RequiredCount;
                    slider.value = Math.Min(progress, milestone.RequiredCount);
                }
                SetPrefabActive(row.gameObject, "UncompletedGo",
                    milestone.State == DailyDungeonAchievementState.Processing);
                SetPrefabActive(row.gameObject, "FinishedChestImg",
                    milestone.State == DailyDungeonAchievementState.Received);
                SetPrefabActive(row.gameObject, "UnfinishedChestImg",
                    milestone.State != DailyDungeonAchievementState.Received);
                Button receive = Find(row, "ReceiveBtn")?.GetComponent<Button>();
                if (receive != null) receive.interactable = milestone.State == DailyDungeonAchievementState.Completed;
                RefreshDailyDungeonAchievementRewards(row, milestone.Rewards);
            }
        }

        private void RefreshDailyDungeonAchievementRewards(Transform row,
            IReadOnlyList<DailyDungeonAchievementReward> rewards)
        {
            Transform grid = Find(row, "AwardGrid");
            if (grid == null) return;
            foreach (Transform child in grid.Cast<Transform>().Where(value => value.name.StartsWith(
                    "OfflineReward_", StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.Destroy(child.gameObject);
            GameObject itemIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemIcon");
            if (itemIconPrefab == null)
                throw new InvalidOperationException("Official daily dungeon item icon prefab is missing.");
            foreach (DailyDungeonAchievementReward reward in rewards ?? Array.Empty<DailyDungeonAchievementReward>())
            {
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0) continue;
                GameObject iconObject = UnityEngine.Object.Instantiate(itemIconPrefab, grid, false);
                DungeonUiBindings.Apply(iconObject, "ItemIcon");
                iconObject.name = "OfflineReward_" + reward.ItemId;
                DailyDungeonResultItemIconAdapter adapter = iconObject
                    .GetComponent<DailyDungeonResultItemIconAdapter>();
                if (adapter == null) adapter = iconObject.AddComponent<DailyDungeonResultItemIconAdapter>();
                adapter.Configure(host, reward.ItemId, reward.Count);
            }
        }

        private void ClaimDailyDungeonAchievement(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dailyDungeonVisibleAchievements.Count
                || dailyDungeonVisibleAchievements[rowIndex].State != DailyDungeonAchievementState.Completed)
                return;
            if (!host.ClaimDailyDungeonAchievement(dailyDungeonVisibleAchievements[rowIndex].Index))
            {
                Notify("馈赠奖励领取失败");
                return;
            }
            RefreshDailyDungeonEntryPage();
        }

        private bool IsMapRegionPublic(int mapClassId, int region)
        {
            return host.MapRegionProgress(mapClassId, region) >= 10000;
        }

        private bool IsArenaPreviewAvailable() => host.Arena.IsFunctionLimitMet(58, -1,
            host.MapRegionProgress);

        private bool IsArenaUnlocked() => host.Arena.IsFunctionLimitMet(58, 0,
            host.MapRegionProgress);

        private void OpenArena()
        {
            if (!IsArenaUnlocked()) return;
            CloseDungeonPages();
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/ArenaPanel");
            if (prefab == null) throw new InvalidOperationException("Official arena panel is missing.");
            arenaRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            arenaRoot.name = "ArenaPanel";
            arenaRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(arenaRoot);
            ApplyOfficialDungeonBindings(arenaRoot, "ArenaPanel");
            SetOfficialPrefabSprite(arenaRoot, "Bg", LoadDungeonUiSprite(
                "defaultpackage_assets_image_ui_arena.bundle", "BgArena"));
            BindPrefabButton(arenaRoot, "CloseBtn_dark", CloseArena);
            BindPrefabButton(arenaRoot, "CloseBtn", CloseArena);
            BindPrefabButton(arenaRoot, "RefreshBtn", RefreshArenaOpponents);
            RefreshArenaPanel();
            if (host.Arena.HasActiveChallenge) ResumeArenaChallenge();
        }

        private void RefreshArenaOpponents()
        {
            if (host.Arena.RefreshesUsed >= host.Arena.Catalog.settings.InitRefreshCount)
            {
                ShowArenaConfirm("刷新竞技对手", "消耗 "
                    + host.Arena.Catalog.runtime.refreshCost + " 晨星刷新对手？", "取消", "确认刷新",
                    ExecuteArenaRefresh);
                return;
            }
            ExecuteArenaRefresh();
        }

        private void ExecuteArenaRefresh()
        {
            if (!host.Arena.TryRefresh(host.ConsumeArenaCurrency, out string error))
            {
                Notify(error ?? "对手刷新失败");
                return;
            }
            RefreshArenaPanel();
        }

        private void RefreshArenaPanel()
        {
            if (arenaRoot == null) return;
            SetNamedText(arenaRoot.transform, "FightRankTxt", host.Arena.Rank.ToString());
            SetNamedText(arenaRoot.transform, "RefreshTxt", "免费刷新 "
                + Math.Max(0, host.Arena.Catalog.settings.InitRefreshCount - host.Arena.RefreshesUsed)
                + " · 已用 " + host.Arena.RefreshesUsed);
            SetNamedText(arenaRoot.transform, "DescTxt", "今日挑战次数：" + host.Arena.Tickets
                + " · 胜 " + host.Arena.Wins + " · 负 " + host.Arena.Losses
                + " · 点击挑战按钮购买额外次数");
            for (int index = 0; index < 4; index++)
            {
                Transform item = Find(arenaRoot.transform, index == 0 ? "OpponentItem"
                    : "OpponentItem (" + index + ")");
                if (item == null) continue;
                if (index >= host.Arena.Opponents.Count)
                {
                    item.gameObject.SetActive(false);
                    continue;
                }
                item.gameObject.SetActive(true);
                var opponent = host.Arena.Opponents[index];
                var guard = host.Arena.FindGuard(opponent.guardTemplateId);
                SetNamedText(item, "RankTxt", opponent.rank.ToString());
                SetNamedText(item, "NameTxt", guard == null ? "守卫" : "守卫 " + guard.id);
                SetNamedText(item, "CombatRatingTxt", "Lv." + (guard == null ? 0 : guard.level));
                Button fight = Find(item, "FightBtn")?.GetComponent<Button>();
                if (fight != null)
                {
                    fight.onClick.RemoveAllListeners();
                    int selected = index;
                    fight.onClick.AddListener(() => BeginArenaChallenge(selected));
                }
            }
        }

        private void BeginArenaChallenge(int index)
        {
            if (host.Arena.Tickets > 0 && index >= 0 && index < host.Arena.Opponents.Count)
            {
                ArenaRuntimeGuard guard = host.Arena.FindGuard(host.Arena.Opponents[index].guardTemplateId);
                long playerRating = host.DailyDungeonCombatRating();
                long guardRating = ArenaBattleRunner.CalculateGuardCombatRating(host, guard);
                int directWinPercent = host.Arena.Catalog.settings.ArenaCombatRatingDirectWinPercent;
                if (guardRating > 0 && playerRating * 100L >= guardRating * directWinPercent)
                {
                    ShowArenaConfirm("竞技挑战方式", "战力达到直接胜利条件（本地战力 "
                        + playerRating + " / 守卫战力 " + guardRating + "）", "正常挑战", "直接胜利",
                        () => CompleteDirectArenaChallenge(index), () => StartArenaChallenge(index));
                    return;
                }
            }
            StartArenaChallenge(index);
        }

        private void StartArenaChallenge(int index)
        {
            if (!host.Arena.TryBeginChallenge(index, out string error))
            {
                if (host.Arena.Tickets <= 0 && host.Arena.NextTicketPrice > 0)
                {
                    ShowArenaConfirm("购买竞技次数", "消耗 " + host.Arena.NextTicketPrice
                        + " 晨星购买 " + host.Arena.Catalog.settings.ChallengeCountBuyOnce + " 次挑战？",
                        "取消", "购买", () =>
                        {
                            if (!host.Arena.TryBuyChallenge(host.ConsumeArenaCurrency, out string buyError))
                            {
                                Notify(buyError ?? "购买竞技次数失败");
                                return;
                            }
                            RefreshArenaPanel();
                            BeginArenaChallenge(index);
                        });
                    return;
                }
                Notify(error ?? "当前竞技挑战不可开始");
                return;
            }
            StartArenaBattle();
        }

        private void CompleteDirectArenaChallenge(int index)
        {
            if (!host.Arena.TryBeginChallenge(index, out string error))
            {
                Notify(error ?? "当前竞技挑战不可开始");
                return;
            }
            if (!host.Arena.CompleteChallenge(true, host.GrantBattleReward))
                throw new InvalidOperationException("Direct arena victory settlement failed.");
            RefreshArenaPanel();
            Notify("竞技挑战直接胜利，已结算奖励");
        }

        private void ResumeArenaChallenge()
        {
            if (!host.Arena.HasActiveChallenge || activeArenaBattle != null) return;
            StartArenaBattle();
        }

        private void StartArenaBattle()
        {
            if (arenaRoot != null) arenaRoot.SetActive(false);
            var battleObject = new GameObject("ArenaBattleRunner");
            battleObject.transform.SetParent(host.transform, false);
            activeArenaBattle = battleObject.AddComponent<ArenaBattleRunner>();
            try
            {
                activeArenaBattle.Begin(host, OnArenaBattleFinished);
            }
            catch
            {
                UnityEngine.Object.Destroy(battleObject);
                activeArenaBattle = null;
                arenaRoot.SetActive(true);
                throw;
            }
        }

        private void OnArenaBattleFinished(bool won)
        {
            activeArenaBattle = null;
            if (arenaRoot != null) arenaRoot.SetActive(true);
            RefreshArenaPanel();
            Notify(won ? "竞技挑战胜利，已结算奖励" : "竞技挑战失败，已结算奖励");
        }

        private void CloseArena()
        {
            if (activeArenaBattle != null) return;
            CloseArenaConfirm();
            if (arenaRoot != null) UnityEngine.Object.Destroy(arenaRoot);
            arenaRoot = null;
        }

        private void ShowArenaConfirm(string title, string message, string cancelText,
            string confirmText, Action confirm, Action cancel = null)
        {
            if (arenaConfirmRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonConfirmEnterPanel");
            if (prefab == null) throw new InvalidOperationException("Official confirmation panel is missing.");
            arenaConfirmRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            arenaConfirmRoot.name = "ArenaConfirm";
            arenaConfirmRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(arenaConfirmRoot);
            ApplyOfficialDungeonBindings(arenaConfirmRoot, "DailyDungeonConfirmEnterPanel");
            SetNamedText(arenaConfirmRoot.transform, "TitleText", title);
            SetNamedText(arenaConfirmRoot.transform, "Txt", message);
            SetNamedText(arenaConfirmRoot.transform, "CommonBtn_11", cancelText);
            SetNamedText(arenaConfirmRoot.transform, "CommonBtn_12", confirmText);
            BindPrefabButton(arenaConfirmRoot, "CommonBtn_11", () =>
            {
                CloseArenaConfirm();
                cancel?.Invoke();
            });
            BindPrefabButton(arenaConfirmRoot, "CommonBtn_12", () =>
            {
                CloseArenaConfirm();
                confirm?.Invoke();
            });
        }

        private void CloseArenaConfirm()
        {
            if (arenaConfirmRoot != null) UnityEngine.Object.Destroy(arenaConfirmRoot);
            arenaConfirmRoot = null;
        }

        private void OpenDailyDungeonDetail(int dungeonId, int? requestedDifficulty = null)
        {
            var dungeon = host.DailyDungeons.FindDungeon(dungeonId);
            if (dungeon == null) return;
            CloseDailyDungeonLeaderboard();
            selectedDungeonId = dungeonId;
            selectedDifficulty = requestedDifficulty ?? host.DailyDungeons.DefaultDifficulty(dungeonId);
            if (selectedDifficulty < 0) selectedDifficulty = 0;
            if (dailyDungeonDialogRoot != null) UnityEngine.Object.Destroy(dailyDungeonDialogRoot);
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon detail panel is missing.");
            dailyDungeonDialogRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonDialogRoot.name = "DailyDungeonDialog";
            dailyDungeonDialogRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonDialogRoot);
            ApplyOfficialDungeonBindings(dailyDungeonDialogRoot, "DailyDungeonDialog");
            BindDungeonArtwork(dailyDungeonDialogRoot, dungeonId, "BgDialog3", "IconImg");
            SetNamedText(dailyDungeonDialogRoot.transform, "TitleTxt", dungeon.title);
            BindDifficultyTabs(dungeonId);
            RefreshDailyDungeonDetail();
            BindPrefabButton(dailyDungeonDialogRoot, "BattleBtn", BeginSelectedDailyDungeon);
            BindPrefabButton(dailyDungeonDialogRoot, "GoToBtn", BeginSelectedDailyDungeon);
            BindPrefabButton(dailyDungeonDialogRoot, "UnlockBtn", UnlockSelectedDailyDungeonDifficulty);
            BindPrefabButton(dailyDungeonDialogRoot, "CloseBtn2", CloseDungeonDetail);
            BindPrefabButton(dailyDungeonDialogRoot, "LeftBtn", () => NavigateDailyDungeon(-1));
            BindPrefabButton(dailyDungeonDialogRoot, "RightBtn", () => NavigateDailyDungeon(1));
            BindPrefabButton(dailyDungeonDialogRoot, "DailyDungeonTopGrade", OpenDailyDungeonLeaderboard);
        }

        private void OpenDailyDungeonLeaderboard()
        {
            if (dailyDungeonLeaderboardRoot != null) return;
            DailyDungeonLevelDefinition level = host.DailyDungeons.ResolveLevel(
                selectedDungeonId, selectedDifficulty);
            if (level?.SupportsLeaderboard != true) return;
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/DailyDungeon/DailyDungeonTopPanel");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon leaderboard panel is missing.");
            dailyDungeonLeaderboardRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonLeaderboardRoot.name = "DailyDungeonTopPanel";
            dailyDungeonLeaderboardRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonLeaderboardRoot);
            ApplyOfficialDungeonBindings(dailyDungeonLeaderboardRoot, "DailyDungeonTopPanel");
            BindPrefabButton(dailyDungeonLeaderboardRoot, "CloseBtn_dark", CloseDailyDungeonLeaderboard);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "FirstGo", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "TeamInfo", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "MemberInfo", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "AchievementPanel", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "SwitchTopRangeBtn", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "TopLimitTime", false);

            int[] grades = level.LeaderboardGrades.ToArray();
            Transform tabGroup = Find(dailyDungeonLeaderboardRoot.transform, "TabGroup");
            Zjcs.Official.WorldUI.WorldTabItem[] gradeTabs = tabGroup == null
                ? Array.Empty<Zjcs.Official.WorldUI.WorldTabItem>()
                : tabGroup.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldTabItem>(true)
                    .Where(value => value.transform.name.StartsWith("DailyDungeonTopPanelTabBottomBtn",
                        StringComparison.Ordinal)).ToArray();
            for (int index = 0; index < gradeTabs.Length; index++)
            {
                bool active = index < grades.Length && grades[index] > 0;
                gradeTabs[index].gameObject.SetActive(active);
                if (!active) continue;
                int grade = grades[index];
                gradeTabs[index].OnValueChanged.AddListener(isOn =>
                {
                    if (isOn) RefreshDailyDungeonLeaderboard(grade);
                });
            }
            selectedLeaderboardGrade = grades.FirstOrDefault(value => value == host.DailyDungeons.BestGrade(
                selectedDungeonId, selectedDifficulty));
            if (selectedLeaderboardGrade <= 0) selectedLeaderboardGrade = grades.FirstOrDefault(value => value > 0);
            for (int index = 0; index < gradeTabs.Length && index < grades.Length; index++)
                gradeTabs[index].SetIsOnWithoutNotify(grades[index] == selectedLeaderboardGrade, true);
            Transform giftTabNode = Find(dailyDungeonLeaderboardRoot.transform, "DailyDungeonTopPanelAchievementTabBottomBtn");
            var giftTab = giftTabNode == null ? null : giftTabNode.GetComponent<Zjcs.Official.WorldUI.WorldTabItem>();
            if (giftTab != null)
            {
                giftTab.gameObject.SetActive(true);
                giftTab.SetIsOnWithoutNotify(false, true);
                giftTab.OnValueChanged.AddListener(isOn => { if (isOn) RefreshDailyDungeonLeaderboardGifts(); });
            }
            RefreshDailyDungeonLeaderboard(selectedLeaderboardGrade);
        }

        private void RefreshDailyDungeonLeaderboard(int grade)
        {
            if (dailyDungeonLeaderboardRoot == null) return;
            SetPrefabActive(dailyDungeonLeaderboardRoot, "AchievementPanel", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "TeamTopContent", true);
            selectedLeaderboardGrade = grade;
            DungeonLeaderboardBinding binding = LoadDungeonOfficialBindings("DailyDungeonTopPanel").leaderboard;
            Transform scrollNode = FindOfficialBindingNode(dailyDungeonLeaderboardRoot.transform, binding.mScrollRect, null);
            LoopVerticalScrollRect scroll = scrollNode == null ? null : scrollNode.GetComponent<LoopVerticalScrollRect>();
            if (scroll == null || scroll.content == null)
                throw new InvalidOperationException("Official daily dungeon leaderboard content is missing.");
            IReadOnlyList<DailyDungeonLeaderboardEntry> rankedEntries = grade <= 0
                ? Array.Empty<DailyDungeonLeaderboardEntry>()
                : host.DailyDungeons.Leaderboard(selectedDungeonId, selectedDifficulty, grade,
                    int.MaxValue);
            IReadOnlyList<DailyDungeonLeaderboardEntry> entries = rankedEntries
                .Take(host.DailyDungeons.Options.LeaderboardLimit).ToArray();
            DailyDungeonLeaderboardEntry myEntry = rankedEntries.FirstOrDefault(value => value.IsSelf);
            DungeonLeaderboardBinding rowBinding = LoadDungeonOfficialBindings("TopRankTeamItem").leaderboard;
            scroll.dataSource = new DailyDungeonLeaderboardRows((rowTransform, index) =>
            {
                BindDailyDungeonLeaderboardRow(rowTransform.gameObject, entries[index], rowBinding, true);
            });
            scroll.totalCount = entries.Count;
            SetLeaderboardObjectState(dailyDungeonLeaderboardRoot, binding.mEmptyStateSwitcher, entries.Count == 0 ? 1 : 0);
            if (myEntry == null)
            {
                SetLeaderboardObjectState(dailyDungeonLeaderboardRoot, binding.mMyTopStateSwitcher, 0);
            }
            else
            {
                Transform myTopItem = FindOfficialBindingNode(dailyDungeonLeaderboardRoot.transform,
                    binding.mMeTopItem, null);
                if (myTopItem == null)
                    throw new InvalidOperationException("Official daily dungeon personal leaderboard row is missing.");
                BindDailyDungeonLeaderboardRow(myTopItem.gameObject, myEntry, rowBinding, false);
                SetLeaderboardObjectState(dailyDungeonLeaderboardRoot, binding.mMyTopStateSwitcher, 1);
            }
            // The server callback arrives after the original panel has laid out.
            // A local reply must also wait for its first valid viewport.
            host.StartCoroutine(FillDailyDungeonLeaderboard(dailyDungeonLeaderboardRoot, scroll, scroll.dataSource));
            Transform content = FindOfficialBindingNode(dailyDungeonLeaderboardRoot.transform, binding.mContent, null);
            CanvasGroup group = content == null ? null : content.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
        }

        private void BindDailyDungeonLeaderboardRow(GameObject row, DailyDungeonLeaderboardEntry entry,
            DungeonLeaderboardBinding rowBinding, bool applyPrefabBindings)
        {
            if (applyPrefabBindings) ApplyOfficialDungeonBindings(row, "TopRankTeamItem");
            SetNamedText(row.transform, "RankText", entry.Rank.ToString());
            SetNamedText(row.transform, "ScoreText", FormatDailyDungeonFinishTime(entry.FinishTicks));
            SetNamedText(row.transform, "Name", entry.PlayerName);
            Transform team = Find(row.transform, "Team");
            if (team != null)
            {
                bool showedPlayer = false;
                foreach (Transform member in team)
                {
                    bool playerSlot = !showedPlayer && member.name.StartsWith(
                        "TopRankTeamMemberIcon", StringComparison.Ordinal);
                    if (member.name.StartsWith("TopRankTeamMemberIcon", StringComparison.Ordinal))
                    {
                        member.gameObject.SetActive(playerSlot);
                        showedPlayer |= playerSlot;
                        if (playerSlot)
                        {
                            SetPrefabActive(member.gameObject, "NpcIcon", false);
                            SetPrefabActive(member.gameObject, "ImgEmpty", false);
                            var portrait = member.GetComponent<DailyDungeonResultPlayerRowAdapter>()
                                ?? member.gameObject.AddComponent<DailyDungeonResultPlayerRowAdapter>();
                            portrait.Configure(host);
                        }
                    }
                }
            }
            SetLeaderboardObjectState(row, rowBinding.mMeStateSwitcher, entry.IsSelf ? 1 : 0);
            SetLeaderboardObjectState(row, rowBinding.mTopStateSwticher, entry.Rank);
        }

        private void RefreshDailyDungeonLeaderboardGifts()
        {
            if (dailyDungeonLeaderboardRoot == null) return;
            SetPrefabActive(dailyDungeonLeaderboardRoot, "TeamTopContent", false);
            SetPrefabActive(dailyDungeonLeaderboardRoot, "AchievementPanel", true);
            DungeonOfficialPrefabBinding binding = LoadDungeonOfficialBindings("DailyDungeonTopPanel");
            IReadOnlyList<DailyDungeonLeaderboardGift> gifts = host.DailyDungeons.BuildLeaderboardGiftModel(
                selectedDungeonId, selectedDifficulty);
            DungeonLeaderboardAchievementBinding[] rows = binding.achievementRows
                ?? throw new InvalidOperationException("Original leaderboard achievement rows are missing.");
            for (int index = 0; index < rows.Length; index++)
            {
                DungeonLeaderboardAchievementBinding rowBinding = rows[index];
                Transform row = FindOfficialBindingNode(dailyDungeonLeaderboardRoot.transform, rowBinding.path, null);
                if (row == null) throw new InvalidOperationException("Original leaderboard achievement row is missing.");
                row.gameObject.SetActive(index < gifts.Count);
                if (index >= gifts.Count) continue;
                DailyDungeonLeaderboardGift gift = gifts[index];
                SetNamedText(row, "TitleTxt", gift.Title);
                SetNamedText(row, "ProgressTxt", Math.Min(gift.Progress, gift.RequiredCount) + "/" + gift.RequiredCount);
                Slider slider = row.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    slider.minValue = 0; slider.maxValue = gift.RequiredCount;
                    slider.value = Math.Min(gift.Progress, gift.RequiredCount);
                }
                SetLeaderboardObjectState(dailyDungeonLeaderboardRoot, rowBinding.states, (int)gift.State);
                foreach (DungeonLeaderboardColorState color in rowBinding.colors ?? Array.Empty<DungeonLeaderboardColorState>())
                {
                    if (color.state != (int)gift.State) continue;
                    foreach (string path in color.objects)
                    {
                        Transform node = FindOfficialBindingNode(dailyDungeonLeaderboardRoot.transform, path, null);
                        Graphic graphic = node == null ? null : node.GetComponent<Graphic>();
                        if (graphic != null) graphic.color = ReadColor(color.color);
                    }
                }
                Button receive = Find(row, "ReceiveBtn")?.GetComponent<Button>();
                if (receive != null)
                {
                    receive.onClick.RemoveAllListeners();
                    receive.interactable = gift.State == DailyDungeonAchievementState.Completed;
                    int id = gift.Id;
                    receive.onClick.AddListener(() =>
                    {
                        if (!host.ClaimDailyDungeonLeaderboardGift(id)) Notify("馈赠奖励领取失败");
                        RefreshDailyDungeonLeaderboardGifts();
                    });
                }
                RefreshDailyDungeonAchievementRewards(row, gift.Rewards);
            }
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator FillDailyDungeonLeaderboard(GameObject root, LoopVerticalScrollRect scroll,
            LoopScrollDataSource source)
        {
            yield return null;
            if (root == null || root != dailyDungeonLeaderboardRoot || scroll == null
                || !ReferenceEquals(scroll.dataSource, source)) yield break;
            Canvas.ForceUpdateCanvases();
            scroll.RefillCells();
        }

        private sealed class DailyDungeonLeaderboardRows : LoopScrollDataSource
        {
            private readonly Action<Transform, int> provide;
            public DailyDungeonLeaderboardRows(Action<Transform, int> provide) { this.provide = provide; }
            public void ProvideData(Transform transform, int index) { provide(transform, index); }
        }

        private static void SetLeaderboardObjectState(GameObject root, DungeonLeaderboardState[] states, int state)
        {
            // Original StateGameObjectConfig retains objects shared by states,
            // disables the other objects first, then enables the selected list.
            string[] selected = states?.FirstOrDefault(row => row.state == state)?.objects;
            foreach (DungeonLeaderboardState row in states ?? Array.Empty<DungeonLeaderboardState>())
            {
                if (row.state == state) continue;
                foreach (string path in row.objects)
                {
                    if (selected != null && selected.Contains(path)) continue;
                    Transform target = FindOfficialBindingNode(root.transform, path, null);
                    if (target == null) throw new InvalidOperationException("Official leaderboard state target is missing: " + path);
                    target.gameObject.SetActive(false);
                }
            }
            foreach (string path in selected ?? Array.Empty<string>())
            {
                Transform target = FindOfficialBindingNode(root.transform, path, null);
                if (target == null) throw new InvalidOperationException("Official leaderboard state target is missing: " + path);
                target.gameObject.SetActive(true);
            }
        }

        private static string FormatDailyDungeonFinishTime(long ticks)
        {
            TimeSpan elapsed = TimeSpan.FromTicks(Math.Max(0, ticks));
            return elapsed.TotalHours >= 1
                ? ((int)elapsed.TotalHours).ToString("00") + elapsed.ToString(@"\:mm\:ss")
                : elapsed.ToString(@"mm\:ss\.ff");
        }

        private void CloseDailyDungeonLeaderboard()
        {
            if (dailyDungeonLeaderboardRoot != null) UnityEngine.Object.Destroy(dailyDungeonLeaderboardRoot);
            dailyDungeonLeaderboardRoot = null;
        }

        private void NavigateDailyDungeon(int direction)
        {
            IReadOnlyList<(int dungeonId, int difficulty)> order = host.DailyDungeons.NavigationOrder(
                IsDailyDungeonPublic, (dungeonId, conditionId) =>
                    IsDailyDungeonConditionMet(dungeonId, conditionId));
            int current = -1;
            for (int index = 0; index < order.Count; index++)
                if (order[index].dungeonId == selectedDungeonId
                    && order[index].difficulty == selectedDifficulty)
                {
                    current = index;
                    break;
                }
            int next = current + direction;
            if (current < 0 || next < 0 || next >= order.Count) return;
            OpenDailyDungeonDetail(order[next].dungeonId, order[next].difficulty);
        }

        private void BindDifficultyTabs(int dungeonId)
        {
            Transform tabs = Find(dailyDungeonDialogRoot.transform, "TabGroup");
            if (tabs == null) return;
            var difficulties = host.DailyDungeons.VisibleDifficulties(dungeonId,
                conditionId => IsDailyDungeonConditionMet(dungeonId, conditionId))
                .Where(value => host.DailyDungeons.ResolveLevel(dungeonId, value.difficulty)?.IsPrivateRoute != true)
                .ToArray();
            var buttons = new List<Button>();
            foreach (Transform tab in tabs)
            {
                Button button = tab.GetComponent<Button>() ?? tab.GetComponentInChildren<Button>(true);
                if (button != null) buttons.Add(button);
            }
            int count = Math.Min(difficulties.Length, buttons.Count);
            for (int index = 0; index < count; index++)
            {
                int difficulty = difficulties[index].difficulty;
                Transform tabRoot = buttons[index].transform;
                bool tabUnlocked = index == 0 || host.DailyDungeons.IsUnlocked(dungeonId,
                    difficulties[index - 1].difficulty);
                SetPrefabActive(tabRoot.gameObject, "LockIconImg", !tabUnlocked);
                SetPrefabActive(tabRoot.gameObject, "LockText", !tabUnlocked);
                Transform lockText = Find(tabRoot, "LockText");
                Button lockTip = lockText == null ? null
                    : lockText.GetComponent<Button>() ?? lockText.GetComponentInChildren<Button>(true);
                if (!tabUnlocked && lockTip != null)
                {
                    int previousDifficulty = difficulties[index - 1].difficulty;
                    lockTip.onClick.AddListener(() => Notify("请先解锁前一难度："
                        + previousDifficulty));
                }
                buttons[index].onClick.AddListener(() =>
                {
                    selectedDifficulty = difficulty;
                    RefreshDailyDungeonDetail();
                });
            }
        }

        private void RefreshDailyDungeonDetail()
        {
            if (dailyDungeonDialogRoot == null) return;
            bool unlocked = host.DailyDungeons.IsDifficultyUnlocked(selectedDungeonId,
                selectedDifficulty, conditionId => IsDailyDungeonConditionMet(selectedDungeonId, conditionId));
            DailyDungeonHudModel model = host.DailyDungeons.BuildHudModel(
                selectedDungeonId, selectedDifficulty,
                conditionId => IsDailyDungeonConditionMet(selectedDungeonId, conditionId));
            unlocked = model.IsUnlocked;
            int best = model.BestGrade;
            SetNamedText(dailyDungeonDialogRoot.transform, "DescTxt",
                "難度 " + selectedDifficulty + " · 最高评级 " + (best <= 0 ? "暂无" : best.ToString())
                + " · 今日剩余领奖次数 " + model.RemainingEntries);
            RefreshDailyDungeonUnlockConditions(unlocked);
            SetPrefabActive(dailyDungeonDialogRoot, "UnlockBtn", !unlocked
                && (selectedDifficulty > 0 || host.DailyDungeons.FindDungeon(selectedDungeonId)
                    ?.difficulties?.FirstOrDefault(value => value.difficulty == selectedDifficulty)
                    ?.unlockConditionId > 0));
            SetPrefabActive(dailyDungeonDialogRoot, "BattleBtn", unlocked);
            SetPrefabActive(dailyDungeonDialogRoot, "GoToBtn", unlocked);
            World.OfficialDungeonEntryBindings.RefreshCatalogueDetail(dailyDungeonDialogRoot, unlocked,
                host.DailyDungeons.CanEnter(selectedDungeonId, selectedDifficulty, true,
                    condition => IsDailyDungeonConditionMet(selectedDungeonId, condition)));
        }

        private void RefreshDailyDungeonUnlockConditions(bool unlocked)
        {
            Transform lockRoot = Find(dailyDungeonDialogRoot.transform, "LockGo");
            if (lockRoot == null) return;
            lockRoot.gameObject.SetActive(!unlocked);
            if (unlocked) return;

            var dungeon = host.DailyDungeons.FindDungeon(selectedDungeonId);
            var difficulty = dungeon?.difficulties?.FirstOrDefault(value =>
                value.difficulty == selectedDifficulty);
            if (difficulty == null) return;
            DailyDungeonRuntimeCondition condition = host.DailyDungeons.Catalog.conditions
                .FirstOrDefault(value => value.id == difficulty.unlockConditionId);
            var conditions = new List<DailyDungeonRuntimeCondition>();
            CollectUnlockConditions(condition, conditions, 0);
            DailyDungeonRuntimeCondition level = conditions.FirstOrDefault(value =>
                value.type == "LevelCondition");
            DailyDungeonRuntimeCondition rating = conditions.FirstOrDefault(value =>
                value.type == "CombatRatingOrRankSuppressCondition");
            DailyDungeonRuntimeCondition grade = conditions.FirstOrDefault(value =>
                value.type == "DailyDungeonPersonalGradeCondition");
            int requiredHighSubRank = rating == null ? 0
                : host.DailyDungeonHighSubRank(rating.rank);
            bool rankSuppressed = rating != null
                && host.SubRank >= host.DailyDungeons.Catalog.settings.SuppressUnlockRank
                && requiredHighSubRank > 0
                && host.DailyDungeonRankForSubRank(host.SubRank)
                    > host.DailyDungeonRankForSubRank(requiredHighSubRank);
            SetPrefabActive(lockRoot.gameObject, "LevelLimitGo", level != null);
            SetPrefabActive(lockRoot.gameObject, "CombatRatingLimitGo", rating != null && !rankSuppressed);
            SetPrefabActive(lockRoot.gameObject, "RankSuppressionGo", rankSuppressed);
            SetPrefabActive(lockRoot.gameObject, "BattleGradeLimitGo", grade != null);
            SetPrefabActive(lockRoot.gameObject, "TimeLimitGo", false);
            if (level != null)
                SetNamedText(lockRoot, "LimitTxt", "等级达到 " + level.level);
            if (rating != null)
            {
                SetNamedText(lockRoot, "CombatRatingLimitTxt", "战力达到 " + rating.combatRating);
                SetNamedText(lockRoot, "RankSuppressionTxt", "或达到对应阶位压制");
            }
            if (grade != null)
                SetNamedText(lockRoot, "BattleGradeLimitTxt", "副本 " + grade.dungeonId
                    + " 难度 " + grade.difficulty + " 评级达到 " + grade.gradeType);
        }

        private void CollectUnlockConditions(DailyDungeonRuntimeCondition condition,
            List<DailyDungeonRuntimeCondition> result, int depth)
        {
            if (condition == null || depth > 16) return;
            if (condition.type == "CompositeAndCondition" || condition.type == "CompositeOrCondition")
            {
                int[] children = condition.type == "CompositeAndCondition"
                    ? condition.andIds : condition.orIds;
                foreach (int id in children ?? Array.Empty<int>())
                    CollectUnlockConditions(host.DailyDungeons.Catalog.conditions
                        .FirstOrDefault(value => value.id == id), result, depth + 1);
                return;
            }
            result.Add(condition);
        }

        private void BeginSelectedDailyDungeon()
        {
            if (host.DailyDungeons.ActiveRun != null)
            {
                var activeRun = host.DailyDungeons.ActiveRun;
                if (activeRun.dungeonId == selectedDungeonId
                    && activeRun.difficulty == selectedDifficulty)
                    OpenDailyDungeonRoute();
                else
                    Notify("已有其他副本路线，请先继续或离开该路线");
                return;
            }
            string error;
            bool entered = host.DailyDungeons.TryBegin(selectedDungeonId, selectedDifficulty,
                IsDailyDungeonPublic, IsDailyDungeonEntryConditionMet, host.ConsumeDailyDungeonItem,
                host.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId,
                DateTimeOffset.Now.ToUnixTimeSeconds(), out error);
            if (!entered)
            {
                Notify(error ?? "当前副本不可进入");
                return;
            }
            RefreshDailyDungeonEntryPage();
            OpenDailyDungeonRoute();
        }

        private void UnlockSelectedDailyDungeonDifficulty()
        {
            bool unlocked = host.DailyDungeons.UnlockDifficulty(selectedDungeonId, selectedDifficulty,
                conditionId => IsDailyDungeonConditionMet(selectedDungeonId, conditionId));
            if (!unlocked)
            {
                Notify("未满足该难度的官方解锁条件");
                return;
            }
            RefreshDailyDungeonDetail();
        }

        private bool IsDailyDungeonPublic(int dungeonId)
        {
            var dungeon = host.DailyDungeons.FindDungeon(dungeonId);
            return dungeon != null && IsMapRegionPublic(dungeon.entryMapClassId, dungeon.entryRegionId);
        }

        private bool IsDailyDungeonEntryConditionMet(int dungeonId, int conditionId) =>
            IsDailyDungeonConditionMet(dungeonId, conditionId);

        private bool IsDailyDungeonConditionMet(int dungeonId, int conditionId)
        {
            return host.DailyDungeons.IsConditionMet(conditionId, host.Level,
                host.DailyDungeonCombatRating(), host.SubRank, IsMapRegionPublic,
                host.DailyDungeonHighSubRank, host.DailyDungeonRankForSubRank,
                host.Profile.Save.Player.Wardrobe.Profession, host.DailyDungeonProfessionMatches,
                host.Profile.InventoryCount);
        }

        private void OpenDailyDungeonRoute()
        {
            CloseDungeonDetail();
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonBigMapDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon route map is missing.");
            dailyDungeonDialogRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonDialogRoot.name = "DailyDungeonBigMapDialog";
            dailyDungeonDialogRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonDialogRoot);
            RenderDailyDungeonRoute();
        }

        private void OpenDailyDungeonAction()
        {
            if (host.DailyDungeons.ActiveRun != null) OpenDailyDungeonRoute();
            else OpenPendingDailyDungeonClaim();
        }

        private void OpenPendingDailyDungeonClaim()
        {
            if (dailyDungeonClaimConfirmRoot != null) return;
            DailyDungeonPendingClaimSave pending = host.DailyDungeons.PendingClaims
                .OrderByDescending(value => value != null && value.awardTimes > 0)
                .FirstOrDefault();
            if (pending?.run == null) return;
            DailyDungeonEntryAllowanceState allowance = host.DailyDungeons.EntryAllowance();
            int awardTimes = pending.awardTimes > 0 ? pending.awardTimes
                : Math.Min(host.DailyDungeons.MaxAwardTimesPerClaim, host.DailyDungeons.RemainEntries);
            bool canPurchase = allowance.PurchaseRemaining > 0
                && host.DailyDungeons.ClaimCapacity > 0;
            if (awardTimes <= 0 && !canPurchase)
            {
                ShowDailyDungeonClaimConfirm(pending, 0);
                return;
            }
            if (awardTimes <= 0)
            {
                ShowDailyDungeonClaimConfirm(pending, 0);
                return;
            }
            ShowDailyDungeonClaimConfirm(pending, awardTimes);
        }

        private void ShowDailyDungeonClaimConfirm(DailyDungeonPendingClaimSave pending,
            int awardTimes)
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Fight/GameEndPanelDailyDungeon");
            if (prefab == null)
                throw new InvalidOperationException("Official daily dungeon result panel is missing.");
            DailyDungeonSettlementHudModel settlement = host.DailyDungeons.BuildSettlementHudModel(
                pending, awardTimes, host.Level, host.SubRank);
            if (settlement == null)
            {
                CloseDailyDungeonClaimConfirm();
                return;
            }
            awardTimes = settlement.AwardTimes;
            dailyDungeonResultPanelAdapter = DailyDungeonResultPanelAdapter.Create(prefab, canvas.transform,
                DungeonUiBindings);
            dailyDungeonClaimConfirmRoot = dailyDungeonResultPanelAdapter.gameObject;
            dailyDungeonClaimConfirmRoot.name = "GameEndPanelDailyDungeon";
            dailyDungeonClaimConfirmRoot.transform.SetAsLastSibling();
            dailyDungeonResultPanelAdapter.Configure(settlement, host, settlement.CanClaim,
                settlement.CanPurchaseEntries, () =>
            {
                IReadOnlyList<DailyDungeonClaimRewardDisplayItem> receivedRewards =
                    Array.Empty<DailyDungeonClaimRewardDisplayItem>();
                bool claimed = host.ClaimDailyDungeonRewards(pending.id, awardTimes, out receivedRewards,
                    out bool hasRewardDetails);
                if (!claimed)
                {
                    Notify("副本奖励领取失败，请重试");
                    return;
                }
                dailyDungeonResultPanelAdapter?.ShowClaimedRewards(receivedRewards);
                RefreshDailyDungeonEntryPage();
                Notify(hasRewardDetails ? "副本奖励领取成功" : "历史奖励已入库，明细不可恢复");
            }, OpenDailyDungeonBuyCountDialog, () =>
            {
                CloseDailyDungeonClaimConfirm();
                RefreshDailyDungeonEntryPage();
            });
            PlaceModalCanvasOnTop(dailyDungeonClaimConfirmRoot);
        }

        private void OpenDailyDungeonBuyCountDialog()
        {
            if (dailyDungeonBuyCountRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonBuyCountDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon count purchase dialog is missing.");
            dailyDungeonBuyCountRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonBuyCountRoot.name = "DailyDungeonBuyCountDialog";
            dailyDungeonBuyCountRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonBuyCountRoot);
            DailyDungeonEntryAllowanceState allowance = host.DailyDungeons.EntryAllowance();
            dailyDungeonBuyCountLimit = Math.Min(allowance.PurchaseRemaining,
                host.DailyDungeons.ClaimCapacity);
            Transform sliderNode = Find(dailyDungeonBuyCountRoot.transform, "Slider");
            dailyDungeonBuyCountSlider = sliderNode == null ? null : sliderNode.GetComponent<Slider>();
            if (dailyDungeonBuyCountSlider == null)
                throw new InvalidOperationException("Official daily dungeon purchase slider is missing.");
            dailyDungeonBuyCountSlider.minValue = 1;
            dailyDungeonBuyCountSlider.maxValue = Math.Max(1, dailyDungeonBuyCountLimit);
            dailyDungeonBuyCountSlider.wholeNumbers = true;
            dailyDungeonBuyCountSlider.value = Mathf.Clamp(
                host.DailyDungeons.Catalog.settings.DailyDungeonDefaultBuyCount,
                1, Math.Max(1, dailyDungeonBuyCountLimit));
            dailyDungeonBuyCountSlider.onValueChanged.AddListener(RefreshDailyDungeonPurchaseCost);
            BindPrefabButton(dailyDungeonBuyCountRoot, "CloseBtn", CloseDailyDungeonBuyCountDialog);
            BindPrefabButton(dailyDungeonBuyCountRoot, "CloseBtn_dark", CloseDailyDungeonBuyCountDialog);
            BindPrefabButton(dailyDungeonBuyCountRoot, "CommonBtn_11", CloseDailyDungeonBuyCountDialog);
            BindPrefabButton(dailyDungeonBuyCountRoot, "CommonBtn_12", PurchaseDailyDungeonClaims);
            BindPrefabButton(dailyDungeonBuyCountRoot, "CommonBtn_Reduce", () =>
                dailyDungeonBuyCountSlider.value = Mathf.Max(1f, dailyDungeonBuyCountSlider.value - 1f));
            BindPrefabButton(dailyDungeonBuyCountRoot, "CommonBtn_Add", () =>
                dailyDungeonBuyCountSlider.value = Mathf.Min(dailyDungeonBuyCountLimit,
                    dailyDungeonBuyCountSlider.value + 1f));
            SetNamedText(dailyDungeonBuyCountRoot.transform, "TipText", "购买额外副本领奖次数");
            SetNamedText(dailyDungeonBuyCountRoot.transform, "LimitText",
                "剩余可购买 " + allowance.PurchaseRemaining + " 次");
            RefreshDailyDungeonPurchaseCost(dailyDungeonBuyCountSlider.value);
        }

        private void RefreshDailyDungeonPurchaseCost(float value)
        {
            if (dailyDungeonBuyCountRoot == null || dailyDungeonBuyCountSlider == null) return;
            int count = Mathf.Clamp(Mathf.RoundToInt(value), 1, Math.Max(1, dailyDungeonBuyCountLimit));
            long[] prices = host.DailyDungeons.Catalog.settings.DailyDungeonBuyCostCountList
                ?? Array.Empty<long>();
            int priceIndex = host.DailyDungeons.Save.purchasedEntries;
            long cost = 0;
            bool validPrice = priceIndex >= 0 && (long)priceIndex + count <= prices.Length;
            try
            {
                for (int index = 0; validPrice && index < count; index++)
                {
                    long price = prices[priceIndex + index];
                    if (price <= 0) { validPrice = false; break; }
                    cost = checked(cost + price);
                }
            }
            catch (OverflowException) { validPrice = false; }
            SetNamedText(dailyDungeonBuyCountRoot.transform, "BuyCountText", count.ToString());
            SetNamedText(dailyDungeonBuyCountRoot.transform, "CostCountText",
                validPrice ? cost.ToString() : "—");
            SetNamedText(dailyDungeonBuyCountRoot.transform, "CostText",
                host.ItemName(host.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId));
        }

        private void PurchaseDailyDungeonClaims()
        {
            if (dailyDungeonBuyCountSlider == null) return;
            int count = Mathf.RoundToInt(dailyDungeonBuyCountSlider.value);
            string receipt = "daily-dungeon:claim-purchase:" + host.DailyDungeons.Save.localDate
                + ":" + host.DailyDungeons.Save.purchasedEntries;
            if (!host.DailyDungeons.TryPurchaseEntries(receipt, count,
                new DailyDungeonEconomyAdapter(host)))
            {
                Notify(host.ItemName(host.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId)
                    + "不足或今日可购买次数已用完");
                return;
            }
            CloseDailyDungeonBuyCountDialog();
            CloseDailyDungeonClaimConfirm();
            RefreshDailyDungeonEntryPage();
            OpenPendingDailyDungeonClaim();
        }

        private void CloseDailyDungeonBuyCountDialog()
        {
            if (dailyDungeonBuyCountRoot != null) UnityEngine.Object.Destroy(dailyDungeonBuyCountRoot);
            dailyDungeonBuyCountRoot = null;
            dailyDungeonBuyCountSlider = null;
            dailyDungeonBuyCountLimit = 0;
        }

        private void CloseDailyDungeonClaimConfirm()
        {
            if (dailyDungeonClaimConfirmRoot != null) UnityEngine.Object.Destroy(dailyDungeonClaimConfirmRoot);
            dailyDungeonClaimConfirmRoot = null;
            dailyDungeonResultPanelAdapter = null;
        }

        private sealed class DailyDungeonEconomyAdapter : IDailyDungeonEconomyAdapter
        {
            private readonly HomeFeatureRuntime runtime;
            public DailyDungeonEconomyAdapter(HomeFeatureRuntime runtime) => this.runtime = runtime;
            public bool Consume(string receipt, int itemId, long count) =>
                runtime.ConsumeDailyDungeonItem(receipt, itemId, count);
        }

        private void RenderDailyDungeonRoute()
        {
            if (dailyDungeonDialogRoot == null || host.DailyDungeons.ActiveRun == null) return;
            World.OfficialDungeonRoutePresenter.Refresh(dailyDungeonDialogRoot, host.DailyDungeons,
                SelectDailyDungeonRoom, ShowDailyDungeonLeaveConfirm);
        }

        private void SelectDailyDungeonRoom(int roomId)
        {
            var run = host.DailyDungeons.ActiveRun;
            if (run == null) return;
            if (run.clearedRoomIds.Contains(roomId)) return;
            if (roomId != run.currentRoomId)
            {
                if (!host.DailyDungeons.ChooseNextRoom(roomId)) return;
                run = host.DailyDungeons.ActiveRun;
            }
            var room = host.DailyDungeons.FindRoom(roomId);
            var stage = room == null ? null : host.DailyDungeons.FindStage(room.stageId);
            if (stage != null && (stage.waveArrayIds?.Length > 0
                    || stage.waveMonsterIds?.Any(wave => wave != null && wave.Length > 0) == true))
            {
                OpenDailyDungeonAssist(roomId);
                return;
            }
            if (host.DailyDungeons.RecordRoomCleared(roomId, string.Empty))
            {
                if (room.nextRoomIds == null || room.nextRoomIds.Length == 0)
                {
                    int grade = host.DailyDungeons.CurrentGrade();
                    if (grade > 0 && host.DailyDungeons.Finish(grade))
                    {
                        CloseDungeonDetail();
                        RefreshDailyDungeonEntryPage();
                        OpenPendingDailyDungeonClaim();
                        return;
                    }
                }
                RenderDailyDungeonRoute();
            }
        }

        private void StartDailyDungeonBattle(int roomId)
        {
            if (activeDungeonBattle != null) return;
            var battleObject = new GameObject("DailyDungeonBattleRunner");
            battleObject.transform.SetParent(host.transform, false);
            activeDungeonBattle = battleObject.AddComponent<DailyDungeonBattleRunner>();
            try
            {
                activeDungeonBattle.Begin(host, roomId, "daily-dungeon:" + roomId,
                    OnDailyDungeonBattleFinished);
            }
            catch
            {
                UnityEngine.Object.Destroy(battleObject);
                activeDungeonBattle = null;
                throw;
            }
        }

        private void OnDailyDungeonBattleFinished(bool won)
        {
            activeDungeonBattle = null;
            if (host.DailyDungeons.ActiveRun == null)
            {
                CloseDungeonDetail();
                RefreshDailyDungeonEntryPage();
                OpenPendingDailyDungeonClaim();
                return;
            }
            if (!won)
            {
                Notify("战斗失败，当前房间未清理，可再次挑战");
                RenderDailyDungeonRoute();
                return;
            }
            var run = host.DailyDungeons.ActiveRun;
            var room = run == null ? null : host.DailyDungeons.FindRoom(run.currentRoomId);
            if (room != null && room.nextRoomIds != null && room.nextRoomIds.Length == 0)
            {
                int grade = host.DailyDungeons.CurrentGrade();
                if (grade > 0 && host.DailyDungeons.Finish(grade))
                {
                    CloseDungeonDetail();
                    RefreshDailyDungeonEntryPage();
                    OpenPendingDailyDungeonClaim();
                    return;
                }
            }
            Notify("房间已清理");
            RenderDailyDungeonRoute();
        }

        private void LeaveDailyDungeonRoute()
        {
            bool left = host.DailyDungeons.Leave(GrantDailyDungeonRoomRewards);
            if (!left) return;
            CloseDailyDungeonLeaveConfirm();
            CloseDungeonDetail();
            RefreshDailyDungeonEntryPage();
            if (host.DailyDungeons.PendingClaims.Count > 0) OpenPendingDailyDungeonClaim();
            else Notify("已离开路线，已结算清理房间掉落；尚未达到评级，不消耗领奖次数");
        }

        private void GrantDailyDungeonRoomRewards(string receipt, Zjcs.Offline.World.DailyDungeonRunSave run)
        {
            if (!host.SettleDailyDungeonRooms(receipt, run))
                throw new InvalidOperationException("Official daily dungeon reward settlement failed.");
        }

        private void CloseDailyDungeonRoute()
        {
            CloseDungeonDetail();
            RefreshDailyDungeonEntryPage();
            if (host.DailyDungeons.ActiveRun != null)
                Notify("路线已保存，可从副本列表继续");
        }

        private void ShowDailyDungeonLeaveConfirm()
        {
            if (activeDungeonBattle != null)
            {
                Notify("战斗结束后才能离开路线");
                return;
            }
            if (dailyDungeonLeaveConfirmRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonConfirmEnterPanel");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon confirmation panel is missing.");
            dailyDungeonLeaveConfirmRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonLeaveConfirmRoot.name = "DailyDungeonLeaveConfirm";
            dailyDungeonLeaveConfirmRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(dailyDungeonLeaveConfirmRoot);
            ApplyOfficialDungeonBindings(dailyDungeonLeaveConfirmRoot, "DailyDungeonConfirmEnterPanel");
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "TitleText", "离开副本路线");
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "Txt", "离开后按已通过行数结算当前评级奖励及房间掉落；未达到评级时仅结算房间掉落");
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "CommonBtn_11", "保存并返回");
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "CommonBtn_12", "离开并结算");
            BindPrefabButton(dailyDungeonLeaveConfirmRoot, "CommonBtn_11", () =>
            {
                CloseDailyDungeonLeaveConfirm();
                CloseDailyDungeonRoute();
            });
            BindPrefabButton(dailyDungeonLeaveConfirmRoot, "CommonBtn_12", LeaveDailyDungeonRoute);
        }

        private void CloseDailyDungeonLeaveConfirm()
        {
            if (dailyDungeonLeaveConfirmRoot != null) UnityEngine.Object.Destroy(dailyDungeonLeaveConfirmRoot);
            dailyDungeonLeaveConfirmRoot = null;
        }

        private static string BestGradeText(int grade) => grade <= 0 ? "暂无" : grade.ToString();

        private static void SetNamedText(Transform root, string name, string value)
        {
            Transform node = Find(root, name);
            TMPro.TMP_Text text = node == null ? null : node.GetComponent<TMPro.TMP_Text>();
            if (text == null && node != null) text = node.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null) text.text = value ?? string.Empty;
        }

        private void PlaceModalCanvasOnTop(GameObject root)
        {
            Canvas modalCanvas = root == null ? null : root.GetComponent<Canvas>();
            if (modalCanvas == null || canvas == null) return;
            int sortingOrder = canvas.sortingOrder;
            foreach (Canvas existing in canvas.GetComponentsInChildren<Canvas>(true))
            {
                if (existing != modalCanvas && existing.overrideSorting)
                    sortingOrder = Math.Max(sortingOrder, existing.sortingOrder);
            }
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingLayerID = canvas.sortingLayerID;
            modalCanvas.sortingOrder = sortingOrder + 1;
        }

        private DailyDungeonUiBindingModule DungeonUiBindings => dungeonUiBindingModule
            ?? (dungeonUiBindingModule = new DailyDungeonUiBindingModule(host.Catalog.Font));

        private void ApplyOfficialDungeonBindings(GameObject root, string prefabName) =>
            DungeonUiBindings.Apply(root, prefabName);

        private DungeonOfficialPrefabBinding LoadDungeonOfficialBindings(string prefabName) =>
            DungeonUiBindings.LoadBinding(prefabName);

        private static Transform FindOfficialBindingNode(Transform root, string path, string[] names) =>
            DailyDungeonUiBindingModule.FindNode(root, path, names);

        private static Color ReadColor(float[] values) => DailyDungeonUiBindingModule.ReadColor(values);

        private void BindDungeonArtwork(GameObject root, int dungeonId, string backgroundNode,
            string iconNode)
        {
            var dungeon = host.DailyDungeons.FindDungeon(dungeonId);
            if (dungeon == null) return;
            int mapClass = dungeon.entryMapClassId;
            int variant = host.DailyDungeons.Catalog.dungeons
                .Where(value => value != null && value.entryMapClassId == mapClass)
                .OrderBy(value => value.id).TakeWhile(value => value.id != dungeonId).Count() + 1;
            string prefix = "Map_" + mapClass + "_" + variant;
            string bundle = "defaultpackage_assets_image_ui_notes_dailydungeon_map_" + mapClass + ".bundle";
            SetOfficialPrefabSprite(root, backgroundNode, LoadDungeonUiSprite(bundle,
                "DetailBg_" + prefix));
            SetOfficialPrefabSprite(root, iconNode, LoadDungeonUiSprite(bundle,
                "DetailBuilding_" + prefix));
        }

        private void BindDungeonListArtwork(GameObject root, int dungeonId)
        {
            var dungeon = host.DailyDungeons.FindDungeon(dungeonId);
            if (dungeon == null) return;
            int mapClass = dungeon.entryMapClassId;
            int variant = host.DailyDungeons.Catalog.dungeons
                .Where(value => value != null && value.entryMapClassId == mapClass)
                .OrderBy(value => value.id).TakeWhile(value => value.id != dungeonId).Count() + 1;
            string bundle = "defaultpackage_assets_image_ui_notes_dailydungeon_map_" + mapClass + ".bundle";
            SetOfficialPrefabSprite(root, "Icon", LoadDungeonUiSprite(bundle,
                "ListImg_Map_" + mapClass + "_" + variant));
        }

        private void SetOfficialPrefabSprite(GameObject root, string nodeName, Sprite sprite)
        {
            if (root == null || sprite == null) return;
            Transform node = FindPath(root.transform, nodeName);
            Image image = node == null ? null : node.GetComponent<Image>();
            if (image != null) SetOfficialImage(image, sprite);
        }

        private static Transform FindPath(Transform root, string path)
        {
            Transform current = root;
            string[] parts = path.Split('/');
            for (int index = 0; index < parts.Length; index++)
            {
                current = current == null ? null : index == 0
                    ? Find(current, parts[index]) : current.Find(parts[index]);
                if (current == null) return null;
            }
            return current;
        }

        private static void SetOfficialImage(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.enabled = true;
        }

        private Sprite LoadDungeonUiSprite(string bundle, string spriteName)
        {
            string key = bundle + "/" + spriteName;
            if (dungeonUiSprites.TryGetValue(key, out Sprite cached)) return cached;
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", "Sprites",
                Path.GetFileNameWithoutExtension(bundle), spriteName + ".png");
            if (!File.Exists(path)) return null;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = spriteName;
            dungeonUiSprites.Add(key, sprite);
            return sprite;
        }

        private static void SetPrefabActive(GameObject root, string name, bool active)
        {
            Transform node = Find(root == null ? null : root.transform, name);
            if (node != null) node.gameObject.SetActive(active);
        }

        private void CloseDungeonDetail()
        {
            CloseDailyDungeonLeaderboard();
            if (dailyDungeonDialogRoot != null) UnityEngine.Object.Destroy(dailyDungeonDialogRoot);
            dailyDungeonDialogRoot = null;
        }

        public void ShowFantasyTowerBattleResult(int entryIndex, int layer, bool won, bool autoNext,
            IReadOnlyList<FantasyTowerReward> rewards)
        {
            string stageName = catalog.LocalizedText("stage_DemonBattlefield_" + entryIndex,
                "幻想阶梯 {0}");
            string stageText = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                stageName, layer);
            bool hasNextLayer = host.FantasyTowerCatalog.CurrentLayer(host.FantasyTower, entryIndex) > 0;
            bool canContinue = won && !autoNext && hasNextLayer;
            GameObject panel = OpenDialog("fantasy_tower_end", (renderer, root) =>
            {
                renderer.SetTextByName("StageText", stageText);
                renderer.SetTextByName("Title", won ? "战斗胜利" : "战斗失败");
                renderer.SetActiveByName("ContinueBtn", canContinue);
                renderer.SetActiveByName("RetryBtn", !won);
                renderer.SetActiveByName("CompletedText", won && !hasNextLayer);
                renderer.SetActiveByName("AutoPushText", autoNext);
                Transform rewardGrid = Find(root.transform, "AwardGrid");
                FantasyTowerReward[] visibleRewards = (rewards ?? Array.Empty<FantasyTowerReward>())
                    .Where(value => value != null && value.itemId > 0 && value.count > 0).ToArray();
                if (rewardGrid != null && catalog.HasPanel("award_row"))
                {
                    for (int index = 0; index < visibleRewards.Length; index++)
                    {
                        FantasyTowerReward reward = visibleRewards[index];
                        HomeUiRenderer rowRenderer = Renderer();
                        GameObject row = rowRenderer.Create("award_row", rewardGrid, null);
                        row.name = "FantasyTowerReward_" + reward.itemId;
                        RectTransform rowRect = row.transform as RectTransform;
                        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                        rowRect.pivot = new Vector2(0.5f, 0.5f);
                        rowRect.anchoredPosition = new Vector2(
                            (index - (visibleRewards.Length - 1) * 0.5f) * 188f, 0f);
                        var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                        iconObject.transform.SetParent(row.transform, false);
                        RectTransform iconRect = (RectTransform)iconObject.transform;
                        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                        iconRect.pivot = new Vector2(0.5f, 0.5f);
                        iconRect.anchoredPosition = new Vector2(0f, 4f);
                        iconRect.sizeDelta = new Vector2(76f, 76f);
                        Image icon = iconObject.GetComponent<Image>();
                        icon.sprite = host.ItemIcon(reward.itemId);
                        icon.preserveAspect = true;
                        icon.raycastTarget = false;
                        TextMeshProUGUI name = CreateLabel(row.transform, "ItemName", 18,
                            new Vector2(0f, 62f), TextAlignmentOptions.Center);
                        RectTransform nameRect = name.rectTransform;
                        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
                        nameRect.pivot = new Vector2(0.5f, 0.5f);
                        nameRect.anchoredPosition = new Vector2(0f, 62f);
                        nameRect.sizeDelta = new Vector2(150f, 26f);
                        name.text = host.ItemName(reward.itemId);
                        TextMeshProUGUI count = CreateLabel(row.transform, "ItemCount", 20,
                            new Vector2(0f, -57f), TextAlignmentOptions.Center);
                        RectTransform countRect = count.rectTransform;
                        countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.5f);
                        countRect.pivot = new Vector2(0.5f, 0.5f);
                        countRect.anchoredPosition = new Vector2(0f, -57f);
                        countRect.sizeDelta = new Vector2(150f, 28f);
                        count.text = "×" + reward.count.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                FantasyTowerEntrySave progress = host.FantasyTower.GetEntry(entryIndex);
                bool canAutoPush = progress.clearedLayer >= 3
                    && host.FantasyTowerCatalog.CurrentLayer(host.FantasyTower, entryIndex) > 0;
                Transform autoPushRoot = Find(root.transform, "AutoPush");
                Transform autoToggleRoot = Find(autoPushRoot, "Toggle");
                Transform autoLockRoot = Find(autoPushRoot, "Lock");
                if (autoToggleRoot != null) autoToggleRoot.gameObject.SetActive(canAutoPush);
                if (autoLockRoot != null) autoLockRoot.gameObject.SetActive(!canAutoPush);
                BindPrefabButton(root, "LockedBtn", () => Notify(catalog.LocalizedText(
                    "ui_DemonBattlefield_AutoPushLockedTip", "通过幻想阶梯第3层解锁自动推关")));
                Toggle autoToggle = autoToggleRoot == null ? null : autoToggleRoot.GetComponent<Toggle>();
                if (autoToggle != null)
                {
                    autoToggle.SetIsOnWithoutNotify(progress.autoPushEnabled);
                    autoToggle.onValueChanged.AddListener(enabled =>
                    {
                        renderer.SetActiveByName("ContinueBtn", won && !enabled && hasNextLayer);
                        renderer.SetActiveByName("AutoPushText", enabled);
                        FantasyTowerBattleRunner active = host.GetComponent<FantasyTowerBattleRunner>();
                        if (active != null) active.SetAutoPush(enabled);
                        else if (enabled) host.StartFantasyTowerAfterCountdown(entryIndex);
                        else host.FantasyTower.SetAutoPush(entryIndex, false);
                    });
                }
                if (canContinue || !won)
                    BindPrefabButton(root, won ? "ContinueBtn" : "RetryBtn", () =>
                    {
                        CloseDialog();
                        host.StartFantasyTower(entryIndex);
                    });
                BindPrefabButton(root, "CloseBtn", () =>
                {
                    CloseDialog();
                    ShowFantasyTowerPendingResult();
                });
            });
            panel.name = "FantasyTowerBattleResult";
        }

        public void ShowFantasyTowerPendingResult()
        {
            if (activePanelKey == "fantasy_tower_result") return;
            FantasyTowerPushResult result = host.FantasyTower.EarliestPendingResult();
            if (result == null) return;
            string stageName = catalog.LocalizedText("stage_DemonBattlefield_" + result.entryIndex,
                "幻想阶梯 {0}");
            GameObject panel = OpenDialog("fantasy_tower_result", (renderer, root) =>
            {
                renderer.SetTextByName("PreLevelText", result.startLayer.ToString());
                renderer.SetTextByName("PostLevelText", (result.endLayer + 1).ToString());
                renderer.SetTextByName("TimeCostText", "耗时：" + TimeSpan.FromSeconds(result.elapsedSeconds));
                renderer.SetTextByName("StageText", string.Format(stageName, result.startLayer));
                renderer.SetActiveByName("CompletedText", result.towerCompleted);
                renderer.SetTextByName("ReturnText", "领取并关闭");
                if (result.rewards.Count == 0)
                    renderer.SetTextByName("Text", "本次结算未获得道具");
                BindPrefabButton(root, "CloseBtn", () =>
                {
                    FantasyTowerResult claimed = host.ClaimFantasyTowerResult(result.id);
                    if (claimed != FantasyTowerResult.Applied) return;
                    CloseDialog();
                    ShowFantasyTowerPendingResult();
                });
                Transform grid = Find(root.transform, "AwardGrid");
                FantasyTowerReward[] visible = result.rewards.Where(value => value != null
                    && value.itemId > 0 && value.count > 0).ToArray();
                if (grid == null || !catalog.HasPanel("award_row")) return;
                for (int index = 0; index < visible.Length; index++)
                {
                    FantasyTowerReward reward = visible[index];
                    HomeUiRenderer rowRenderer = Renderer();
                    GameObject row = rowRenderer.Create("award_row", grid, null);
                    row.name = "FantasyTowerBackgroundReward_" + reward.itemId;
                    RectTransform rowRect = row.transform as RectTransform;
                    rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                    rowRect.pivot = new Vector2(0.5f, 0.5f);
                    rowRect.anchoredPosition = new Vector2(
                        (index - (visible.Length - 1) * 0.5f) * 188f, 0f);
                    var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(row.transform, false);
                    RectTransform iconRect = (RectTransform)iconObject.transform;
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(0f, 4f);
                    iconRect.sizeDelta = new Vector2(76f, 76f);
                    Image icon = iconObject.GetComponent<Image>();
                    icon.sprite = host.ItemIcon(reward.itemId);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    TextMeshProUGUI name = CreateLabel(row.transform, "ItemName", 18,
                        new Vector2(0f, 62f), TextAlignmentOptions.Center);
                    RectTransform nameRect = name.rectTransform;
                    nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    nameRect.pivot = new Vector2(0.5f, 0.5f);
                    nameRect.anchoredPosition = new Vector2(0f, 62f);
                    nameRect.sizeDelta = new Vector2(150f, 26f);
                    name.text = host.ItemName(reward.itemId);
                    TextMeshProUGUI count = CreateLabel(row.transform, "ItemCount", 20,
                        new Vector2(0f, -57f), TextAlignmentOptions.Center);
                    RectTransform countRect = count.rectTransform;
                    countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.5f);
                    countRect.pivot = new Vector2(0.5f, 0.5f);
                    countRect.anchoredPosition = new Vector2(0f, -57f);
                    countRect.sizeDelta = new Vector2(150f, 28f);
                    count.text = "×" + reward.count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }
            });
            panel.name = "FantasyTowerPendingResult";
        }

        private static void BindPrefabButton(GameObject root, string name, Action action)
        {
            Transform node = Find(root == null ? null : root.transform, name);
            if (node == null) return;
            Button button = node.GetComponent<Button>() ?? node.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                button = node.gameObject.AddComponent<Button>();
                button.targetGraphic = node.GetComponent<Graphic>()
                    ?? node.GetComponentInChildren<Graphic>(true);
            }
            button.onClick.AddListener(() => action());
        }

        private static void SetPrefabText(GameObject root, string name, string value)
        {
            Transform node = Find(root == null ? null : root.transform, name);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private void CloseDungeonPages()
        {
            if (activeDungeonBattle != null || activeArenaBattle != null) return;
            CloseDailyDungeonLeaveConfirm();
            CloseDailyDungeonClaimConfirm();
            CloseDailyDungeonBuyCountDialog();
            CloseDailyDungeonLeaderboard();
            CloseDungeonDetail();
            if (travelNotesRoot != null) UnityEngine.Object.Destroy(travelNotesRoot);
            CloseMaterialGathering();
            if (dailyDungeonRoot != null) UnityEngine.Object.Destroy(dailyDungeonRoot);
            if (dailyChallengeSelectionRoot != null) UnityEngine.Object.Destroy(dailyChallengeSelectionRoot);
            if (dailyChallengeRoot != null) UnityEngine.Object.Destroy(dailyChallengeRoot);
            if (dailyChallengeResultRoot != null) UnityEngine.Object.Destroy(dailyChallengeResultRoot);
            DestroyFantasyTowerPage();
            travelNotesRoot = null;
            dailyDungeonRoot = null;
            dailyChallengeSelectionRoot = null;
            dailyChallengeRoot = null;
            dailyChallengeResultRoot = null;
            CloseArena();
        }
    }
}
