using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static AcquiredSageRoundSecret BeginAcquiredSageRoundSecretValidation(int item, string output)
            => item == 10610 || item == 10611 ? new AcquiredSageRoundSecret(item, output) : null;

        private sealed class AcquiredSageRoundSecret
        {
            private readonly int item;
            private readonly string output;
            private readonly int[] gains;
            private readonly HashSet<int> seen = new HashSet<int>();
            private readonly HashSet<int> captured = new HashSet<int>();
            private int round, turns, rank;
            private bool restored;

            public AcquiredSageRoundSecret(int item, string output)
            {
                this.item = item;
                this.output = output;
                gains = item == 10610 ? new[] { 24395 } : new[] { 24396, 24398, 24399 };
            }

            public void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(value => value.Id == 1);
                if (step.ActorId != actor.Id || actor.SkillState.Round <= round) return;
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                File.WriteAllText(Path.Combine(output, "companion-sage-round-" + item + "-checkpoint.json"), saved);
                var listener = actor.Statuses.SingleOrDefault(value => value.ClassId == (item == 10610 ? 25119 : 25120)
                    && value.ApplyingSkillItemId == item);
                var gain = actor.Statuses.SingleOrDefault(value => gains.Contains(value.ClassId));
                Require(listener != null && gain != null, "The acquired personal-round listener or gain is missing.");
                Require(gain.OwnerId == actor.Id && gain.CreatorId == actor.Id && gain.ApplyingSkillItemId == item
                    && gain.SourceSkillRank == listener.SourceSkillRank && gain.SourceSkillLevel == 2
                    && gain.SourceSubRank == actor.SubRank && gain.DurationRound == 1
                    && gain.StartRound == actor.SkillState.Round && gain.CanDispersed == (item == 10611),
                    "The acquired round-secret gain lost its source, cultivation, lifetime or dispel rule.");
                var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
                var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
                var cultivated = catalog.CalculateSkillProps(gain.ClassId, listener.SourceSkillRank, 2, actor.SubRank)
                    .ToDictionary(value => value.Key, value => value.Value);
                foreach (var pair in cultivated)
                {
                    Require(gain.StatusProperties.Single(value => value.Id == pair.Key).Value == pair.Value,
                        "The acquired round-secret gain changed its cultivated status property.");
                    if (item == 10610)
                    {
                        long baseline = actor.BaseProperties.FirstOrDefault(value => value.Id == pair.Key)?.Value ?? 0;
                        Require(actor.Properties.Single(value => value.Id == pair.Key).Value == baseline + pair.Value,
                            "The acquired blessing omitted its actual cultivated damage increase.");
                    }
                }
                if (item == 10611)
                {
                    bool checkedFinalProperty = false;
                    // FightStatusProp consumes scale properties into the final
                    // stat, then applies its fixed addition. The scale itself
                    // is no longer present in the actor's property list.
                    foreach (string name in new[] { "Attack", "Defence", "Speed" })
                    {
                        double scale = formula.ReadProperty(cultivated, formula.PropertyId(name + "Scale"));
                        double fixedValue = formula.ReadProperty(cultivated, formula.PropertyId("Fixed" + name));
                        if (scale == 0 && fixedValue == 0) continue;
                        checkedFinalProperty = true;
                        int id = formula.PropertyId(name);
                        long baseline = actor.BaseProperties.FirstOrDefault(value => value.Id == id)?.Value ?? 0;
                        long reference = baseline + (actor.BaseProperties.FirstOrDefault(
                            value => value.Id == formula.PropertyId("Base" + name))?.Value ?? 0);
                        long expected = (scale == 0 ? baseline : Math.Max(1L, baseline + (long)(reference * scale)))
                            + (long)fixedValue;
                        Require(actor.Properties.Single(value => value.Id == id).Value == expected,
                            "The acquired dark-star gain changed its final " + name + " calculation.");
                    }
                    Require(checkedFinalProperty, "The acquired dark-star gain did not improve a final property.");
                }
                if (item == 10610)
                {
                    var cast = step.SkillCasts.Single(value => value.SkillEntityId == 12180);
                    Require(cast.SkillItemId == item && cast.TargetIds.SequenceEqual(new[] { actor.Id }),
                        "The acquired blessing lost its original support child or eligible role.");
                }
                var copy = FightBattleCheckpointSerializer.Load(saved);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(copy);
                Require(executor.Step(copy.Sequence, copy.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(copy) == saved,
                    "The acquired personal-round secret repeated or changed after restoring its receipt.");
                restored = true;
                rank = listener.SourceSkillRank;
                round = actor.SkillState.Round;
                turns++;
                seen.Add(gain.ClassId);
                WriteReceipt();
            }

            public int ReadyCapture()
            {
                string prefix = item == 10610 ? "playable_24395" : "playable25120_common";
                if (!IsNativeSkillParticleVisible(prefix)) return 0;
                foreach (int id in gains)
                    if (seen.Contains(id) && !captured.Contains(id)
                        && UnityEngine.Object.FindObjectsOfType<Transform>().Any(value => value.name == id.ToString()
                            || value.name.StartsWith(id + "(", StringComparison.Ordinal))) return id;
                return 0;
            }

            public void Captured(int id)
            {
                Require(gains.Contains(id), "The round-secret capture has an unexpected status.");
                captured.Add(id);
                WriteReceipt();
            }

            public void Complete()
            {
                Require(turns >= 2 && restored && seen.SetEquals(gains) && captured.SetEquals(gains),
                    "The acquired round secret did not exercise and render every gain across personal turns.");
                WriteReceipt();
                Debug.Log("[Acquired passive combat] " + item + " cultivated personal-round gains, actual properties, native effects and restore replay passed.");
            }

            private void WriteReceipt() => File.WriteAllText(
                Path.Combine(output, "companion-sage-round-" + item + "-receipt.json"), JsonUtility.ToJson(
                    new SageRoundSecretReceipt { item = item, rank = rank, level = 2, turns = turns,
                        restored = restored, seen = seen.OrderBy(value => value).ToArray(),
                        captured = captured.OrderBy(value => value).ToArray() }, true));
        }

        [Serializable]
        private sealed class SageRoundSecretReceipt
        {
            public int item, rank, level, turns;
            public bool restored;
            public int[] seen, captured;
        }
    }
}
