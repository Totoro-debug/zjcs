using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // The server report's numeric fields. Nullable fields preserve unavailable offline history.
    public sealed class WorldBattleReportSkillRowData
    {
        public int Count;
        public float? AvgDamageCount, AvgDamageCountPerTarget;
        public long RealDamage, RealCure;
        public long? RealShield;
        public float RealDamagePer, RealCurePer;
        public float? RealShieldPer, RealCureMaxHpPer, RealShieldMaxHpPer;
        public int? ApplyBuffStatusCount, ApplyDebuffStatusCount, DamageCount, CureCount;
    }

    /// <summary>Original FightEndReportSkillTabViewItem numeric Refresh and color/state branches.
    /// The icon manager, detail popup and report snapshot providers are separate pending adapters.</summary>
    public sealed class WorldBattleReportSkillRow : MonoBehaviour
    {
        [SerializeField] private Transform mSkillSlot;
        [SerializeField] private WorldMultiLanguageText mCountText, mAvgDmgCountText, mAvgDmgCountPerTargetText;
        [SerializeField] private WorldMultiLanguageText mRealDamageText;
        [SerializeField] private Slider mRealDamagePerSlider;
        [SerializeField] private WorldMultiLanguageText mRealCureText;
        [SerializeField] private Slider mRealCurePerSlider;
        [SerializeField] private WorldMultiLanguageText mRealCureMaxHpPerText, mRealShieldText;
        [SerializeField] private Slider mRealShieldPerSlider;
        [SerializeField] private WorldMultiLanguageText mRealShieldMaxHpPerText, mApplyBuffStatusCountText, mApplyDebuffStatusCountText;
        [SerializeField] private Color mNormalTextColor, mZeroTextColor;
        [SerializeField] private WorldMultiLanguageText mDamageCountText, mCureCountText;
        [SerializeField] private WorldStateSwitcher<bool> mGMStateSwitcher;

        public void Refresh(WorldBattleReportSkillRowData info, bool gm, Action<Transform> refreshSkillIcon = null)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            refreshSkillIcon?.Invoke(mSkillSlot);
            SetNumber(mCountText, info.Count);
            SetAverage(mAvgDmgCountText, info.AvgDamageCount);
            SetAverage(mAvgDmgCountPerTargetText, info.AvgDamageCountPerTarget);
            SetNumber(mRealDamageText, -info.RealDamage);
            mRealDamagePerSlider.value = info.RealDamagePer;
            SetNumber(mRealCureText, info.RealCure);
            mRealCurePerSlider.value = info.RealCurePer;
            SetPercent(mRealCureMaxHpPerText, info.RealCureMaxHpPer);
            SetNumber(mRealShieldText, info.RealShield);
            mRealShieldPerSlider.value = info.RealShieldPer ?? 0f;
            SetPercent(mRealShieldMaxHpPerText, info.RealShieldMaxHpPer);
            SetNumber(mApplyBuffStatusCountText, info.ApplyBuffStatusCount);
            SetNumber(mApplyDebuffStatusCountText, info.ApplyDebuffStatusCount);
            SetNumber(mDamageCountText, info.DamageCount);
            SetNumber(mCureCountText, info.CureCount);
            mGMStateSwitcher.SetState(gm);
        }

        private void SetNumber(WorldMultiLanguageText text, long? value)
        {
            text.Color = !value.HasValue || value.Value == 0 ? mZeroTextColor : mNormalTextColor;
            text.SetKey("{0}", value.HasValue ? WorldBattleReportNumberFormat.Compact(value.Value) : string.Empty);
        }
        private void SetAverage(WorldMultiLanguageText text, float? value)
        {
            text.Color = !value.HasValue || value.Value < 1E-05f ? mZeroTextColor : mNormalTextColor;
            // User-confirmed integer display; the report/checkpoint retain the original ratio.
            text.SetKey("{0}", value.HasValue ? WorldBattleReportNumberFormat.Significant(Mathf.RoundToInt(value.Value)) : string.Empty);
        }
        private static void SetPercent(WorldMultiLanguageText text, float? value) => text.SetKey("{0}",
            value.HasValue ? WorldBattleReportNumberFormat.Percent(value.Value) : string.Empty);
    }
}
