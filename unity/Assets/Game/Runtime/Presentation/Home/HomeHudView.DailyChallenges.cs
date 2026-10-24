using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private static readonly int[] DailyChallengeActivityIds = { 209, 212, 226, 217 };
        private int dailyChallengeIndex;
        private GameObject dailyChallengeSelectionRoot;
        private GameObject dailyChallengeResultRoot;
        private GameObject dailyChallengeAssistRoot;
        private int dailyChallengeAssistActivityId;
        private OfficialDailyChallengeCatalog dailyChallengeAssistCatalog;
        private readonly System.Collections.Generic.Dictionary<int, int> dailyChallengeDifficulties =
            new System.Collections.Generic.Dictionary<int, int>();

        private void OpenDailyChallenges()
        {
            host.DailyChallenges.EnterFeature(DateTime.Now);
            OpenDailyChallengeChooser();
        }

        private void OpenDailyChallengeChooser()
        {
            if (dailyChallengeSelectionRoot != null) UnityEngine.Object.Destroy(dailyChallengeSelectionRoot);
            Transform source = Find(travelNotesRoot == null ? null : travelNotesRoot.transform, "DailyActivityEntry");
            if (source == null) throw new InvalidOperationException("Official travel notes daily activity entry is missing.");
            dailyChallengeSelectionRoot = new GameObject("DailyChallengeEntries", typeof(RectTransform),
                typeof(Image), typeof(Button));
            dailyChallengeSelectionRoot.transform.SetParent(canvas.transform, false);
            dailyChallengeSelectionRoot.transform.SetAsLastSibling();
            RectTransform selectionRect = (RectTransform)dailyChallengeSelectionRoot.transform;
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.pivot = new Vector2(0.5f, 0.5f);
            selectionRect.offsetMin = selectionRect.offsetMax = Vector2.zero;
            Image backdrop = dailyChallengeSelectionRoot.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;
            Button backdropButton = dailyChallengeSelectionRoot.GetComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.onClick.AddListener(CloseDailyChallengeChooser);
            string[] labels = { "圣兽试炼 1", "圣兽试炼 2", "圣兽试炼 3", "双影幻境" };
            for (int index = 0; index < DailyChallengeActivityIds.Length; index++)
            {
                int activityIndex = index;
                GameObject entry = UnityEngine.Object.Instantiate(source.gameObject,
                    dailyChallengeSelectionRoot.transform, false);
                entry.name = "DailyChallengeEntry_" + DailyChallengeActivityIds[index];
                RectTransform rect = (RectTransform)entry.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(index % 2 == 0 ? -230f : 230f,
                    index < 2 ? 280f : -280f);
                TextMeshProUGUI label = CreateLabel(entry.transform, "DailyChallengeTitle", 28f,
                    Vector2.zero, TextAlignmentOptions.Center);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -24f);
                labelRect.sizeDelta = new Vector2(390f, 60f);
                label.text = labels[index];
                BindPrefabButton(entry, "EntryBtn", () => SelectDailyChallengeActivity(activityIndex));
            }
        }

        private void CloseDailyChallengeChooser()
        {
            if (dailyChallengeSelectionRoot != null) UnityEngine.Object.Destroy(dailyChallengeSelectionRoot);
            dailyChallengeSelectionRoot = null;
        }

        private void SelectDailyChallengeActivity(int index)
        {
            dailyChallengeIndex = Mathf.Clamp(index, 0, DailyChallengeActivityIds.Length - 1);
            if (dailyChallengeSelectionRoot != null) UnityEngine.Object.Destroy(dailyChallengeSelectionRoot);
            dailyChallengeSelectionRoot = null;
            if (travelNotesRoot != null) UnityEngine.Object.Destroy(travelNotesRoot);
            travelNotesRoot = null;
            OpenDailyChallengePage();
        }

        private void OpenDailyChallengePage()
        {
            if (dailyChallengeRoot != null) UnityEngine.Object.Destroy(dailyChallengeRoot);
            int activityId = DailyChallengeActivityIds[Mathf.Clamp(dailyChallengeIndex, 0,
                DailyChallengeActivityIds.Length - 1)];
            bool funnyBoss = activityId == 217;
            string resource = funnyBoss
                ? "DailyChallenges/AnniversaryFunnyBossEntryPanel"
                : "DailyChallenges/ActivityDomainBossPanel";
            GameObject prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) throw new InvalidOperationException("Official daily challenge prefab is missing: " + resource);
            dailyChallengeRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyChallengeRoot.name = funnyBoss ? "AnniversaryFunnyBossEntryPanel" : "ActivityDomainBossPanel";
            dailyChallengeRoot.transform.SetAsLastSibling();

            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            int duration = catalog.DurationHours(activityId);
            int dailyLimit = catalog.DailyLimit(activityId);
            host.DailyChallenges.CanParticipate(activityId, DateTime.Now,
                duration, dailyLimit, out _);
            if (funnyBoss)
            {
                long damage = host.DailyChallenges.GetDoubleShadowDamage(DateTime.Now, duration);
                SetNamedText(dailyChallengeRoot.transform, "ScoreText",
                    OfficialBattleSceneView.FormatBattleNumber(damage));
                SetNamedText(dailyChallengeRoot.transform, "ChestNumText",
                    host.DailyChallenges.DoubleShadowChestCount(duration, DateTime.Now).ToString());
                BindPrefabButton(dailyChallengeRoot, "ChestGetClickMask", ClaimDoubleShadowChest);
                Transform stepSlider = Find(dailyChallengeRoot.transform, "AwardContent/AnniversaryFunnyBossStepAwardSlider");
                if (stepSlider != null)
                    foreach (Button button in stepSlider.GetComponentsInChildren<Button>(true))
                        button.onClick.AddListener(ClaimDoubleShadowSteps);
            }

            BindPrefabButton(dailyChallengeRoot, "GoToBtn", () => OpenDailyChallengeAssist(activityId, catalog));
            BindPrefabButton(dailyChallengeRoot, "CloseBtn_dark", CloseDailyChallengePage);
            BindPrefabButton(dailyChallengeRoot, "CloseBtn", CloseDailyChallengePage);
            if (!funnyBoss)
            {
                var difficulties = catalog.Difficulties(activityId);
                int maxUnlocked = Mathf.Clamp(host.DailyChallenges.GetHighestDifficulty(activityId),
                    0, Math.Max(0, difficulties.Count - 1));
                int selected = dailyChallengeDifficulties.TryGetValue(activityId, out int remembered)
                    ? remembered : maxUnlocked;
                selected = Mathf.Clamp(selected, 0, maxUnlocked);
                dailyChallengeDifficulties[activityId] = selected;
                PopulateDifficultySelector(activityId, difficulties, selected, maxUnlocked);
                BindPrefabButton(dailyChallengeRoot, "ReceiveKillAwardBtn",
                    () => ClaimBossDifficultyAward(activityId, selected));
                BindPrefabButton(dailyChallengeRoot, "LeftBtn", () => SelectDifficulty(activityId, -1));
                BindPrefabButton(dailyChallengeRoot, "RightBtn", () => SelectDifficulty(activityId, 1));
            }
            else BindPrefabButton(dailyChallengeRoot, "CloseBtn2", CloseDailyChallengePage);
        }

        private void SelectDailyChallenge(int offset)
        {
            dailyChallengeIndex = (dailyChallengeIndex + offset + DailyChallengeActivityIds.Length)
                % DailyChallengeActivityIds.Length;
            OpenDailyChallengePage();
        }

        private void SelectDifficulty(int activityId, int offset)
        {
            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            var difficulties = catalog.Difficulties(activityId);
            int maxUnlocked = Mathf.Clamp(host.DailyChallenges.GetHighestDifficulty(activityId),
                0, Math.Max(0, difficulties.Count - 1));
            int selected = dailyChallengeDifficulties.TryGetValue(activityId, out int current)
                ? current : maxUnlocked;
            dailyChallengeDifficulties[activityId] = Mathf.Clamp(selected + offset, 0, maxUnlocked);
            OpenDailyChallengePage();
        }

        private void PopulateDifficultySelector(int activityId,
            System.Collections.Generic.IReadOnlyList<OfficialDomainBossDifficulty> difficulties,
            int selected, int maxUnlocked)
        {
            Transform content = Find(dailyChallengeRoot.transform, "NumScrollView/CellContent");
            GameObject cellPrefab = Resources.Load<GameObject>("DailyChallenges/ActivityDomainBossNumCell");
            if (content == null || cellPrefab == null) return;
            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            foreach (OfficialDomainBossDifficulty difficulty in difficulties)
            {
                GameObject cell = UnityEngine.Object.Instantiate(cellPrefab, content, false);
                SetNamedText(cell.transform, "NumText", (difficulty.Difficulty + 1).ToString());
                SetPrefabActive(cell, "LockImg", difficulty.Difficulty > maxUnlocked);
                SetPrefabActive(cell, "EffectRoot", difficulty.Difficulty == selected);
                Button button = cell.GetComponent<Button>() ?? cell.GetComponentInChildren<Button>(true);
                if (button == null) continue;
                bool unlocked = difficulty.Difficulty <= maxUnlocked;
                button.interactable = unlocked;
                int target = difficulty.Difficulty;
                button.onClick.AddListener(() =>
                {
                    if (!unlocked) return;
                    dailyChallengeDifficulties[activityId] = target;
                    OpenDailyChallengePage();
                });
            }
        }

        private void OpenDailyChallengeAssist(int activityId, OfficialDailyChallengeCatalog challengeCatalog)
        {
            if (!string.IsNullOrEmpty(host.DailyChallenges.ActiveBattleCheckpoint(activityId)))
            {
                StartDailyChallenge(activityId, challengeCatalog);
                return;
            }
            dailyChallengeAssistActivityId = activityId;
            dailyChallengeAssistCatalog = challengeCatalog;
            if (dailyChallengeAssistRoot != null) UnityEngine.Object.Destroy(dailyChallengeAssistRoot);
            dailyChallengeAssistRoot = new GameObject("DailyChallengeAssistSelection", typeof(RectTransform),
                typeof(Image), typeof(Button));
            dailyChallengeAssistRoot.transform.SetParent(canvas.transform, false);
            dailyChallengeAssistRoot.transform.SetAsLastSibling();
            RectTransform rootRect = (RectTransform)dailyChallengeAssistRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            Image backdrop = dailyChallengeAssistRoot.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.76f);
            backdrop.raycastTarget = true;
            Button backdropButton = dailyChallengeAssistRoot.GetComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.onClick.AddListener(CloseDailyChallengeAssist);

            GameObject panel = new GameObject("AssistPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(dailyChallengeAssistRoot.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 760f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.19f, 0.98f);

            TextMeshProUGUI title = CreateAssistLabel(panel.transform, "Title", 30f,
                new Vector2(0f, 330f), new Vector2(660f, 54f), TextAlignmentOptions.Center);
            title.text = "选择助战伙伴";
            TextMeshProUGUI count = CreateAssistLabel(panel.transform, "SelectedCount", 18f,
                new Vector2(0f, 290f), new Vector2(660f, 34f), TextAlignmentOptions.Center);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image),
                typeof(Mask), typeof(ScrollRect));
            viewportObject.transform.SetParent(panel.transform, false);
            RectTransform viewportRect = (RectTransform)viewportObject.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = new Vector2(0f, 10f);
            viewportRect.sizeDelta = new Vector2(650f, 560f);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0.04f, 0.06f, 0.07f, 0.68f);
            viewportImage.raycastTarget = true;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;
            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;

            IReadOnlyList<int> candidates = host.NpcAssistanceCatalog.FightAssistNpcClassIds();
            var friendships = (host.Profile.Save.Player.CharacterProperties.NpcFriendships
                    ?? new System.Collections.Generic.List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            FightOfficialNpcInviteRule invite = host.NpcAssistanceCatalog.InviteRule(10);
            OfficialBattleModelCatalog battleModels = OfficialBattleModelCatalog.LoadDefault();
            NpcAssistRefreshCycle cycle = (NpcAssistRefreshCycle)invite.RefreshCycle;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            int maxPlayer = Math.Min(3, invite.MaxPlayer);
            int[] existing = host.NpcAssistanceProgress.Selected(10, "daily-challenge:" + activityId).ToArray();
            var validSelection = new List<int>();
            int selectedCount = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                int npcId = candidates[index];
                bool met = host.NpcAssistanceCatalog.AssistConditionMet(npcId,
                    host.Profile.Save.Player.Level, friendships, host.Profile.StoryCount);
                bool encountered = friendships.ContainsKey(npcId);
                bool modelAvailable = battleModels.HasNpcBattleAnimations(npcId);
                bool available = modelAvailable
                    && host.NpcAssistanceCatalog.CanInviteNpc(npcId, encountered, met)
                    && (invite.OneNpcMaxInviteCount <= 0
                        || host.NpcAssistanceProgress.AssistCount(10, npcId, cycle, now)
                            < invite.OneNpcMaxInviteCount);
                bool selected = existing.Contains(npcId) && available && selectedCount < maxPlayer;
                if (selected)
                {
                    selectedCount++;
                    validSelection.Add(npcId);
                }
                CreateDailyChallengeAssistRow(contentRect, npcId, index, available, selected,
                    encountered, met, modelAvailable, maxPlayer);
            }
            if (!existing.SequenceEqual(validSelection))
                host.NpcAssistanceProgress.SetSelection(10, "daily-challenge:" + activityId,
                    validSelection, maxPlayer);
            contentRect.sizeDelta = new Vector2(0f, candidates.Count * 82f);

            Button start = CreateAssistButton(panel.transform, "Start", "开始挑战", new Vector2(-115f, 42f));
            start.onClick.AddListener(() => StartDailyChallengeWithAssist(false));
            Button solo = CreateAssistButton(panel.transform, "Solo", "单人挑战", new Vector2(115f, 42f));
            solo.onClick.AddListener(() => StartDailyChallengeWithAssist(true));
            Button close = CreateAssistButton(panel.transform, "Close", "返回", new Vector2(0f, 88f));
            close.onClick.AddListener(CloseDailyChallengeAssist);
            RefreshDailyChallengeAssistCount(count, maxPlayer);
        }

        private void CreateDailyChallengeAssistRow(RectTransform content, int npcId, int index,
            bool available, bool selected, bool encountered, bool conditionMet,
            bool modelAvailable, int maxPlayer)
        {
            GameObject row = new GameObject("Npc_" + npcId, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(content, false);
            RectTransform rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 82f);
            rect.sizeDelta = new Vector2(-8f, 76f);
            Image image = row.GetComponent<Image>();
            image.color = selected ? new Color(0.22f, 0.34f, 0.31f, 0.98f)
                : new Color(0.13f, 0.19f, 0.22f, 0.96f);
            Button button = row.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = available || selected;

            GameObject portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(row.transform, false);
            RectTransform portraitRect = (RectTransform)portrait.transform;
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(12f, 0f);
            portraitRect.sizeDelta = new Vector2(58f, 58f);
            NpcTravelNpc npc = host.NpcTravelCatalog.Npc(npcId);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = catalog.FindDynamicSprite(NpcTravelIconKey(npc));
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            TextMeshProUGUI name = CreateAssistLabel(row.transform, "Name", 19f,
                new Vector2(92f, 14f), new Vector2(390f, 28f), TextAlignmentOptions.Left);
            name.text = host.Catalog.LocalizedText(host.NpcAssistanceCatalog.NpcNameKey(npcId),
                "伙伴 " + npcId);
            TextMeshProUGUI state = CreateAssistLabel(row.transform, "State", 15f,
                new Vector2(92f, -16f), new Vector2(440f, 26f), TextAlignmentOptions.Left);
            state.text = !modelAvailable ? "本地战斗动画未安装"
                : !encountered && host.NpcAssistanceCatalog.RequiresEncounter(npcId)
                ? "尚未相识" : !conditionMet ? "未满足助战条件"
                : !available ? "今日助战次数已用完" : selected ? "已选中" : "可助战";
            if (!available) state.color = new Color(0.68f, 0.70f, 0.71f, 1f);
            button.onClick.AddListener(() =>
            {
                int[] current = host.NpcAssistanceProgress.Selected(10,
                    "daily-challenge:" + dailyChallengeAssistActivityId).ToArray();
                var next = current.ToList();
                if (next.Contains(npcId)) next.Remove(npcId);
                else if (available && next.Count < maxPlayer) next.Add(npcId);
                else if (next.Count >= maxPlayer) Notify("最多选择 " + maxPlayer + " 名助战伙伴");
                else return;
                host.NpcAssistanceProgress.SetSelection(10,
                    "daily-challenge:" + dailyChallengeAssistActivityId, next, maxPlayer);
                RebuildDailyChallengeAssistSelection();
            });
        }

        private void RebuildDailyChallengeAssistSelection()
        {
            int activityId = dailyChallengeAssistActivityId;
            OfficialDailyChallengeCatalog challengeCatalog = dailyChallengeAssistCatalog;
            OpenDailyChallengeAssist(activityId, challengeCatalog);
        }

        private void RefreshDailyChallengeAssistCount(TextMeshProUGUI label, int maxPlayer)
        {
            int count = host.NpcAssistanceProgress.Selected(10,
                "daily-challenge:" + dailyChallengeAssistActivityId).Count;
            label.text = "助战伙伴 " + count + " / " + maxPlayer;
        }

        private TextMeshProUGUI CreateAssistLabel(Transform parent, string name, float size,
            Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateLabel(parent, name, size, Vector2.zero, alignment);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateAssistButton(Transform parent, string name, string text, Vector2 position)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(190f, 42f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.40f, 0.61f, 0.50f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateAssistLabel(buttonObject.transform, "Text", 18f,
                Vector2.zero, new Vector2(180f, 38f), TextAlignmentOptions.Center);
            label.text = text;
            label.color = Color.white;
            return button;
        }

        private void StartDailyChallengeWithAssist(bool solo)
        {
            if (solo)
                host.NpcAssistanceProgress.SetSelection(10,
                    "daily-challenge:" + dailyChallengeAssistActivityId, Array.Empty<int>(), 3);
            int activityId = dailyChallengeAssistActivityId;
            OfficialDailyChallengeCatalog challengeCatalog = dailyChallengeAssistCatalog;
            CloseDailyChallengeAssist();
            StartDailyChallenge(activityId, challengeCatalog);
        }

        private void CloseDailyChallengeAssist()
        {
            if (dailyChallengeAssistRoot != null) UnityEngine.Object.Destroy(dailyChallengeAssistRoot);
            dailyChallengeAssistRoot = null;
        }

        private void StartDailyChallenge(int activityId, OfficialDailyChallengeCatalog catalog)
        {
            if (activeDungeonBattle != null) return;
            bool resume = !string.IsNullOrEmpty(host.DailyChallenges.ActiveBattleCheckpoint(activityId));
            if (!resume && !host.DailyChallenges.CanParticipate(activityId, DateTime.Now,
                    catalog.DurationHours(activityId), catalog.DailyLimit(activityId), out string reason))
            {
                Notify(reason);
                return;
            }
            int difficulty = resume ? host.DailyChallenges.ActiveChallengeDifficulty(activityId)
                : dailyChallengeDifficulties.TryGetValue(activityId, out int selectedDifficulty)
                    ? selectedDifficulty : 0;
            if (dailyChallengeRoot != null) UnityEngine.Object.Destroy(dailyChallengeRoot);
            dailyChallengeRoot = null;
            var battleObject = new GameObject("DailyChallengeBattleRunner");
            try
            {
                activeDungeonBattle = battleObject.AddComponent<DailyDungeonBattleRunner>();
                activeDungeonBattle.BeginChallenge(host, activityId, catalog.StageId(activityId),
                    catalog.StageType(activityId), catalog.DurationHours(activityId),
                    catalog.DailyLimit(activityId), difficulty, OnDailyChallengeBattleFinished);
            }
            catch (Exception error)
            {
                if (battleObject != null) UnityEngine.Object.Destroy(battleObject);
                activeDungeonBattle = null;
                Notify("挑战未能进入战斗：" + error.Message);
            }
        }

        private void OnDailyChallengeBattleFinished(bool won)
        {
            activeDungeonBattle = null;
        }

        public void OpenDailyChallengeResult(int activityId, bool won, FightBattleCheckpoint checkpoint,
            System.Collections.Generic.IReadOnlyList<OfficialChallengeReward> rewards = null)
        {
            if (dailyChallengeResultRoot != null) UnityEngine.Object.Destroy(dailyChallengeResultRoot);
            string resource = activityId == OfflineDailyChallengeService.FunnyBossActivityId
                ? "DailyChallenges/GameEndPanelActivityFunnyBoss"
                : "DailyChallenges/GameEndPanelActivityDomainBoss";
            GameObject prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) throw new InvalidOperationException("Official daily challenge result prefab is missing: " + resource);
            dailyChallengeResultRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            dailyChallengeResultRoot.name = "DailyChallengeResult";
            dailyChallengeResultRoot.transform.SetAsLastSibling();
            SetPrefabActive(dailyChallengeResultRoot, "WinObj", won);
            SetPrefabActive(dailyChallengeResultRoot, "ShareBtn", false);
            SetPrefabActive(dailyChallengeResultRoot, "SaveRecordBtn", false);
            SetPrefabActive(dailyChallengeResultRoot, "ThumbUpReportSwitchBtn", false);
            SetPrefabActive(dailyChallengeResultRoot, "BtnBattleData", false);
            SetNamedText(dailyChallengeResultRoot.transform, "NameTxt", activityId switch
            {
                209 => "圣兽试炼",
                212 => "圣兽试炼",
                226 => "圣兽试炼",
                OfflineDailyChallengeService.FunnyBossActivityId => "双影幻境",
                _ => "挑战"
            });
            SetNamedText(dailyChallengeResultRoot.transform, "EndTxt", won ? "挑战完成" : "挑战失败");
            SetNamedText(dailyChallengeResultRoot.transform, "ReturnText", "返回挑战");
            PopulateDailyChallengeResultRewards(rewards);
            long totalDamage = 0;
            long selfDamage = 0;
            foreach (FightBattleUnitStatistics statistic in checkpoint?.Statistics?.Units
                         ?? new System.Collections.Generic.List<FightBattleUnitStatistics>())
            {
                if (checkpoint == null) break;
                FightBattleUnitState unit = checkpoint.Units.FirstOrDefault(value => value.Id == statistic.UnitId);
                if (unit == null || (unit.Id != 1 && unit.MasterId != 1)) continue;
                long dealt = statistic.DamageAndShieldDamageDealt;
                totalDamage = checked(totalDamage + dealt);
                if (unit.Id == 1) selfDamage = checked(selfDamage + dealt);
            }
            string damageText = OfficialBattleSceneView.FormatBattleNumber(totalDamage);
            SetNamedText(dailyChallengeResultRoot.transform, "DamageTxt", damageText);
            foreach (Transform node in dailyChallengeResultRoot.GetComponentsInChildren<Transform>(true))
                if (node.name == "DamageValueText")
                    SetNamedText(node, node.name, node.parent != null && node.parent.name == "FunnyBossDamageItemSelf"
                        ? OfficialBattleSceneView.FormatBattleNumber(selfDamage) : damageText);
            Transform returnText = Find(dailyChallengeResultRoot.transform, "ReturnText");
            Button returnButton = returnText == null ? null : returnText.GetComponentInParent<Button>();
            if (returnButton == null)
                returnButton = Find(dailyChallengeResultRoot.transform, "LevelBtn")?.GetComponent<Button>();
            if (returnButton != null) returnButton.onClick.AddListener(CloseDailyChallengeResult);
        }

        private void PopulateDailyChallengeResultRewards(
            System.Collections.Generic.IReadOnlyList<OfficialChallengeReward> rewards)
        {
            Transform content = Find(dailyChallengeResultRoot.transform, "GameEndRewardContent");
            if (content == null) return;
            Transform title = Find(content, "Title");
            foreach (Transform child in content)
                if (child != title) UnityEngine.Object.Destroy(child.gameObject);
            bool hasRewards = rewards != null && rewards.Any(value => value != null
                && value.ItemId > 0 && value.Count > 0);
            content.gameObject.SetActive(hasRewards);
            if (!hasRewards) return;

            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var layout = content.gameObject.GetComponent<HorizontalLayoutGroup>()
                ?? content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (OfficialChallengeReward reward in rewards.Where(value => value != null
                         && value.ItemId > 0 && value.Count > 0))
            {
                var item = new GameObject("DailyChallengeReward_" + reward.ItemId,
                    typeof(RectTransform));
                item.transform.SetParent(content, false);
                RectTransform itemRect = (RectTransform)item.transform;
                itemRect.sizeDelta = new Vector2(120f, 116f);
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(item.transform, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, -4f);
                iconRect.sizeDelta = new Vector2(68f, 68f);
                Image icon = iconObject.GetComponent<Image>();
                icon.sprite = host.ItemIcon(reward.ItemId);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                TextMeshProUGUI count = CreateLabel(iconObject.transform, "RewardCount", 18f,
                    Vector2.zero, TextAlignmentOptions.BottomRight);
                RectTransform countRect = count.rectTransform;
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = new Vector2(2f, 2f);
                countRect.offsetMax = new Vector2(-4f, -4f);
                count.text = "×" + reward.Count;
                count.color = Color.white;
                count.raycastTarget = false;
                TextMeshProUGUI name = CreateLabel(item.transform, "RewardName", 16f,
                    Vector2.zero, TextAlignmentOptions.Top);
                RectTransform nameRect = name.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0f);
                nameRect.pivot = new Vector2(0.5f, 0f);
                nameRect.anchoredPosition = new Vector2(0f, 4f);
                nameRect.sizeDelta = new Vector2(0f, 40f);
                name.text = host.ItemName(reward.ItemId);
                name.color = Color.white;
                name.enableWordWrapping = true;
                name.raycastTarget = false;
            }
        }

        private void CloseDailyChallengeResult()
        {
            if (dailyChallengeResultRoot != null) UnityEngine.Object.Destroy(dailyChallengeResultRoot);
            dailyChallengeResultRoot = null;
            OpenDailyChallengePage();
        }

        private void CloseDailyChallengePage()
        {
            if (dailyChallengeRoot != null) UnityEngine.Object.Destroy(dailyChallengeRoot);
            dailyChallengeRoot = null;
            OpenTravelNotes();
        }

        private void ClaimBossDifficultyAward(int activityId, int difficulty)
        {
            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            ProgressionResult result = host.DailyChallenges.ClaimBossDifficultyAward(activityId, difficulty,
                catalog, host.Profile, DateTime.Now, catalog.DurationHours(activityId));
            Notify(result.Status == ProgressionResultStatus.Applied ? "阶位奖励已领取"
                : string.IsNullOrEmpty(result.Message) ? "当前没有可领取的阶位奖励" : result.Message);
            OpenDailyChallengePage();
        }

        private void ClaimDoubleShadowSteps()
        {
            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            DateTime now = DateTime.Now;
            int duration = catalog.DurationHours(OfflineDailyChallengeService.FunnyBossActivityId);
            int score = host.DailyChallenges.GetDoubleShadowScore(now, duration);
            int[] configuredScores = catalog.DoubleShadowStepAwards.Select(row => row.Score).ToArray();
            string transactionId = "daily-challenge:steps:" + host.DailyChallenges.GetLogicDayStartLocal(now).Ticks
                + ":" + score;
            ProgressionResult result = host.DailyChallenges.ClaimDoubleShadowSteps(configuredScores,
                transactionId, host.Profile, (save, claimedScores) =>
                {
                    foreach (OfficialChallengeReward reward in catalog.ResolveDoubleShadowStepRewards(
                                 claimedScores, StableDailyChallengeSeed(transactionId)))
                    {
                        InventoryOperationResult grant = OfflineInventoryOperations.Grant(save.Player,
                            reward.ItemId, reward.Count, new DateTimeOffset(now).ToUnixTimeSeconds());
                        if (grant.Status != InventoryOperationStatus.Applied)
                            throw new InvalidOperationException("阶段奖励发放失败：" + reward.ItemId);
                    }
                }, now, duration);
            Notify(result.Status == ProgressionResultStatus.Applied ? "阶段奖励已领取"
                : string.IsNullOrEmpty(result.Message) ? "当前没有可领取的阶段奖励" : result.Message);
            OpenDailyChallengePage();
        }

        private void ClaimDoubleShadowChest()
        {
            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            ProgressionResult result = host.DailyChallenges.ClaimDoubleShadowChest(catalog, host.Profile,
                DateTime.Now, catalog.DurationHours(OfflineDailyChallengeService.FunnyBossActivityId));
            Notify(result.Status == ProgressionResultStatus.Applied ? "宝箱奖励已领取"
                : string.IsNullOrEmpty(result.Message) ? "当前没有可开启的宝箱" : result.Message);
            OpenDailyChallengePage();
        }

        private static int StableDailyChallengeSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
    }
}
