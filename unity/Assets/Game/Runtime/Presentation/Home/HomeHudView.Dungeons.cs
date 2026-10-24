using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private GameObject travelNotesRoot;
        private GameObject dailyDungeonRoot;
        private GameObject dailyDungeonDialogRoot;
        private GameObject dailyDungeonLeaveConfirmRoot;
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
        private int selectedDungeonId;
        private int selectedDifficulty;

        private bool DungeonModalOpen => travelNotesRoot != null || dailyDungeonRoot != null
            || arenaRoot != null || dailyChallengeRoot != null || dailyChallengeSelectionRoot != null
            || dailyChallengeAssistRoot != null
            || dailyChallengeResultRoot != null
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
            Transform mapPlayEntry = Find(travelNotesRoot.transform, "MapGamePlayEntry");
            if (mapPlayEntry != null) BindPrefabButton(mapPlayEntry.gameObject, "EntryBtn", OpenDailyDungeons);
            Transform dailyActivityEntry = Find(travelNotesRoot.transform, "DailyActivityEntry");
            if (dailyActivityEntry != null) BindPrefabButton(dailyActivityEntry.gameObject, "EntryBtn", OpenDailyChallenges);
            BindPrefabButton(travelNotesRoot, "NormalMaterialEntry", OpenMaterialGatheringEntries);
            AddNpcAdventureTravelEntry();
            BindPrefabButton(travelNotesRoot, "CloseBtn2", CloseDungeonPages);
            Button arenaButton = Find(travelNotesRoot.transform, "ArenaAreaEntry")?.GetComponent<Button>();
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
            BindPrefabButton(travelNotesRoot, "DemonBattleEntry", OpenFantasyTowerEntries);
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
            ConfigureTowerContent(fantasyTowerRowsRoot.transform as RectTransform, 4 * entryRowHeight);
            for (int entry = 1; entry <= 4; entry++)
            {
                int selectedEntry = entry;
                GameObject row = Renderer().CreateNode(panel.root, fantasyTowerRowsRoot.transform);
                row.name = "FantasyTowerEntry_" + entry;
                RectTransform rect = row.transform as RectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -entryRowHeight * (entry - 1));
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
            if (entry < 1 || entry > 4) return;
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
                Button start = Find(row.transform, "StartBtn/CommonBtn_12")?.GetComponent<Button>();
                if (start != null)
                {
                    start.onClick.RemoveAllListeners();
                    start.interactable = layer == currentLayer;
                    int selectedLayer = layer;
                    if (selectedLayer == currentLayer)
                        start.onClick.AddListener(() => StartFantasyTowerLayer(selectedLayer));
                }
                Button autoPush = Find(row.transform, "Right/AutoPush/CommonBtn_12 (1)")?.GetComponent<Button>();
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
            dailyDungeonRoot.transform.SetAsLastSibling();
            BindPrefabButton(dailyDungeonRoot, "CloseBtn2", CloseDungeonPages);
            BindPrefabButton(dailyDungeonRoot, "GoToBtn", OpenDailyDungeonRoute);
            RefreshDailyDungeonEntryPage();
        }

        private void RefreshDailyDungeonEntryPage()
        {
            if (dailyDungeonRoot == null) return;
            SetPrefabText(dailyDungeonRoot, "FreeCount", host.DailyDungeons.RemainEntries.ToString());
            if (host.DailyDungeons.ActiveRun != null)
            {
                SetNamedText(dailyDungeonRoot.transform, "DescTxt", "存在未完成路线 · 今日剩余 "
                    + host.DailyDungeons.RemainEntries);
                SetNamedText(dailyDungeonRoot.transform, "GoToBtn", "继续副本");
                SetPrefabActive(dailyDungeonRoot, "GoToBtn", true);
            }
            int publicCount = 0;
            try
            {
                publicCount = System.Linq.Enumerable.Count(host.DailyDungeons.PublicDungeons(IsMapRegionPublic));
            }
            catch (InvalidOperationException)
            {
                publicCount = 0;
            }
            SetPrefabText(dailyDungeonRoot, "DescTxt", host.DailyDungeons.ActiveRun != null
                ? "存在未完成路线 · 今日剩余 " + host.DailyDungeons.RemainEntries
                : publicCount == 0 ? "尚无已公开的日常副本入口区域"
                    : "已公开副本 " + publicCount + " 个 · 今日剩余 " + host.DailyDungeons.RemainEntries);
            Transform list = Find(dailyDungeonRoot.transform, "DetailContent");
            if (list == null) return;
            foreach (Transform child in list) UnityEngine.Object.Destroy(child.gameObject);
            GameObject itemPrefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonEntryPanelItem");
            if (itemPrefab == null) throw new InvalidOperationException("Official daily dungeon list item is missing.");
            foreach (var dungeon in host.DailyDungeons.PublicDungeons(IsMapRegionPublic))
            {
                GameObject item = UnityEngine.Object.Instantiate(itemPrefab, list, false);
                item.name = "Dungeon_" + dungeon.id;
                SetNamedText(item.transform, "TitleGo", dungeon.title);
                SetNamedText(item.transform, "DescTxt", "最高评级：" + BestGradeText(dungeon.id));
                Button select = item.GetComponent<Button>();
                if (select == null)
                {
                    select = item.AddComponent<Button>();
                    select.targetGraphic = item.GetComponent<Graphic>();
                }
                int id = dungeon.id;
                select.onClick.AddListener(() => OpenDailyDungeonDetail(id));
            }
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
            if (!host.Arena.CompleteChallenge(true, host.GrantArenaReward))
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
            selectedDungeonId = dungeonId;
            selectedDifficulty = requestedDifficulty ?? host.DailyDungeons.DefaultDifficulty(dungeonId);
            if (selectedDifficulty < 0) selectedDifficulty = 0;
            if (dailyDungeonDialogRoot != null) UnityEngine.Object.Destroy(dailyDungeonDialogRoot);
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon detail panel is missing.");
            dailyDungeonDialogRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyDungeonDialogRoot.name = "DailyDungeonDialog";
            dailyDungeonDialogRoot.transform.SetAsLastSibling();
            SetNamedText(dailyDungeonDialogRoot.transform, "TitleTxt", dungeon.title);
            BindDifficultyTabs(dungeonId);
            RefreshDailyDungeonDetail();
            BindPrefabButton(dailyDungeonDialogRoot, "BattleBtn", BeginSelectedDailyDungeon);
            BindPrefabButton(dailyDungeonDialogRoot, "GoToBtn", BeginSelectedDailyDungeon);
            BindPrefabButton(dailyDungeonDialogRoot, "UnlockBtn", UnlockSelectedDailyDungeonDifficulty);
            BindPrefabButton(dailyDungeonDialogRoot, "CloseBtn2", CloseDungeonDetail);
            BindPrefabButton(dailyDungeonDialogRoot, "LeftBtn", () => NavigateDailyDungeon(-1));
            BindPrefabButton(dailyDungeonDialogRoot, "RightBtn", () => NavigateDailyDungeon(1));
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
                .Where(value => value.difficulty != 50).ToArray();
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
            bool unlocked = selectedDifficulty == 0 || host.DailyDungeons.IsUnlocked(
                selectedDungeonId, selectedDifficulty);
            int best = host.DailyDungeons.BestGrade(selectedDungeonId, selectedDifficulty);
            SetNamedText(dailyDungeonDialogRoot.transform, "DescTxt",
                "難度 " + selectedDifficulty + " · 最高评级 " + (best <= 0 ? "暂无" : best.ToString())
                + " · 今日剩餘 " + host.DailyDungeons.RemainEntries);
            RefreshDailyDungeonUnlockConditions(unlocked);
            SetPrefabActive(dailyDungeonDialogRoot, "UnlockBtn", !unlocked && selectedDifficulty > 0);
            SetPrefabActive(dailyDungeonDialogRoot, "BattleBtn", unlocked);
            SetPrefabActive(dailyDungeonDialogRoot, "GoToBtn", unlocked);
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
                Notify("已有未完成路线，请先继续或离开该路线");
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
            BindPrefabButton(dailyDungeonDialogRoot, "CloseBtn", ShowDailyDungeonLeaveConfirm);
            RenderDailyDungeonRoute();
        }

        private void RenderDailyDungeonRoute()
        {
            var run = host.DailyDungeons.ActiveRun;
            if (run == null || dailyDungeonDialogRoot == null) return;
            var map = host.DailyDungeons.FindMap(run.mapGroupId);
            if (map == null) throw new InvalidOperationException("Daily dungeon route map configuration is missing.");
            Transform grid = Find(dailyDungeonDialogRoot.transform, "RoomGrid");
            if (grid == null) return;
            foreach (Transform child in grid) UnityEngine.Object.Destroy(child.gameObject);
            GameObject roomPrefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonRoomItem");
            if (roomPrefab == null) throw new InvalidOperationException("Official daily dungeon room item is missing.");
            for (int index = 0; index < map.roomArray.Length; index++)
            {
                int roomId = map.roomArray[index];
                if (roomId <= 0) continue;
                var room = host.DailyDungeons.FindRoom(roomId);
                if (room == null || room.type == 0) continue;
                bool visible = run.clearedRoomIds.Contains(roomId) || roomId == run.currentRoomId
                    || run.clearedRoomIds.Select(host.DailyDungeons.FindRoom)
                        .Any(previous => previous != null
                            && (previous.nextRoomIds ?? Array.Empty<int>()).Contains(roomId));
                if (!visible) continue;
                GameObject item = UnityEngine.Object.Instantiate(roomPrefab, grid, false);
                item.name = "Room_" + roomId;
                SetPrefabActive(item, "SelectImg", roomId == run.currentRoomId);
                SetPrefabActive(item, "RewardIconImg", room.surpriseRewards != null
                    && room.surpriseRewards.Any(value => value.difficulty == run.difficulty
                        && value.rewardRuleIds != null && value.rewardRuleIds.Length > 0));
                Button select = item.GetComponent<Button>();
                if (select == null) select = item.AddComponent<Button>();
                int selectedRoom = roomId;
                select.onClick.AddListener(() => SelectDailyDungeonRoom(selectedRoom));
            }
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
                    if (grade > 0 && host.DailyDungeons.Finish(grade, GrantDailyDungeonRoomRewards))
                    {
                        CloseDungeonDetail();
                        RefreshDailyDungeonEntryPage();
                        Notify("路线完成，评级 " + grade);
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
                if (grade > 0 && host.DailyDungeons.Finish(grade, GrantDailyDungeonRoomRewards))
                {
                    CloseDungeonDetail();
                    RefreshDailyDungeonEntryPage();
                    Notify("路线完成，评级 " + grade);
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
            Notify("已离开路线，已结算清理房间的配置掉落；挑战次数不返还");
        }

        private void GrantDailyDungeonRoomRewards(string receipt, Zjcs.Offline.World.DailyDungeonRunSave run)
        {
            int[] rules = host.DailyDungeons.ConfiguredRewardRuleIds(run, run.grade > 0);
            if (!host.GrantDailyDungeonRewards(receipt, run, rules))
                throw new InvalidOperationException("Official daily dungeon reward settlement failed.");
        }

        private void CloseDailyDungeonRoute()
        {
            CloseDungeonDetail();
            if (host.DailyDungeons.ActiveRun != null)
                Notify("路线已保存，可从副本列表继续");
            else
                RefreshDailyDungeonEntryPage();
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
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "TitleText", "离开副本路线");
            SetNamedText(dailyDungeonLeaveConfirmRoot.transform, "Txt", "离开后将结算已清理房间的配置掉落，挑战次数不返还");
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

        private string BestGradeText(int dungeonId)
        {
            var dungeon = host.DailyDungeons.FindDungeon(dungeonId);
            int grade = (dungeon?.difficulties ?? Array.Empty<Zjcs.Offline.World.DailyDungeonRuntimeDifficulty>())
                .Where(value => value.difficulty != 50)
                .Select(value => host.DailyDungeons.BestGrade(dungeonId, value.difficulty))
                .DefaultIfEmpty(0).Max();
            return grade <= 0 ? "暂无" : grade.ToString();
        }

        private static void SetNamedText(Transform root, string name, string value)
        {
            Transform node = Find(root, name);
            TMPro.TMP_Text text = node == null ? null : node.GetComponent<TMPro.TMP_Text>();
            if (text == null && node != null) text = node.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null) text.text = value ?? string.Empty;
        }

        private static void SetPrefabActive(GameObject root, string name, bool active)
        {
            Transform node = Find(root == null ? null : root.transform, name);
            if (node != null) node.gameObject.SetActive(active);
        }

        private void CloseDungeonDetail()
        {
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
            if (button != null) button.onClick.AddListener(() => action());
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
