using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Controlled input for the public host; never changes a user's skill unlocks.
    public sealed class OfficialBattleHitFinishValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finishingAttack;
        private readonly string cureMode;
        private bool settled, animated, captured;
        private int nativeCasts, nativeCures;
        private float castTime;
        public string CaptureName { get; private set; }

        private OfficialBattleHitFinishValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finishingAttack, string cureMode)
        { this.catalog = catalog; this.finishingAttack = finishingAttack; this.cureMode = cureMode; }

        public static OfficialBattleHitFinishValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            string mode = Environment.GetEnvironmentVariable("ZJCS_CURE_INTERCEPTION_ACCEPTANCE") ?? "normal";
            Require(mode == "normal" || mode == "conversion" || mode == "full" || mode == "banned",
                "Unknown cure diagnostic mode.");
            var probe = new OfficialBattleHitFinishValidation(catalog, player.Skills.First(value => value.Slot == 0), mode);
            enemy.MonsterId = 3665; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(enemy.MonsterId);
            enemy.MonsterType = catalog.MonsterType(enemy.MonsterId);
            enemy.AiType = catalog.MonsterAiType(enemy.MonsterId);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(enemy.MonsterId, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            Require(enemy.BodyOffsets.Count == 9, "The original large target lost its body cells.");
            player.X = enemy.X; player.Y = enemy.Y - 2;
            Require(checkpoint.Grid.Contains(new FightGridPoint(player.X, player.Y)), "Source is outside the battlefield.");
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long fixtureMaxHp = unit == player ? 5000000 : 1000000000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = fixtureMaxHp;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == player ? 1000 : 1;
                    if (property.Id == catalog.PropertyType("DodgePercent")
                        || property.Id == catalog.PropertyType("BlindingPercent")) property.Value = 0;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = fixtureMaxHp;
                unit.Turn.Speed = unit == player ? 1000 : 1;
            }
            player.Hp = player.Turn.Hp = 2500000;
            player.Statuses.Add(catalog.CreateStatus(24721, player.Id, player.Id, 9913960));
            if (mode != "normal")
                player.Statuses.Add(catalog.CreateStatus(25136, player.Id, player.Id, 9913961,
                    13970, 10209, 7, 30, player.SubRank));
            if (mode == "full") player.Hp = player.Turn.Hp = player.MaxHp;
            if (mode == "banned")
                player.Statuses.Add(catalog.CreateStatus(24223, player.Id, player.Id, 9913962));
            var skill = catalog.CreateSkill(13960, 0, 10208, 5, 20, player.SubRank,
                catalog.CalculateSkillProps(13960, 5, 20, player.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 8;
            return probe;
        }

        public void BindPresentation(OfficialBattleRuntimeController controller)
        {
            var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            entities.CastStarted += cast => {
                if (cast.ActorId != 1) return;
                if (cast.SkillEntityId == 13965) { nativeCures++; return; }
                if (cast.SkillEntityId != 13960) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
                var source = controller.TryGetUnitTransform(1);
                var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
                Require(renderer, "Source has no original player renderer.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == "100/skill_74" && time > .04f && weight > 0) animated = true;
                };
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (settled) return;
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            if (!step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == 13960)) return;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var hits = step.DamageEvents.Where(value => value.SourceId == 1 && value.SkillEntityId == 13960).ToArray();
            var cures = step.DamageEvents.Where(value => value.SourceId == 1 && value.SkillEntityId == 13965).ToArray();
            decimal lostHp = hits.Sum(value => Math.Max(0m, (decimal)value.BeforeHp - value.AfterHp));
            Require(hits.Length == 6 && hits.All(value => value.HasHitGridPosition && value.Damage < 0)
                && cures.Length == 1 && cures[0].IsCure && cures[0].TargetId == player.Id && lostHp > 0m,
                "The six body cells did not produce one whole-Hit recovery; hits=" + hits.Length
                    + ", recoveries=" + cures.Length + ", actual HP removed=" + lostHp + ".");
            // The node supplies a base cure; ordinary cure modifiers still belong
            // to the existing formula and are covered independently by rule tests.
            var shields = player.Statuses.Where(value => value.ClassId == 24435).ToArray();
            if (cureMode == "normal")
                Require(cures[0].Damage > 0 && cures[0].BeforeHp < cures[0].AfterHp && shields.Length == 0,
                    "Ordinary recovery did not increase HP.");
            else
            {
                Require(cures[0].Damage == 0 && cures[0].BeforeHp == cures[0].AfterHp,
                    "Intercepted recovery changed HP.");
                if (cureMode == "banned") Require(shields.Length == 0, "Prohibited recovery created a shield.");
                else Require(shields.Length == 1 && shields[0].ShieldRemainValue > 0
                    && shields[0].CustomShieldValue == shields[0].ShieldRemainValue,
                    "Recovery was not converted to an original custom shield.");
                if (cureMode == "full") Require(player.Hp == player.MaxHp, "Full-HP conversion changed HP.");
            }
            Require(player.SkillState.Slots[0].ReleasedTimes == 1, "Recovery spent an extra equipped release.");
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(saved.RandomSeed == checkpoint.RandomSeed && saved.Units.Single(value => value.Id == 1).Hp == player.Hp
                && saved.Receipts.Last().Event.DamageEvents.Count(value => value.SkillEntityId == 13965) == 1,
                "Recovery/checkpoint restore differs.");
            Require(saved.Units.Single(value => value.Id == 1).Statuses.Where(value => value.ClassId == 24435)
                .Select(value => value.ShieldRemainValue).SequenceEqual(shields.Select(value => value.ShieldRemainValue)),
                "Converted shield/checkpoint restore differs.");
            Debug.Log("[Hit finish] original 13960: six body-cell hits / " + lostHp
                + " actual HP removed; one 13965 recovery / " + cures[0].Damage + "; cure mode " + cureMode
                + "; shield " + shields.Sum(value => value.ShieldRemainValue) + "; one release and checkpoint passed.");
            settled = true;
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            player.Skills.Add(finishingAttack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(finishingAttack, 0));
            foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || !settled || !animated || nativeCasts != 1 || nativeCures != 1
                || Time.realtimeSinceStartup - castTime < 1.15f) return;
            var source = controller.TryGetUnitTransform(1);
            if (!source || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                && value.isVisible && !value.forceRenderingOff
                && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "hit-finish-13960-" + cureMode;
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(settled && animated && captured && nativeCasts == 1 && nativeCures == 1 && checkpoint.Finished,
                "Whole-Hit recovery, native source/child, animation, input or settlement was incomplete.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Hit-finish diagnostic: " + message); }
    }
}
