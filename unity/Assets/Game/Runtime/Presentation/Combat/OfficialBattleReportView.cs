using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    // Hosts supply appearance/detail providers; the native window, statistics,
    // ownership groups, tabs and scrolling belong to the shared battle module.
    public sealed partial class OfficialBattleReportView : IDisposable, LoopScrollDataSource
    {
        public sealed class Presentation
        {
            public int PlayerId = 1;
            public int Side = 1;
            public int MaximumRound = int.MaxValue;
            public TMP_FontAsset Font;
            // The original client disables this when no server environment tags are present.
            public bool ShowEquipmentSkills;
            public Action<Component, int> PlayerIcon;
            public Func<int, string> PlayerName;
            public Action<Transform, FightBattleReportSkill, FightBattleSkillDefinition> SkillIcon;
            public Action<FightBattleSkillDefinition> SkillDetail;
            public Action Closed;
        }

        [Serializable] private sealed class Bindings
        {
            public Window window; public Player player; public Skill skill;
            public Tab[] panelTabs, skillTabs; public Text[] texts;
        }
        [Serializable] private sealed class Window
        { public string path, tabVisual, tabs, close, round, player, skill; public State[] states; public int maskType; public bool closeByMask; public OfficialBattlePopupMask.Binding mask;
            public string dropdown, skillRound; public Color ownColor, otherColor, selectedColor; public bool hideTabsWithoutMe; }
        [Serializable] private sealed class Player { public string path, slot, splitTitle; }
        [Serializable] private sealed class Skill
        { public string path, titleScroll, itemScroll, tabGroupGo, tabs; public State[] gmStates, equipmentStates; }
        [Serializable] private sealed class Text { public string key, text; }
        [Serializable] private sealed class Tab { public string path; public int type; }
        [Serializable] private sealed class State { public int state; public string[] objects; }

        private readonly GameObject root;
        private readonly Bindings bindings;
        private readonly FightBattleCheckpoint checkpoint;
        private readonly FightBattleReport report;
        private readonly FightOfficialSkillItemCatalog skillItems;
        private readonly Presentation presentation;
        private readonly LoopVerticalScrollRect scroll;
        private readonly WorldBattleReportSkillScrollSync sync;
        private readonly WorldTabGroup tabs, skillTabs;
        private HashSet<int> playerUnits, petUnits;
        private readonly bool allPlayers;
        private FightBattleReportSkill[] skills = Array.Empty<FightBattleReportSkill>();
        private bool disposed;

        public GameObject Root => root;
        public bool IsReady => !disposed && root != null &&
            (bindings.window.maskType == 0 || root.GetComponent<OfficialBattlePopupMask>().IsReady);

        public OfficialBattleReportView(Transform parent, FightBattleCheckpoint checkpoint, Presentation presentation)
            : this(parent, checkpoint, null, presentation)
        {
        }

        public OfficialBattleReportView(Transform parent, FightBattleCheckpoint checkpoint,
            FightBattleReport finalReport, Presentation presentation)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            if (!checkpoint.Finished || checkpoint.Outcome == null || checkpoint.Outcome.Invalid)
                throw new InvalidOperationException("A battle report requires a completed, valid battle.");
            report = finalReport ?? FightBattleReportBuilder.Build(checkpoint);
            if (report.StageId != checkpoint.StageId)
                throw new InvalidOperationException("The battle report does not match the completed checkpoint.");
            var groups = FightBattleReportGrouping.Build(report, presentation.Side);
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            skillItems = assets.LoadSkillItems();
            var settings = assets.LoadReportScoreSettings();
            allPlayers = settings.ReportAllPlayerStages.Contains(checkpoint.StageType);
            FightBattleReportPlayerGroup owner = groups.Players.SingleOrDefault(value => value.TargetId == presentation.PlayerId);
            playerUnits = new HashSet<int>(owner == null ? Enumerable.Empty<int>()
                : owner.Normal.Concat(owner.Summon).Select(value => value.UnitId));
            petUnits = new HashSet<int>(owner == null ? Enumerable.Empty<int>() : owner.Pet.Select(value => value.UnitId));
            TextAsset data = Resources.Load<TextAsset>("Combat/BattleReport/" +
                (allPlayers ? "all-player-report-window-bindings" : "report-window-bindings"));
            if (data == null) throw new InvalidOperationException("The original report window bindings are missing.");
            bindings = JsonUtility.FromJson<Bindings>(data.text);
            string prefabName = allPlayers ? "FightEndAllPlayerReportPanel" : "FightEndReportPanel";
            GameObject prefab = Resources.Load<GameObject>("Combat/BattleReport/" + prefabName);
            if (prefab == null) throw new InvalidOperationException("The original report window prefab is missing.");
            root = UnityEngine.Object.Instantiate(prefab, parent, false);
            root.name = prefabName;
            root.SetActive(false);
            try
            {
                root.transform.SetAsLastSibling();
                PutAboveHost();
                BindFont(root);
                Node(bindings.window.round).GetComponent<WorldMultiLanguageText>()
                    .SetKey("ui_battle_data_round", ReportRound());
                Node(bindings.window.close).GetComponent<Button>().onClick.AddListener(Close);
                if (!allPlayers || bindings.window.hideTabsWithoutMe)
                    Node(bindings.window.tabVisual).gameObject.SetActive(owner != null);

                Transform playerSlot = Node(bindings.player.slot);
                foreach (Transform child in playerSlot) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
                if (allPlayers) BuildAllPlayerRows(playerSlot, groups, settings);
                else BuildPlayerRows(playerSlot, groups, settings);

                scroll = Node(bindings.skill.itemScroll).GetComponent<LoopVerticalScrollRect>();
                sync = Node(bindings.skill.path).GetComponent<WorldBattleReportSkillScrollSync>();
                if (scroll == null || sync == null) throw new InvalidOperationException("The original report scroll bindings are missing.");
                scroll.dataSource = this;
                sync.SetGMState(false);
                SetState(bindings.skill.gmStates, 0);
                SetState(bindings.skill.equipmentStates, presentation.ShowEquipmentSkills ? 1 : 0);
                Node(bindings.skill.tabGroupGo).gameObject.SetActive(petUnits.Count != 0);
                tabs = Node(bindings.window.tabs).GetComponent<WorldTabGroup>();
                skillTabs = Node(bindings.skill.tabs).GetComponent<WorldTabGroup>();
                if (tabs == null || skillTabs == null) throw new InvalidOperationException("The original report tab groups are missing.");
                tabs.OnTabItemChanged.AddListener(OnTabChanged);
                skillTabs.OnTabItemChanged.AddListener(OnSkillTabChanged);
                if (allPlayers) InitializePlayerDropdown();
                SelectSkills(0);
                root.SetActive(true);
                skillTabs.ResetTabItem(true);
                tabs.ResetTabItem(true);
                if (bindings.window.maskType != 0)
                    root.AddComponent<OfficialBattlePopupMask>().Show(bindings.window.mask,
                        bindings.window.maskType, bindings.window.closeByMask, Close, null,
                        allPlayers ? (Action<Vector2>)dropdown.OnGlobalPointerDown : null);
            }
            catch { Dispose(); throw; }
        }

        private Transform Node(string path)
        {
            Transform node = root.transform;
            if (!string.IsNullOrEmpty(path)) foreach (string part in path.Split('/')) node = node.GetChild(int.Parse(part));
            return node;
        }

        private void SetState(State[] states, int state)
        {
            var data = new WorldStateGameObjectData {
                GoConfig = states.Select(value => new WorldStateGameObjectData.StateGameObjectKV {
                    State = value.state, GameObjects = value.objects.Select(path => Node(path).gameObject).ToList()
                }).ToList()
            };
            WorldStateGameObjectConfig.FromBindings(data).SetState(state);
        }

        private void OnTabChanged(WorldTabItem item, bool selected)
        {
            if (!selected || disposed) return;
            int type = bindings.panelTabs.Single(value => Node(value.path) == item.transform).type;
            SetState(bindings.window.states, type);
            if (dropdown != null) dropdown.Hide();
            if (type == 1) RefreshSkills();
        }

        private void OnSkillTabChanged(WorldTabItem item, bool selected)
        {
            if (!selected || disposed) return;
            SelectSkills(bindings.skillTabs.Single(value => Node(value.path) == item.transform).type);
        }

        private void SelectSkills(int type)
        {
            HashSet<int> units = type == 1 ? petUnits : playerUnits;
            skills = report.Skills.Where(value => units.Contains(value.UnitId)
                && (type == 1 || IsEquipmentSkill(value) == (type == 2))).ToArray();
            RefreshSkills();
        }

        private bool IsEquipmentSkill(FightBattleReportSkill skill)
        {
            // Official SkillUserType.Equip = 4; unknown derived skills retain the player branch.
            return skillItems.TryGet(skill.SkillItemId, out FightOfficialSkillItem item) && item.UserType == 4;
        }

        private int ReportRound()
        {
            int round = checkpoint.Timeline.Round;
            int limit = presentation.MaximumRound;
            if (checkpoint.IsPlayerRoundLimit)
            {
                round = checkpoint.Units.Single(value => value.Id == presentation.PlayerId).SkillState.Round;
                limit = Math.Min(limit, checkpoint.PlayerRoundLimit > 0 ? checkpoint.PlayerRoundLimit : checkpoint.MaxRound);
            }
            return Math.Max(0, Math.Min(round, limit));
        }

        private void RefreshSkills()
        {
            scroll.ClearCells();
            scroll.totalCount = skills.Length;
            if (!scroll.isActiveAndEnabled) return;
            scroll.RefillCells();
            sync.Initialize();
            sync.RefreshLayout();
        }

        public void ProvideData(Transform item, int index)
        {
            if (disposed || index < 0 || index >= skills.Length) throw new ArgumentOutOfRangeException(nameof(index));
            BindFont(item.gameObject);
            FightBattleReportSkill skill = skills[index];
            FightBattleUnitState unit = checkpoint.Units.Single(value => value.Id == skill.UnitId);
            var snapshots = unit.Skills.Concat(unit.ReportSkillSnapshots ?? new List<FightBattleSkillDefinition>());
            FightBattleSkillDefinition snapshot = snapshots.FirstOrDefault(value =>
                value.ItemId == skill.SkillItemId && value.EntityId == skill.SkillEntityId)
                // Passive-trigger statistics retain their root item snapshot even when
                // the executed child entity differs from that root skill entity.
                ?? snapshots.FirstOrDefault(value => value.ItemId == skill.SkillItemId);
            item.GetComponent<WorldBattleReportSkillRow>().Refresh(OfficialBattleReportSkillRowBinder.Values(skill), false, slot => {
                foreach (Transform old in slot) { old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
                presentation.SkillIcon?.Invoke(slot, skill, snapshot);
                foreach (Button button in slot.GetComponentsInChildren<Button>(true))
                {
                    button.enabled = true;
                    if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
                    button.onClick.RemoveAllListeners();
                    if (snapshot != null && presentation.SkillDetail != null)
                        button.onClick.AddListener(() => presentation.SkillDetail(snapshot));
                }
            });
        }

        private void BindFont(GameObject target)
        {
            if (presentation.Font == null) return;
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
            { text.font = presentation.Font; text.fontSharedMaterial = presentation.Font.material; }
        }

        private void PutAboveHost()
        {
            int order = root.transform.parent.GetComponentsInChildren<Canvas>(true)
                .Where(value => !value.transform.IsChildOf(root.transform)).Select(value => value.sortingOrder).DefaultIfEmpty(0).Max() + 1;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            { canvas.overrideSorting = true; canvas.sortingOrder = Math.Max(canvas.sortingOrder, order); }
        }

        private void Close() { if (disposed) return; Dispose(); presentation.Closed?.Invoke(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (tabs != null) tabs.OnTabItemChanged.RemoveListener(OnTabChanged);
            if (skillTabs != null) skillTabs.OnTabItemChanged.RemoveListener(OnSkillTabChanged);
            if (scroll != null) scroll.dataSource = null;
            if (dropdown != null) dropdown.OnDropdownUpdate -= OnPlayerChanged;
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        }
    }
}
