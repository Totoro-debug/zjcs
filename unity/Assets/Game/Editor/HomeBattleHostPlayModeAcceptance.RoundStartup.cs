using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static RoundStartupProbe ConfigureRoundStartupProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_STARTUP_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit == player ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit != player && property.Id == catalog.PropertyType("Attack"))
                        property.Value = Math.Min(property.Value, 100L);
                }
                unit.MaxHp = unit.LastMaxHp = hp;
                unit.Hp = unit.Turn.Hp = unit == player ? hp / 2 : hp;
            }
            var source = catalog.CreateStatus(25077, 1, 1, 9925077, 0, 0, 5, 20, 1, null);
            player.Statuses.Add(source);
            return new RoundStartupProbe(source);
        }

        private sealed class RoundStartupProbe
        {
            private readonly FightBattleStatusState source;
            private int calls;
            public bool ReadyToCapture => calls > 0;
            public RoundStartupProbe(FightBattleStatusState source) { this.source = source; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                var step = cp.Receipts.Last().Event;
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 18860).ToArray();
                if (casts.Length == 0) return;
                calls++;
                Require(casts.Length == 1 && casts[0].ActorId == 1 && casts[0].TargetIds.Contains(1)
                    && source.RoundStartTriggeredCount == calls && calls <= 4,
                    "Original 25077 lost its configured startup/round invocation count or self target.");
                Require(player.SkillState.Round == calls - 1
                    && player.Statuses.Contains(source) == (calls < 4),
                    "Original 25077 consumed a turn at startup or remained after its final invocation.");
                Require(step.DamageEvents.Any(value => value.SkillEntityId == 18860 && value.IsCure),
                    "Original 18860 lost its actual healing event.");
                Debug.Log("[Round startup hosts] 25077 -> 18860 call=" + calls
                    + ", personalRound=" + player.SkillState.Round + ", remainingSource=" + player.Statuses.Contains(source));
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(calls == 4 && source.RoundStartStartChecked,
                    host + " did not execute its four original round-start calls.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its round-start checkpoint after reload.");
                Debug.Log("[Round startup hosts] " + host + ": startup without personal turn; fourth call removed source; checkpoint passed.");
            }
        }
    }
}
