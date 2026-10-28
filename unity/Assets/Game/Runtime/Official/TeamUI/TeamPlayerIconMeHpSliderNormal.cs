using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    public interface ITeamHpSpriteSlider
    {
        float MinValue { set; }
        float MaxValue { set; }
        float Value { set; }
    }

    // Original PlayerIconMeHpSliderNormal and its abstract Init contract.
    [DisallowMultipleComponent]
    public sealed class TeamPlayerIconMeHpSliderNormal : TeamPlayerUIBase, IProfessionPlayerInfoHpSlider
    {
        public Slider HpSlider;
        public Component HpSpriteSlider;
        private bool mUpdateFlag = true;
        private void Awake()
        {
            if (HpSlider != null) { HpSlider.minValue = 0f; HpSlider.maxValue = 1f; }
            if (HpSpriteSlider != null)
            { ((ITeamHpSpriteSlider)HpSpriteSlider).MinValue = 0f; ((ITeamHpSpriteSlider)HpSpriteSlider).MaxValue = 1f; }
        }
        public void Init() { }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            Services.HpUpdated += OnHpUpdate;
            UpdateValue();
        }
        protected override void OnDisableAndInited()
        {
            if (Services.MainPlayerAvailable) Services.HpUpdated -= OnHpUpdate;
            base.OnDisableAndInited();
        }
        private void Update()
        { if (Services.MainPlayerAvailable && Services.MapCount <= 0 && mUpdateFlag) UpdateValue(); }
        private void OnHpUpdate(float hp) { UpdateValue(); }
        private void UpdateValue()
        {
            if (HpSlider != null) HpSlider.value = Services.HpPercent;
            if (HpSpriteSlider != null) ((ITeamHpSpriteSlider)HpSpriteSlider).Value = Services.HpPercent;
            mUpdateFlag = Services.HpPercent < 1f;
        }
    }
}
