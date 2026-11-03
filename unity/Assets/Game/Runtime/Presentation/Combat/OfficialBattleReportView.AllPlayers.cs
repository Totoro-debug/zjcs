using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleReportView
    {
        private readonly List<FightBattleReportPlayerGroup> selectablePlayers = new List<FightBattleReportPlayerGroup>();
        private WorldUIDropdown dropdown;
        private int? selectedPlayerIndex;

        private void BuildPlayerRows(Transform slot, FightBattleReportPlayerGroups groups,
            FightBattleReportScoreSettings settings)
        {
            var records = OfficialBattleReportPlayerRowBinder.PlayerRows(groups, settings);
            var rows = new List<WorldBattleReportPlayerRow>();
            var prefab = Resources.Load<GameObject>("Combat/BattleReport/FightEndReportPlayerTabViewItem");
            foreach (var record in records)
            {
                var row = UnityEngine.Object.Instantiate(prefab, slot, false);
                BindFont(row);
                var view = row.GetComponent<WorldBattleReportPlayerRow>();
                if (view.PlayerIcon == null) view.PlayerIcon = row.transform.Find("PublicPlayerIcon");
                if (view.PlayerIcon == null) throw new InvalidOperationException("The original report player icon slot is missing.");
                rows.Add(view);
            }
            OfficialBattleReportPlayerRowBinder.InitRows(rows, records, presentation.PlayerId,
                (icon, id) => presentation.PlayerIcon?.Invoke(icon, checked((int)id)));
        }

        // FightEndReportAllPlayerTabView: own faction first, all remaining factions second;
        // each section calculates its own four denominators and retains insertion order.
        private void BuildAllPlayerRows(Transform slot, FightBattleReportPlayerGroups own,
            FightBattleReportScoreSettings settings)
        {
            var other = new FightBattleReportPlayerGroups();
            foreach (int side in report.Units.Select(value => value.Side).Distinct())
            {
                if (side == presentation.Side) continue;
                var group = FightBattleReportGrouping.Build(report, side);
                other.Players.AddRange(group.Players);
                other.Unassigned.AddRange(group.Unassigned);
            }
            bool team = checkpoint.StageType >= 44 && checkpoint.StageType <= 47;
            AddSection(own, team ? "ui_battle_data_team_1" : "ui_battle_data_own");
            AddSection(other, team ? "ui_battle_data_team_2" : "ui_battle_data_other");
            void AddSection(FightBattleReportPlayerGroups group, string key)
            {
                var split = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(
                    "Combat/BattleReport/FightEndReportSplit"), slot, false);
                BindFont(split);
                Transform title = split.transform;
                if (!string.IsNullOrEmpty(bindings.player.splitTitle))
                    foreach (string part in bindings.player.splitTitle.Split('/')) title = title.GetChild(int.Parse(part));
                var language = title.GetComponent<WorldMultiLanguageText>();
                language.Localizer = value => WorldBattleReportTextService.ProcessText(
                    bindings.texts.Single(entry => entry.key == value).text);
                language.SetKey(key);
                BuildPlayerRows(slot, group, settings);
                selectablePlayers.AddRange(group.Players);
            }
        }

        // FightEndAllPlayerReportPanel.GetPlayerNames/HandlePlayerSelectDropdown. Names come
        // from the host's recorded identity provider; report code never invents player/NPC names.
        private void InitializePlayerDropdown()
        {
            Node(bindings.window.skillRound).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_battle_data_round", ReportRound());
            dropdown = Node(bindings.window.dropdown).GetComponent<WorldUIDropdown>();
            root.AddComponent<OfficialBattlePopupPointer>().PointerDown = dropdown.OnGlobalPointerDown;
            dropdown.ItemsKey.Clear();
            for (int i = 0; i < selectablePlayers.Count; i++) dropdown.ItemsKey.Add(PlayerLabel(i, false));
            dropdown.OnDropdownUpdate += OnPlayerChanged;
            int index = selectablePlayers.FindIndex(value => value.TargetId == presentation.PlayerId);
            if (index < 0 && selectablePlayers.Count > 0) index = 0;
            if (index >= 0) dropdown.SetSelectedItemIndex(index);
        }

        private string PlayerLabel(int index, bool selected)
        {
            int id = selectablePlayers[index].TargetId;
            Color color = selected ? bindings.window.selectedColor
                : id == presentation.PlayerId ? bindings.window.ownColor : bindings.window.otherColor;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">"
                + (presentation.PlayerName?.Invoke(id) ?? string.Empty) + "</color>";
        }

        private void OnPlayerChanged(int index)
        {
            if (disposed || index < 0 || index >= selectablePlayers.Count || selectedPlayerIndex == index) return;
            if (selectedPlayerIndex.HasValue)
                dropdown.ItemsKey[selectedPlayerIndex.Value] = PlayerLabel(selectedPlayerIndex.Value, false);
            dropdown.ItemsKey[index] = PlayerLabel(index, true);
            var owner = selectablePlayers[index];
            playerUnits = new HashSet<int>(owner.Normal.Concat(owner.Summon).Select(value => value.UnitId));
            petUnits = new HashSet<int>(owner.Pet.Select(value => value.UnitId));
            Node(bindings.skill.tabGroupGo).gameObject.SetActive(petUnits.Count != 0);
            selectedPlayerIndex = index;
            skillTabs.ResetTabItem(true);
            SelectSkills(0);
        }
    }
}
