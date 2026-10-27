using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldBattleReportPlayerRoleData
    {
        public long Damage, RealCure;
        public long? BeAbsoluteDamage = 0, ApplyStatusScore = 0;
    }

    // Original FightReportData's three independent report categories. The production grouping
    // provider supplies these records; unavailable local histories are explicitly nullable.
    public sealed class WorldBattleReportPlayerRowData
    {
        public long TargetId;
        public WorldBattleReportPlayerRoleData NormalReport = new WorldBattleReportPlayerRoleData();
        public WorldBattleReportPlayerRoleData SummonNormalReport = new WorldBattleReportPlayerRoleData();
        public WorldBattleReportPlayerRoleData PetNormalReport = new WorldBattleReportPlayerRoleData();
        public long Damage => NormalReport.Damage + SummonNormalReport.Damage + PetNormalReport.Damage;
        public long? BeAbsoluteDamage => NormalReport.BeAbsoluteDamage + SummonNormalReport.BeAbsoluteDamage + PetNormalReport.BeAbsoluteDamage;
        public long Cure => NormalReport.RealCure + SummonNormalReport.RealCure + PetNormalReport.RealCure;
        public long? ApplyStatusScore => NormalReport.ApplyStatusScore + SummonNormalReport.ApplyStatusScore + PetNormalReport.ApplyStatusScore;
    }

    /// <summary>Original FightEndReportPlayerTabViewItem numeric view and self-highlight state.
    /// PlayerIcon keeps the original prefab reference while its full provider is migrated.</summary>
    public sealed class WorldBattleReportPlayerRow : MonoBehaviour
    {
        public Component PlayerIcon;
        public WorldStateSwitcher<bool> MyselfStateSwitcher;
        public Slider DamageSlider;
        public WorldMultiLanguageText DamageValue;
        public Slider BeDamageSlider;
        public WorldMultiLanguageText BeDamageValue;
        public Slider CureSlider;
        public WorldMultiLanguageText CureValue;
        public Slider ApplyStatusSlider;
        public WorldMultiLanguageText ApplyStatusValue;

        private WorldBattleReportPlayerRowData report;
        private long currentPlayerId, teamDamage, teamCure;
        private long? teamBeDamage, teamStatusScore;
        private Action<Component, long> initPlayerIcon;

        public void Init(WorldBattleReportPlayerRowData data, long playerId, long damage,
            long? beDamage, long cure, long? statusScore, Action<Component, long> iconProvider = null)
        {
            report = data ?? throw new ArgumentNullException(nameof(data));
            currentPlayerId = playerId;
            teamDamage = damage;
            teamBeDamage = beDamage;
            teamCure = cure;
            teamStatusScore = statusScore;
            initPlayerIcon = iconProvider;
            if (isActiveAndEnabled) Refresh();
        }

        private void OnEnable() { if (report != null) Refresh(); }

        private void Refresh()
        {
            initPlayerIcon?.Invoke(PlayerIcon, report.TargetId);
            MyselfStateSwitcher.SetState(report.TargetId == currentPlayerId);
            SetValue(DamageValue, DamageSlider, report.Damage, teamDamage);
            SetValue(BeDamageValue, BeDamageSlider, report.BeAbsoluteDamage, teamBeDamage);
            SetValue(CureValue, CureSlider, report.Cure, teamCure);
            SetValue(ApplyStatusValue, ApplyStatusSlider, report.ApplyStatusScore, teamStatusScore);
        }

        private static void SetValue(WorldMultiLanguageText text, Slider slider, long? amount, long? total)
        {
            text.Format(amount.HasValue ? WorldBattleReportNumberFormat.Compact(amount.Value) : string.Empty);
            // Preserve the original float conversion and division order.
            slider.value = !amount.HasValue || !total.HasValue || total.Value == 0 ? 0f
                : 1f * (float)amount.Value / (float)total.Value;
        }
    }
}
