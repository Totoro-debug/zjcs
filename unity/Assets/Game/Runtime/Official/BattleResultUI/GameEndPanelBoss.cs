using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Official.BattleResultUI
{
    // Selected GameEndPanelBoss. Prefab loading and localization cross existing UI ports.
    public sealed class GameEndPanelBoss : UIBase
    {
        public WorldMultiLanguageText BossNameText;
        public WorldMultiLanguageText DamageText;
        public OfficialBattleAwardList AwardContent;
        public Transform BossSlot, NormalSlot;
        public GameEndPlayerSettleGroup PlayerSettleGroup;
        private BattleResultData result;
        private IBattleBossResultHost host;
        public void Bind(IBattleBossResultHost value)
        {
            host = value ?? throw new ArgumentNullException(nameof(value));
            PlayerSettleGroup?.Bind(value);
        }
        public void Init(BattleResultData value) => InitWrapper(() => result = value);
        public void RefreshAward(IReadOnlyList<OfficialBattleAwardEntry> awards)
        {
            if (awards == null || awards.Count == 0 || AwardContent == null) return;
            AwardContent.gameObject.SetActive(true);
            AwardContent.Init(awards);
        }
        public void RefreshDamage(long damage)
        {
            if (DamageText != null) DamageText.Format(CultureNumberArg.Compact(damage));
            if (PlayerSettleGroup != null) PlayerSettleGroup.RefreshSettles(result);
        }
        public void OnThumbUpReportSwitch(bool showing) => PlayerSettleGroup?.OnThumbUpReportSwitch(showing);
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            BattleResultBoss boss = host.GetBoss(result.StageId);
            if (boss.Prefab != null)
            {
                foreach (Transform child in BossSlot) Destroy(child.gameObject);
                foreach (Transform child in NormalSlot) Destroy(child.gameObject);
                // UIUtils.Instantiate(... KeepScale) restores the source prefab scale.
                Instantiate(boss.Prefab, boss.IsNormal ? NormalSlot : BossSlot, false);
            }
            BossNameText.SetKey(boss.StageNameKey);
        }
        protected override void OnDisableAndInited()
        {
            if (AwardContent != null) AwardContent.gameObject.SetActive(false);
            result = null;
            base.OnDisableAndInited();
        }
    }
}
