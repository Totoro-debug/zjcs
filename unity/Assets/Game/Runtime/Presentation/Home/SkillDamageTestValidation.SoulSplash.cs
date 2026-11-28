using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static AcquiredSoulSplash soulSplash;

        private static int FighterActiveCompanion(int active)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if (active != 10008 || string.IsNullOrWhiteSpace(path)) return 0;
            var source = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            return source.Player.Wardrobe.Profession == 7
                && source.Player.Skills.Plans[source.Player.Skills.SelectedPlan].PassiveSkills[0] == 10545 ? 10545 : 0;
        }

        [Serializable]
        private sealed class SoulSplashReceipt
        {
            public int victim, parentHits, kills, splashHits, comparisons;
            public long overflow, customDamage;
            public bool modifier, restored, captured;
        }

        private sealed class AcquiredSoulSplash
        {
            internal readonly SoulSplashReceipt Receipt = new SoulSplashReceipt();
            private readonly string output;
            private string previousCheckpoint;
            private float maximumDeltaTime;

            internal AcquiredSoulSplash(string output) { this.output = output; }

            internal void Configure(FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var enemies = checkpoint.Units.Where(unit => unit.Side != actor.Side && unit.Hp > 0).ToArray();
                Require(enemies.Length > 1 && checkpoint.DamageTest != null
                    && !checkpoint.DamageTest.EnableMonsterUnlimitHp && !checkpoint.IsPvp,
                    "Soul Splash requires the original group formation and normal monster HP selected through the UI.");
                // Preserve roster order and check the complete authored bodies.
                var victim = enemies.Where(unit => enemies.Any(other => other != unit
                        && unit.BodyOffsets.Any(from => other.BodyOffsets.Any(to =>
                            Math.Abs(other.X + to.X - unit.X - from.X)
                                + Math.Abs(other.Y + to.Y - unit.Y - from.Y) <= 2))))
                    .OrderBy(unit => unit.Id)
                    .FirstOrDefault();
                Require(victim != null, "The original formation has no adjacent splash recipients.");
                Receipt.victim = victim.Id;
                foreach (var unit in enemies)
                {
                    // Explicit isolated diagnostic: retain the original roster,
                    // positions, bodies, AI and render bindings; supply HP only.
                    if (unit == victim) unit.Hp = unit.Turn.Hp = 1;
                    else
                    {
                        SetAcquiredBattleProperty(unit, "MaxHp", 1000000000);
                        unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = 1000000000;
                    }
                }
            }

            internal void Prepare(FightBattleCheckpoint checkpoint)
            {
                previousCheckpoint = FightBattleCheckpointSerializer.Save(checkpoint);
                maximumDeltaTime = Time.maximumDeltaTime;
                Time.maximumDeltaTime = Mathf.Min(maximumDeltaTime, 1f / 60f);
            }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25024);
                var bonus = actor.Statuses.SingleOrDefault(status => status.ClassId == 24471);
                if (bonus != null)
                {
                    Require(bonus.ApplyingSkillItemId == 10545 && bonus.CreatorId == actor.Id
                        && bonus.SourceSkillRank == listener.SourceSkillRank && bonus.SourceSkillLevel == 2
                        && bonus.SourceSubRank == actor.SubRank && listener.Components.Start.Started,
                        "The real single-segment modifier lost its startup or inherited cultivation.");
                    Receipt.modifier = true;
                }
                var parents = step.DamageEvents.Where(hit => hit.SourceId == actor.Id
                    && hit.SkillItemId == 10008 && !hit.IsCure && hit.HasDamageValue && hit.DamageValue < 0).ToArray();
                Receipt.parentHits += parents.Length;
                if (parents.Length > 0)
                {
                    var control = FightBattleCheckpointSerializer.Load(previousCheckpoint);
                    Require(control.Units.Single(unit => unit.Id == 1).Statuses.RemoveAll(status => status.ClassId == 24471) == 1,
                        "The single-segment damage comparison did not begin after the original status startup.");
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    FightStatusPropertyRuntime.RebuildAll(control.Units, assets.Load(), assets.LoadDamageFormula());
                    var controlExecutor = assets.RestoreBattle(control);
                    double elapsed = (checkpoint.StatusSeconds?.ElapsedSeconds ?? 0d)
                        - (control.StatusSeconds?.ElapsedSeconds ?? 0d);
                    Require(elapsed >= 0d, "The splash comparison cannot move the gameplay clock backwards.");
                    controlExecutor.AdvanceStatusSeconds(elapsed);
                    var comparison = controlExecutor.Step(control.Sequence, control.Counter + 1).Event;
                    foreach (var parent in parents)
                    {
                        var bare = comparison.DamageEvents.Single(hit => hit.SourceId == actor.Id
                            && hit.SkillItemId == 10008 && hit.TargetId == parent.TargetId && hit.HitEntityId == parent.HitEntityId);
                        Require(parent.DamageValue < bare.DamageValue,
                            "Removing the cultivated single-segment modifier did not lower real physical damage.");
                        Receipt.comparisons++;
                    }
                }
                var lethal = parents.Where(hit => hit.BeforeHp > 0 && hit.AfterHp == 0).ToArray();
                var children = step.SkillCasts.Where(cast => cast.ActorId == actor.Id && cast.SkillEntityId == 11280).ToArray();
                Require(lethal.Length == children.Length, "The actual kill lost a child or recursively splashed.");
                foreach (var parent in lethal)
                {
                    Require(parent.TargetId == Receipt.victim && parent.HasDamagePosition,
                        "The isolated victim or frozen death cell changed.");
                    var child = children.Single(cast => Math.Abs(cast.Delay - parent.Delay) < .0001f);
                    Require(child.SkillItemId == 10545 && child.Slot < 0 && child.Presentation.TargetX == parent.DamageX
                        && child.Presentation.TargetY == parent.DamageY,
                        "The kill child lost its passive source, actual hit time or death cell.");
                    long overflow = Math.Max(0L, -parent.DamageValue - parent.BeforeHp);
                    long coefficient = listener.StatusProperties.Single(value => value.Id == 233).Value;
                    long expected = (long)(overflow * (coefficient / 10000.0));
                    var hits = step.DamageEvents.Where(hit => child.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    Require(expected > 0 && hits.Length > 0 && hits.All(hit => hit.SourceId == actor.Id
                        && hit.TargetId != Receipt.victim && hit.SkillItemId == 10545 && hit.HitEntityId == 11281
                        && hit.DamageEntityId == 10023 && hit.DamageType == 8 && hit.ElementType == 102 && !hit.IsCure
                        && hit.HasDamageValue && hit.DamageValue == -expected && hit.AfterHp < hit.BeforeHp),
                        "The real splash lost its post-shield cultivated fire true damage or living recipients.");
                    Receipt.kills++; Receipt.splashHits += hits.Length;
                    Receipt.overflow = overflow; Receipt.customDamage = expected;
                }
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                    .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring Soul Splash repeated a start component, kill or child damage.");
                Receipt.restored = true; previousCheckpoint = saved;
                File.WriteAllText(Path.Combine(output, "companion-soul-splash-checkpoint.json"), saved);
            }

            internal void Complete(bool captured)
            {
                Time.maximumDeltaTime = maximumDeltaTime;
                Receipt.captured = captured;
                File.WriteAllText(Path.Combine(output, "companion-soul-splash-receipt.json"), JsonUtility.ToJson(Receipt, true));
                Require(Receipt.modifier && Receipt.kills == 1 && Receipt.splashHits > 0
                    && Receipt.comparisons > 0 && Receipt.restored && Receipt.captured,
                    "Soul Splash must strengthen a real single-segment parent and render its sourced fire splash after a real kill.");
                Debug.Log("[Acquired passive combat] 10545 cultivated single-segment bonus, actual post-shield kill splash, native fire performance and restoration passed.");
            }
        }
    }
}
