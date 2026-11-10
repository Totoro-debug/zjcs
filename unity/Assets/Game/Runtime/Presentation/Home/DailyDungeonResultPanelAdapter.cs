using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultPanelAdapter : MonoBehaviour, LoopScrollDataSource
    {
        [Serializable]
        private sealed class GradeSpriteCatalog
        {
            public GradeSpriteEntry[] gradeSprites;
        }

        [Serializable]
        private sealed class GradeSpriteEntry
        {
            public int gradeType;
            public string resourcePath;
        }

        private sealed class RewardDisplayRow
        {
            public int ItemId;
            public long Count;
        }

        private static Dictionary<int, string> gradeSpritePaths;
        private HomeFeatureRuntime host;
        private DailyDungeonUiBindingModule uiBindings;
        private DailyDungeonSettlementHudModel model;
        private Action claim;
        private Action purchase;
        private Action close;
        private bool canClaim;
        private bool canPurchase;
        private Transform rewardGrid;
        private Zjcs.Official.BattleResultUI.AwardLoopScrollRect rewardScroll;
        private readonly List<RewardDisplayRow> rewardRows = new List<RewardDisplayRow>();
        private readonly Queue<GameObject> waitingRewards = new Queue<GameObject>();
        private Coroutine rewardRoutine;
        private DungeonOfficialAwardContentBinding awardTiming;
        private Zjcs.Official.WorldUI.WorldWindowBlurMask mask;
        private PartnerWindowBlurCapture blur;
        private DailyDungeonResultPlayerRowAdapter playerRow;

        public static DailyDungeonResultPanelAdapter Create(GameObject prefab, Transform parent,
            DailyDungeonUiBindingModule bindings)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            var staging = new GameObject("DailyDungeonResultStaging", typeof(RectTransform));
            staging.SetActive(false);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            instance.SetActive(false);
            foreach (MonoBehaviour component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                if (typeName == "GameEndPanelBase" || typeName == "StoryNodeRegister")
                    component.enabled = false;
            }
            DailyDungeonResultPanelAdapter adapter = instance.GetComponent<DailyDungeonResultPanelAdapter>();
            if (adapter == null)
            {
                UnityEngine.Object.Destroy(staging);
                UnityEngine.Object.Destroy(instance);
                throw new InvalidOperationException("Official daily dungeon result adapter binding is missing.");
            }
            instance.transform.SetParent(parent, false);
            adapter.uiBindings = bindings;
            bindings.Apply(instance, "GameEndPanelDailyDungeon");
            UnityEngine.Object.Destroy(staging);
            return adapter;
        }

        public void Configure(DailyDungeonSettlementHudModel settlement, HomeFeatureRuntime runtime,
            bool allowClaim, bool allowPurchase, Action claimAction, Action purchaseAction, Action closeAction)
        {
            model = settlement ?? throw new ArgumentNullException(nameof(settlement));
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            canClaim = allowClaim && settlement.CanClaim;
            canPurchase = allowPurchase && settlement.CanPurchaseEntries;
            claim = claimAction;
            purchase = purchaseAction;
            close = closeAction;

            BindOfficialView();
            ShowPreviewRewards();
            BindButtons();
            host.StartCoroutine(ShowWindow());
        }

        private IEnumerator ShowWindow()
        {
            var assets = Zjcs.Offline.PartnersUI.OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            blur = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
            blur.Initialize(assets.BlurMaterial);
            var staging = new GameObject("Daily dungeon original blur mask staging");
            staging.SetActive(false);
            GameObject root = UnityEngine.Object.Instantiate(assets.BlurMaskPrefab, staging.transform, false);
            root.SetActive(false);
            root.transform.SetParent(transform.parent, false);
            UnityEngine.Object.Destroy(staging);
            mask = root.GetComponent<Zjcs.Official.WorldUI.WorldWindowBlurMask>();
            mask.BlurManager = blur;
            var popup = GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
            yield return mask.Prepare(popup, false);
            if (!this) yield break;
            int sortingLayer = popup.Canvas.sortingLayerID;
            int sortingOrder = popup.Canvas.sortingOrder;
            gameObject.SetActive(true);
            popup.Show();
            popup.Canvas.sortingLayerID = sortingLayer;
            popup.SetCanvasSortingOrder(sortingOrder);
            mask.Canvas.sortingLayerID = sortingLayer;
            mask.AttachToWindow(popup);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        private void OnDestroy()
        {
            if (rewardRoutine != null && host) host.StopCoroutine(rewardRoutine);
            if (mask)
            {
                mask.UnattachToWindow();
                UnityEngine.Object.Destroy(mask.gameObject);
            }
            if (blur) UnityEngine.Object.Destroy(blur);
        }

        public void ShowClaimedRewards(IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards)
        {
            DrawRewards((rewards ?? Array.Empty<DailyDungeonClaimRewardDisplayItem>())
                .Where(value => value != null && !value.IsGradeReward && value.ItemId > 0 && value.Count > 0)
                .Select(value => new RewardDisplayRow { ItemId = value.ItemId, Count = value.Count }));
            playerRow.OpenRewards((rewards ?? Array.Empty<DailyDungeonClaimRewardDisplayItem>())
                .Where(value => value != null && value.IsGradeReward && value.ItemId > 0 && value.Count > 0).ToArray());
            canClaim = false;
            canPurchase = false;
            uiBindings.ApplyResultClaimState(gameObject, 0, false);
            RefreshClaimCount();
            SetNamedText(transform,
                "Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem", "已领取");
            SetButtonInteractable("Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem", false);
            SetButtonInteractable("Content/WinObj/Content/DailyDungeonCostMoneyContent/ShowCountTipGo/ShowCountTip/CommonBtn_Add", false);
            SetNamedText(transform, "ReturnText", "奖励已发放至背包");
        }

        private void BindOfficialView()
        {
            RefreshClaimCount();
            SetNamedText(transform, "Content/WinObj/DifficultyTitleGo/TitleText",
                uiBindings.DifficultyTitle(model.Run.dungeonId, model.Run.difficulty));

            Transform gradeNode = FindPath(transform, "Content/WinObj/GoTitleBg/GradeImg");
            Image gradeImage = gradeNode == null ? null : gradeNode.GetComponent<Image>();
            if (gradeImage != null)
            {
                gradeImage.sprite = LoadGradeSprite(model.Run.grade);
                gradeImage.enabled = gradeImage.sprite != null;
                Image additiveGrade = gradeNode.Find("GradeImgAdd")?.GetComponent<Image>();
                if (additiveGrade != null) additiveGrade.sprite = gradeImage.sprite;
            }

            Transform playerSlot = FindPath(transform, "Content/WinObj/Content/PlayerTeam");
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem");
            if (playerSlot != null && playerPrefab != null)
            {
                ClearChildren(playerSlot);
                GameObject player = UnityEngine.Object.Instantiate(playerPrefab, playerSlot, false);
                uiBindings.Apply(player, "DailyDungeonEndPanelItem");
                player.name = "DailyDungeonLocalPlayer";
                DailyDungeonResultPlayerRowAdapter playerAdapter =
                    player.GetComponent<DailyDungeonResultPlayerRowAdapter>();
                if (playerAdapter == null)
                    throw new InvalidOperationException("Official daily dungeon local player row adapter is missing.");
                playerAdapter.Configure(host);
                playerAdapter.ConfigureSettlement(model.Run.grade, uiBindings, host);
                playerRow = playerAdapter;
            }

            rewardGrid = FindPath(transform,
                "Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort/Grid");
            if (rewardGrid == null)
                throw new InvalidOperationException("Official daily dungeon result reward grid is missing.");
            ConfigureRewardScroll();

            SetPrefabActive(gameObject, "Content/BtnGroup/BtnBattleData", false);
            SetPrefabActive(gameObject, "Content/BtnGroup/ShareBtn", false);
            SetPrefabActive(gameObject, "Content/BtnGroup/SaveRecordBtn", false);
            SetPrefabActive(gameObject, "Content/BtnGroup/ThumbUpReportSwitchBtn", false);
        }

        private void RefreshClaimCount()
        {
            uiBindings.ApplyLocalizedText(gameObject, "GameEndPanelDailyDungeon",
                "ui_daily_dungeon_dialog_remain_free_count_tip", host.DailyDungeons.RemainEntries,
                host.DailyDungeons.Catalog.settings.DailyDungeonDailyFreeCount);
        }

        private void ConfigureRewardScroll()
        {
            var binding = uiBindings.LoadBinding("GameEndPanelDailyDungeon");
            var fields = binding.awardScroll
                ?? throw new InvalidOperationException("Original award scroll fields are missing.");
            awardTiming = binding.awardContent
                ?? throw new InvalidOperationException("Original award show timing is missing.");
            Transform node = DailyDungeonUiBindingModule.FindNode(transform, fields.path, null);
            rewardScroll = node.GetComponent<Zjcs.Official.BattleResultUI.AwardLoopScrollRect>()
                ?? node.gameObject.AddComponent<Zjcs.Official.BattleResultUI.AwardLoopScrollRect>();
            rewardScroll.content = rewardGrid as RectTransform;
            rewardScroll.viewport = DailyDungeonUiBindingModule.FindNode(transform, fields.viewport, null) as RectTransform;
            rewardScroll.reverseDirection = fields.reverseDirection;
            rewardScroll.reverseIndex = fields.reverseIndex;
            rewardScroll.horizontal = fields.horizontal; rewardScroll.vertical = fields.vertical;
            rewardScroll.movementType = (LoopScrollRectBase.MovementType)fields.movementType;
            rewardScroll.elasticity = fields.elasticity; rewardScroll.inertia = fields.inertia;
            rewardScroll.decelerationRate = fields.decelerationRate;
            rewardScroll.scrollSensitivity = fields.scrollSensitivity;
            rewardScroll.DefaultPrefabSource = new LoopScrollSinglePrefabSource { Prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Award/AwardPanelItem") };
            rewardScroll.dataSource = this;
        }

        private void ShowPreviewRewards()
        {
            IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards = host.PreviewDailyDungeonRewards(
                model.ClaimId, model.AwardTimes, out _);
            IEnumerable<RewardDisplayRow> rows = (rewards ?? Array.Empty<DailyDungeonClaimRewardDisplayItem>())
                .Where(reward => reward != null && !reward.IsGradeReward && reward.ItemId > 0 && reward.Count > 0)
                .Select(reward => new RewardDisplayRow { ItemId = reward.ItemId, Count = reward.Count });
            DrawRewards(rows);
        }

        private void DrawRewards(IEnumerable<RewardDisplayRow> rows)
        {
            if (rewardGrid == null) return;
            if (rewardRoutine != null) host.StopCoroutine(rewardRoutine);
            rewardScroll.ClearCells();
            rewardRows.Clear(); waitingRewards.Clear();
            rewardRows.AddRange(rows ?? Array.Empty<RewardDisplayRow>());
            rewardRoutine = host.StartCoroutine(ShowRewardRows());
        }

        private IEnumerator ShowRewardRows()
        {
            while (this && !gameObject.activeInHierarchy) yield return null;
            if (!this) yield break;
            if (awardTiming.showDelay > 0) yield return new WaitForSeconds(awardTiming.showDelay);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            rewardScroll.totalCount = rewardRows.Count;
            rewardScroll.RefillCells();
            yield return null;
            while (waitingRewards.Count > 0)
            {
                yield return new WaitForSeconds(awardTiming.interval);
                GameObject item = waitingRewards.Dequeue();
                if (!item) continue;
                item.transform.Find("DelayShow").gameObject.SetActive(true);
                item.GetComponent<Animation>().Play();
            }
            rewardRoutine = null;
        }

        public void ProvideData(Transform itemTransform, int index)
        {
            RewardDisplayRow row = rewardRows[index];
            GameObject slot = itemTransform.gameObject;
            uiBindings.Apply(slot, "AwardPanelItem");
            slot.name = "AwardPanelItem_" + row.ItemId;
            Transform delay = FindPath(slot.transform, "DelayShow");
            ClearChildren(delay);
            delay.gameObject.SetActive(awardTiming.interval <= 0);
            GameObject awardIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemAwardIcon");
            GameObject itemIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemIcon");
            if (awardIconPrefab == null || itemIconPrefab == null)
                throw new InvalidOperationException("Original daily dungeon reward item prefabs are missing.");
            GameObject awardIcon = UnityEngine.Object.Instantiate(awardIconPrefab, delay, false);
            uiBindings.Apply(awardIcon, "ItemAwardIcon");
            Transform anchor = FindPath(awardIcon.transform, "Anchor");
            if (anchor == null) throw new InvalidOperationException("Original item award anchor is missing.");
            anchor.gameObject.SetActive(true);
            GameObject icon = UnityEngine.Object.Instantiate(itemIconPrefab, anchor, false);
            uiBindings.Apply(icon, "ItemIcon");
            SetItemIcon(icon.transform, row.ItemId, row.Count);
            if (awardTiming.interval > 0) waitingRewards.Enqueue(slot);
        }

        private void SetItemIcon(Transform root, int itemId, long count)
        {
            DailyDungeonResultItemIconAdapter icon = root.GetComponent<DailyDungeonResultItemIconAdapter>();
            if (icon == null)
                icon = root.gameObject.AddComponent<DailyDungeonResultItemIconAdapter>();
            icon.Configure(host, itemId, count);
        }

        private void BindButtons()
        {
            uiBindings.ApplyResultClaimState(gameObject, model.IsPrivateRoute ? 3
                : canClaim || canPurchase ? 1 : 2, model.AwardTimes > 1);
            BindButton("Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim ? claim : canPurchase ? purchase : null);
            BindButton("Content/WinObj/Content/DailyDungeonCostMoneyContent/ShowCountTipGo/ShowCountTip/CommonBtn_Add",
                canPurchase ? purchase : null);
            BindButton("Content/WinObj/Content/CloseBtn", close);

            SetNamedText(transform, "Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim ? "领取奖励" : canPurchase ? "购买次数" : "次数不足");
            SetButtonInteractable("Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim || canPurchase);
            SetButtonInteractable("Content/WinObj/Content/DailyDungeonCostMoneyContent/ShowCountTipGo/ShowCountTip/CommonBtn_Add", canPurchase);
        }

        private void BindButton(string path, Action action)
        {
            Transform node = FindPath(transform, path);
            Button button = node == null ? null : node.GetComponent<Button>();
            if (button == null && node != null) button = node.GetComponentInChildren<Button>(true);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        private void SetButtonInteractable(string path, bool value)
        {
            Transform node = FindPath(transform, path);
            Button button = node == null ? null : node.GetComponent<Button>();
            if (button == null && node != null) button = node.GetComponentInChildren<Button>(true);
            if (button != null) button.interactable = value;
        }

        private static Sprite LoadGradeSprite(int gradeType)
        {
            if (gradeSpritePaths == null)
            {
                gradeSpritePaths = new Dictionary<int, string>();
                TextAsset json = Resources.Load<TextAsset>("Dungeons/UI/884_156692/Native/grade-sprites");
                if (json == null) return null;
                GradeSpriteCatalog catalog = JsonUtility.FromJson<GradeSpriteCatalog>(json.text);
                foreach (GradeSpriteEntry entry in catalog?.gradeSprites ?? Array.Empty<GradeSpriteEntry>())
                    if (entry != null && !string.IsNullOrEmpty(entry.resourcePath))
                        gradeSpritePaths[entry.gradeType] = entry.resourcePath;
            }
            return gradeSpritePaths.TryGetValue(gradeType, out string path)
                ? Resources.Load<Sprite>(path) : null;
        }

        private static void SetPrefabActive(GameObject root, string path, bool active)
        {
            Transform node = FindPath(root.transform, path);
            if (node != null) node.gameObject.SetActive(active);
        }

        private static void SetNamedText(Transform root, string name, string value)
        {
            Transform node = FindPath(root, name);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text == null && node != null) text = node.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = value ?? string.Empty;
        }

        private static Transform FindPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform direct = root.Find(path);
            if (direct != null) return direct;
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string part in parts)
            {
                Transform next = null;
                foreach (Transform child in current)
                    if (string.Equals(child.name, part, StringComparison.Ordinal))
                    {
                        next = child;
                        break;
                    }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static void ClearChildren(Transform root)
        {
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                GameObject child = root.GetChild(index).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
