using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Official.BattleResultUI
{
    // Selected GameEndPlayerSettleGroup and FightUtilsHandler report sums.
    public sealed class GameEndPlayerSettleGroup : MonoBehaviour
    {
        [SerializeField] private GameObject mSettlePrefab;
        [SerializeField] private bool mNeedCross;
        private readonly List<GameEndPlayerSettleItem> items = new List<GameEndPlayerSettleItem>();
        private IBattleBossResultHost host;
        public void Bind(IBattleBossResultHost value) => host = value ?? throw new ArgumentNullException(nameof(value));
        public void RefreshSettles(BattleResultData result)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            items.Clear();
            var groups = FightBattleReportGrouping.Build(result.Report, 1).Players;
            long total = 0;
            foreach (var group in groups) total += Damage(result.StageType, group);
            int index = 0;
            foreach (var group in groups)
            {
                long damage = Damage(result.StageType, group);
                var item = Instantiate(mSettlePrefab, transform, false).GetComponent<GameEndPlayerSettleItem>();
                host.BindPlayerRow(item.gameObject);
                item.Bind(host);
                items.Add(item);
                item.Init(result.StageType, result.GameId, host.ResolvePlayer(group.TargetId), damage,
                    total == 0 ? 0f : 1f * damage / total, mNeedCross && index % 2 == 1);
                index++;
            }
        }
        public void OnThumbUpReportSwitch(bool showing)
        {
            foreach (var item in items)
                if (item != null && !item.IsNpc && (showing || !host.IsTeamMember(item.PlayerId)))
                    item.OnThumbUpReportSwitch(showing);
        }
        private static long Damage(int stageType, FightBattleReportPlayerGroup group)
        {
            long value = 0;
            foreach (var list in new[] { group.Normal, group.Summon, group.Pet })
                foreach (var unit in list)
                    value += stageType == 2 ? unit.DamageDealt : unit.DamageAndShieldDamageDealt;
            return value;
        }
    }
}
