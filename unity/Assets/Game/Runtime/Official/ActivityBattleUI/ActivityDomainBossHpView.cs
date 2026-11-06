using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Extracted HP presentation from ActivityDomainBossPanelBattleContent.RefreshContent.
    // The original field references are attached to the original HpSliderGo subtree.
    public sealed class ActivityDomainBossHpView : MonoBehaviour
    {
        [SerializeField] private Slider mHpSlider;
        [SerializeField] private WorldMultiLanguageText mHpText;

        public void Refresh(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            string text = WorldBattleReportNumberFormat.Percent(ratio);
            mHpText.Format(text);
            mHpSlider.value = ratio;
        }
    }
}
