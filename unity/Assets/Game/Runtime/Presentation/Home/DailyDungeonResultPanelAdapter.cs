using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultPanelAdapter : MonoBehaviour
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
        private DailyDungeonSettlementHudModel model;
        private Action claim;
        private Action purchase;
        private Action close;
        private bool canClaim;
        private bool canPurchase;
        private Transform rewardGrid;

        public static DailyDungeonResultPanelAdapter Create(GameObject prefab, Transform parent)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (parent == null) throw new ArgumentNullException(nameof(parent));

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
            gameObject.SetActive(true);
        }

        public void ShowClaimedRewards(IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards)
        {
            DrawRewards((rewards ?? Array.Empty<DailyDungeonClaimRewardDisplayItem>())
                .Where(value => value != null && value.ItemId > 0 && value.Count > 0)
                .Select(value => new RewardDisplayRow { ItemId = value.ItemId, Count = value.Count }));
            canClaim = false;
            canPurchase = false;
            SetNamedText(transform, "CommonBtn_CostItem", "已领取");
            SetButtonInteractable("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_CostItem", false);
            SetButtonInteractable("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_Add", false);
            SetNamedText(transform, "ReturnText", "奖励已发放至背包");
        }

        private void BindOfficialView()
        {
            SetNamedText(transform, "Content/WinObj/GoTitleBg/Title",
                model.Level == null ? "副本结算" : model.Level.Title + "结算");
            SetNamedText(transform, "Content/WinObj/DifficultyTitleGo/TitleText",
                (model.Level?.Title ?? "日常副本") + " · 难度 " + model.Run.difficulty);

            Transform gradeNode = FindPath(transform, "Content/WinObj/GradeImg");
            Image gradeImage = gradeNode == null ? null : gradeNode.GetComponent<Image>();
            if (gradeImage != null)
            {
                gradeImage.sprite = LoadGradeSprite(model.Run.grade);
                gradeImage.enabled = gradeImage.sprite != null;
            }

            Transform playerSlot = FindPath(transform, "Content/WinObj/Content/PlayerTeam");
            GameObject playerPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem");
            if (playerSlot != null && playerPrefab != null)
            {
                ClearChildren(playerSlot);
                GameObject player = UnityEngine.Object.Instantiate(playerPrefab, playerSlot, false);
                player.name = "DailyDungeonLocalPlayer";
                DailyDungeonResultPlayerRowAdapter playerAdapter =
                    player.GetComponent<DailyDungeonResultPlayerRowAdapter>();
                if (playerAdapter == null)
                    throw new InvalidOperationException("Official daily dungeon local player row adapter is missing.");
                playerAdapter.Configure(host);
            }

            rewardGrid = FindPath(transform,
                "Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort/Grid");
            if (rewardGrid == null)
                throw new InvalidOperationException("Official daily dungeon result reward grid is missing.");
            ConfigureRewardScroll();

            SetPrefabActive(gameObject, "Content/WinObj/Content/BtnGroup/BtnBattleData", false);
            SetPrefabActive(gameObject, "Content/WinObj/Content/BtnGroup/ShareBtn", false);
            SetPrefabActive(gameObject, "Content/WinObj/Content/BtnGroup/SaveRecordBtn", false);
            SetPrefabActive(gameObject, "Content/WinObj/Content/BtnGroup/ThumbUpReportSwitchBtn", false);
        }

        private void ConfigureRewardScroll()
        {
            GridLayoutGroup gridLayout = rewardGrid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null) gridLayout = rewardGrid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.padding = new RectOffset(0, 0, 8, 0);
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.cellSize = new Vector2(208f, 208f);
            gridLayout.spacing = new Vector2(12f, 30f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;

            Transform scrollNode = FindPath(transform,
                "Content/WinObj/Content/GameEndRewardContent/ScrollRect");
            ScrollRect scroll = scrollNode == null ? null : scrollNode.GetComponent<ScrollRect>();
            if (scrollNode != null && scroll == null) scroll = scrollNode.gameObject.AddComponent<ScrollRect>();
            if (scroll != null)
            {
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                scroll.elasticity = 0.1f;
                scroll.inertia = true;
                scroll.decelerationRate = 0.135f;
                scroll.scrollSensitivity = 1f;
                scroll.viewport = FindPath(transform,
                    "Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort") as RectTransform;
                scroll.content = rewardGrid as RectTransform;
            }
        }

        private void ShowPreviewRewards()
        {
            IEnumerable<RewardDisplayRow> rows = (model.RewardRules ?? Array.Empty<DailyDungeonSettlementRewardRule>())
                .Where(rule => rule?.Candidates != null)
                .SelectMany(rule => rule.Candidates)
                .Where(candidate => candidate != null && candidate.ItemId > 0 && candidate.Count > 0)
                .Select(candidate => new RewardDisplayRow
                {
                    ItemId = candidate.ItemId,
                    Count = candidate.Count
                });
            DrawRewards(rows);
        }

        private void DrawRewards(IEnumerable<RewardDisplayRow> rows)
        {
            if (rewardGrid == null) return;
            ClearChildren(rewardGrid);
            GameObject slotPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Award/AwardPanelItem");
            GameObject awardIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemAwardIcon");
            GameObject itemIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemIcon");
            if (slotPrefab == null || awardIconPrefab == null || itemIconPrefab == null)
                throw new InvalidOperationException("Official daily dungeon reward item prefabs are missing.");

            foreach (RewardDisplayRow row in rows ?? Array.Empty<RewardDisplayRow>())
            {
                GameObject slot = UnityEngine.Object.Instantiate(slotPrefab, rewardGrid, false);
                slot.name = "AwardPanelItem_" + row.ItemId;
                Transform delay = FindPath(slot.transform, "DelayShow") ?? slot.transform;
                delay.gameObject.SetActive(true);
                GameObject awardIcon = UnityEngine.Object.Instantiate(awardIconPrefab, delay, false);
                Transform anchor = FindPath(awardIcon.transform, "Anchor");
                if (anchor == null)
                    throw new InvalidOperationException("Official item award anchor is missing.");
                anchor.gameObject.SetActive(true);
                GameObject icon = UnityEngine.Object.Instantiate(itemIconPrefab, anchor, false);
                SetItemIcon(icon.transform, row.ItemId, row.Count);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGrid as RectTransform);
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
            BindButton("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim ? claim : canPurchase ? purchase : null);
            BindButton("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_Add",
                canPurchase ? purchase : null);
            BindButton("Content/WinObj/Content/CloseBtn", close);

            SetNamedText(transform, "Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim ? "领取奖励" : canPurchase ? "购买次数" : "次数不足");
            SetButtonInteractable("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_CostItem",
                canClaim || canPurchase);
            SetButtonInteractable("Content/WinObj/DailyDungeonCostMoneyContent/CommonBtn_Add", canPurchase);
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
