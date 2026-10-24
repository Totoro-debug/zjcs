using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private static readonly int[] MaterialGatheringEntryTypes = { 2, 4, 1, 3 };
        private static readonly string[] MaterialGatheringEntryNodes =
            { "StoneEntryItem", "SilverEntryItem", "SkillEntryItem", "SandEntryItem" };
        private static readonly string[] MaterialGatheringNames =
            { "铁石矿洞", "罗拉金矿", "历战之地", "时光沙海" };
        private GameObject materialGatheringEntryRoot;
        private GameObject materialGatheringPanelRoot;
        private Animator materialGatheringAnimator;
        private int materialGatheringDungeonType;

        private bool MaterialGatheringModalOpen => materialGatheringEntryRoot != null
            || materialGatheringPanelRoot != null;

        private void OpenMaterialGatheringEntries()
        {
            if (travelNotesRoot != null) UnityEngine.Object.Destroy(travelNotesRoot);
            travelNotesRoot = null;
            if (materialGatheringEntryRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Gathering/MopUpEntryPanel");
            if (prefab == null) throw new InvalidOperationException("Official MopUpEntryPanel is missing.");
            materialGatheringEntryRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            materialGatheringEntryRoot.name = "MopUpEntryPanel";
            materialGatheringEntryRoot.transform.SetAsLastSibling();
            BindPrefabButton(materialGatheringEntryRoot, "CloseBtn", CloseMaterialGathering);
            for (int index = 0; index < MaterialGatheringEntryNodes.Length; index++)
            {
                int type = MaterialGatheringEntryTypes[index];
                Transform card = Find(materialGatheringEntryRoot.transform, MaterialGatheringEntryNodes[index]);
                if (card == null) throw new InvalidOperationException("Official MopUp entry card is missing: "
                    + MaterialGatheringEntryNodes[index]);
                int unlockedLevel = host.MaterialGatheringUnlockedLevel(type);
                SetNamedText(card, "StageName", MaterialGatheringNames[index]);
                SetNamedText(card, "TitleText", MaterialGatheringNames[index]);
                SetNamedText(card, "DescText", unlockedLevel > 0 ? "已开放" : "首次采集可用");
                SetNamedText(card, "AmountText", host.MaterialGatheringState(type)?.CollectedAmount.ToString() ?? "0");
                SetPrefabActive(card.gameObject, "Lock", false);
                SetPrefabActive(card.gameObject, "Unlock", true);
                Button button = card.GetComponent<Button>() ?? card.GetComponentInChildren<Button>(true);
                if (button == null)
                {
                    button = card.gameObject.AddComponent<Button>();
                    button.targetGraphic = card.GetComponent<Graphic>() ?? card.GetComponentInChildren<Graphic>(true);
                }
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OpenMaterialGathering(type));
            }
        }

        private void OpenMaterialGathering(int dungeonType)
        {
            if (materialGatheringPanelRoot != null) UnityEngine.Object.Destroy(materialGatheringPanelRoot);
            GameObject prefab = Resources.Load<GameObject>("Gathering/MopUpPanel");
            if (prefab == null) throw new InvalidOperationException("Official MopUpPanel is missing.");
            materialGatheringDungeonType = dungeonType;
            materialGatheringPanelRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            materialGatheringPanelRoot.name = "MopUpPanel_" + dungeonType;
            materialGatheringPanelRoot.transform.SetAsLastSibling();
            materialGatheringAnimator = materialGatheringPanelRoot.GetComponentInChildren<Animator>(true);
            if (materialGatheringEntryRoot != null) materialGatheringEntryRoot.SetActive(false);
            BindPrefabButton(materialGatheringPanelRoot, "CloseBtn", CloseMaterialGatheringDetail);
            BindMaterialGatheringTabs();
            BindPrefabButton(materialGatheringPanelRoot, "OnceBtn", () => GatherMaterial(false));
            BindPrefabButton(materialGatheringPanelRoot, "QuickBtn", () => GatherMaterial(true));
            BindPrefabButton(materialGatheringPanelRoot, "GetBtn", () => GatherMaterial(false));
            BindMaterialGatheringQuickSwitch();
            RefreshMaterialGatheringPanel();
        }

        private void BindMaterialGatheringTabs()
        {
            Transform tabs = Find(materialGatheringPanelRoot.transform, "TopTabGroup");
            if (tabs == null) return;
            Button[] buttons = tabs.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < Math.Min(buttons.Length, 4); index++)
            {
                int type = index + 1;
                buttons[index].onClick.AddListener(() =>
                {
                    materialGatheringDungeonType = type;
                    RefreshMaterialGatheringPanel();
                });
            }
        }

        private void BindMaterialGatheringQuickSwitch()
        {
            Transform switchNode = Find(materialGatheringPanelRoot.transform, "SwitchGo1");
            if (switchNode == null) return;
            bool unlocked = host.MaterialGatheringQuickUnlocked(materialGatheringDungeonType);
            switchNode.gameObject.SetActive(unlocked);
            Toggle toggle = switchNode.GetComponent<Toggle>() ?? switchNode.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(host.MaterialGatheringQuickEnabled);
                toggle.onValueChanged.AddListener(enabled =>
                {
                    if (!host.SetMaterialGatheringQuickEnabled(enabled))
                    {
                        Notify("快速采集设置保存失败");
                        return;
                    }
                    RefreshMaterialGatheringPanel();
                });
                return;
            }
            Button button = switchNode.GetComponent<Button>() ?? switchNode.GetComponentInChildren<Button>(true);
            if (button != null) button.onClick.AddListener(() =>
            {
                if (host.SetMaterialGatheringQuickEnabled(!host.MaterialGatheringQuickEnabled))
                    RefreshMaterialGatheringPanel();
                else Notify("快速采集设置保存失败");
            });
        }

        private void GatherMaterial(bool quick)
        {
            if (quick && (!host.MaterialGatheringQuickUnlocked(materialGatheringDungeonType)
                || !host.MaterialGatheringQuickEnabled))
            {
                Notify("快速采集尚未解锁或未启用");
                return;
            }
            OfflineMaterialGatheringDungeonState before = host.MaterialGatheringState(materialGatheringDungeonType);
            long previousBreakCount = before?.BreakCount ?? 0;
            MaterialGatheringDungeon dungeon = host.MaterialGatheringDungeon(materialGatheringDungeonType);
            int expectedTargetId = before != null && before.TargetMaterialId > 0
                ? before.TargetMaterialId
                : before == null || before.NewbieSequenceIndex < dungeon.NewbieRewardMaterials.Length
                    ? dungeon.NewbieRewardMaterials[before?.NewbieSequenceIndex ?? 0] : 0;
            MaterialGatheringMaterial expectedTarget = host.MaterialGatheringMaterial(expectedTargetId);
            MaterialGatheringActionResult result = host.GatherMaterial(materialGatheringDungeonType, quick);
            if (!result.IsApplied)
            {
                Notify(result.Code switch
                {
                    MaterialGatheringResultCode.Locked => "尚未解锁该素材秘境",
                    MaterialGatheringResultCode.MissingTool => "采集工具不足",
                    MaterialGatheringResultCode.QuickGatherLocked => "快速采集尚未解锁",
                    MaterialGatheringResultCode.MissingTargetRule => "官方目标生成配置缺失",
                    _ => string.IsNullOrEmpty(result.Message) ? "采集未完成" : result.Message
                });
                return;
            }
            OfflineMaterialGatheringDungeonState after = host.MaterialGatheringState(materialGatheringDungeonType);
            bool targetChanged = after != null && after.TargetMaterialId > 0
                && after.TargetMaterialId != expectedTargetId;
            bool targetBroken = after != null && after.BreakCount > previousBreakCount;
            if (materialGatheringAnimator != null && result.ActionCount > 0)
                materialGatheringAnimator.SetTrigger(targetChanged || targetBroken || expectedTarget?.NeedCost == false
                    ? "GoBack" : "GoAhead");
            RefreshMaterialGatheringPanel();
            string rewardText = string.Join("、", result.Rewards.Take(3)
                .Select(value => host.ItemName(value.ItemId) + " ×" + value.Count));
            Notify("采集 " + result.ActionCount + " 次" + (string.IsNullOrEmpty(rewardText)
                ? string.Empty : "，获得 " + rewardText));
        }

        private void RefreshMaterialGatheringPanel()
        {
            if (materialGatheringPanelRoot == null) return;
            MaterialGatheringDungeon dungeon = host.MaterialGatheringDungeon(materialGatheringDungeonType);
            if (dungeon == null) return;
            OfflineMaterialGatheringDungeonState state = host.MaterialGatheringState(materialGatheringDungeonType);
            int sequenceIndex = state == null ? 0 : state.NewbieSequenceIndex;
            int targetId = state != null && state.TargetMaterialId > 0 ? state.TargetMaterialId
                : sequenceIndex < dungeon.NewbieRewardMaterials.Length
                    ? dungeon.NewbieRewardMaterials[sequenceIndex] : 0;
            MaterialGatheringMaterial target = host.MaterialGatheringMaterial(targetId);
            int durability = state != null && state.TargetMaterialId == targetId
                ? state.RemainingDurability : target?.Durability ?? 0;
            SetNamedText(materialGatheringPanelRoot.transform, "TitleTxt", MaterialGatheringName(materialGatheringDungeonType));
            SetNamedText(materialGatheringPanelRoot.transform, "FreeCount", string.Empty);
            SetPrefabActive(materialGatheringPanelRoot, "FreeCountBg", false);
            SetNamedText(materialGatheringPanelRoot.transform, "AmountText", (state?.CollectedAmount ?? 0).ToString());
            SetNamedText(materialGatheringPanelRoot.transform, "Txt", targetId > 0
                ? host.ItemName(targetId) + "  ·  " + Math.Max(0, durability) : "等待生成采集目标");
            Image targetIcon = Find(materialGatheringPanelRoot.transform, "MopUpBgImg")?.GetComponent<Image>();
            if (targetIcon != null && targetId > 0)
            {
                Sprite sprite = host.ItemIcon(targetId);
                if (sprite != null) targetIcon.sprite = sprite;
            }
            Sprite tool = host.ItemIcon(dungeon.CostItemId);
            foreach (string actionName in new[] { "OnceBtn", "QuickBtn", "GetBtn" })
            {
                Transform action = Find(materialGatheringPanelRoot.transform, actionName);
                Image icon = Find(action, "ItemImg")?.GetComponent<Image>();
                Sprite actionIcon = actionName == "GetBtn" && target != null && !target.NeedCost
                    ? host.ItemIcon(targetId) : tool;
                if (icon != null && actionIcon != null) icon.sprite = actionIcon;
                SetNamedText(action, "count", actionName == "GetBtn" ? string.Empty : "×" + dungeon.CostCount);
                Button button = action == null ? null : action.GetComponent<Button>() ?? action.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    bool hasTool = actionName == "GetBtn"
                        || host.ItemCount(dungeon.CostItemId) >= dungeon.CostCount;
                    bool quickAllowed = actionName != "QuickBtn"
                        || host.MaterialGatheringQuickUnlocked(materialGatheringDungeonType)
                        && host.MaterialGatheringQuickEnabled;
                    button.interactable = hasTool && quickAllowed;
                }
            }
            SetPrefabActive(materialGatheringPanelRoot, "GetBtn", target != null && !target.NeedCost);
            Transform quickSwitch = Find(materialGatheringPanelRoot.transform, "SwitchGo1");
            if (quickSwitch != null)
            {
                quickSwitch.gameObject.SetActive(host.MaterialGatheringQuickUnlocked(materialGatheringDungeonType));
                Toggle toggle = quickSwitch.GetComponent<Toggle>() ?? quickSwitch.GetComponentInChildren<Toggle>(true);
                if (toggle != null) toggle.SetIsOnWithoutNotify(host.MaterialGatheringQuickEnabled);
            }
            Transform tabs = Find(materialGatheringPanelRoot.transform, "TopTabGroup");
            if (tabs != null)
            {
                Transform[] tabItems = tabs.GetComponentsInChildren<Transform>(true)
                    .Where(value => value.name == "TopTabGroupItem").ToArray();
                for (int index = 0; index < tabItems.Length; index++)
                    SetPrefabActive(tabItems[index].gameObject, "Selected", index + 1 == materialGatheringDungeonType);
            }
        }

        private void CloseMaterialGatheringDetail()
        {
            if (materialGatheringPanelRoot != null) UnityEngine.Object.Destroy(materialGatheringPanelRoot);
            materialGatheringPanelRoot = null;
            materialGatheringAnimator = null;
            if (materialGatheringEntryRoot != null) materialGatheringEntryRoot.SetActive(true);
        }

        private void CloseMaterialGathering()
        {
            CloseMaterialGatheringDetail();
            if (materialGatheringEntryRoot != null) UnityEngine.Object.Destroy(materialGatheringEntryRoot);
            materialGatheringEntryRoot = null;
        }

        private static string MaterialGatheringName(int type)
        {
            int index = Array.IndexOf(MaterialGatheringEntryTypes, type);
            return index < 0 ? "素材秘境" : MaterialGatheringNames[index];
        }
    }
}
