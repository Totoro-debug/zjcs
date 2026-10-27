using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Original FightEndReportPlayerTabView: retain its slot/prefab, enable refresh, child
    // replacement, dictionary order and team totals. Local providers supply resolved records.
    public sealed class WorldBattleReportPlayerView : MonoBehaviour
    {
        public Transform PlayerSlot;
        public GameObject PrefabObj;

        private IReadOnlyList<WorldBattleReportPlayerRowData> data;
        private long currentPlayerId;
        private Action<Component, long> initPlayerIcon;

        public void Init(IReadOnlyList<WorldBattleReportPlayerRowData> records, long playerId,
            Action<Component, long> iconProvider = null)
        {
            data = records ?? throw new ArgumentNullException(nameof(records));
            currentPlayerId = playerId;
            initPlayerIcon = iconProvider;
            if (isActiveAndEnabled) RefreshView();
        }

        private void OnEnable() { if (data != null) RefreshView(); }

        private void RefreshView()
        {
            for (int i = PlayerSlot.childCount - 1; i >= 0; i--)
                Destroy(PlayerSlot.GetChild(i).gameObject);
            var rows = new List<WorldBattleReportPlayerRow>();
            foreach (var item in data)
                rows.Add(Instantiate(PrefabObj, PlayerSlot, false).GetComponent<WorldBattleReportPlayerRow>());
            InitRows(rows, data, currentPlayerId, initPlayerIcon);
        }

        public static void InitRows(IReadOnlyList<WorldBattleReportPlayerRow> rows,
            IReadOnlyList<WorldBattleReportPlayerRowData> records, long playerId,
            Action<Component, long> iconProvider = null)
        {
            if (rows == null || records == null || rows.Count != records.Count)
                throw new ArgumentException("Player report rows and records differ.");
            long damage = 0, cure = 0;
            long? beDamage = 0, status = 0;
            foreach (var item in records)
            {
                damage += item.Damage;
                beDamage += item.BeAbsoluteDamage;
                cure += item.Cure;
                status += item.ApplyStatusScore;
            }
            for (int i = 0; i < rows.Count; i++)
                rows[i].Init(records[i], playerId, damage, beDamage, cure, status, iconProvider);
        }
    }
}
