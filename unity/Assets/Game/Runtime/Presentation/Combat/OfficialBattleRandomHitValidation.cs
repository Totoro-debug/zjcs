using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Isolated input for the public weekly host; production skill/range rules are unchanged.
    public sealed class OfficialBattleRandomHitValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finishingAttack;
        private bool settled, animated, captured;
        private int nativeCasts;
        private float castTime;
        private readonly bool distinctSupport;
        private int SkillId => distinctSupport ? 13990 : 13660;
        public string CaptureName { get; private set; }

        private OfficialBattleRandomHitValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finishingAttack, bool distinctSupport)
        { this.catalog = catalog; this.finishingAttack = finishingAttack; this.distinctSupport = distinctSupport; }

        public static OfficialBattleRandomHitValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var source = checkpoint.Units.Single(value => value.Id == 1);
            var target = checkpoint.Units.First(value => value.Id != 1);
            bool distinct = Environment.GetEnvironmentVariable("ZJCS_RANDOM_HIT_DISTINCT") == "1";
            var probe = new OfficialBattleRandomHitValidation(catalog, source.Skills.First(value => value.Slot == 0), distinct);
            target.MonsterId = 3665; target.MonsterGroupNo = 0;
            target.CharacterType = catalog.MonsterCharacterType(3665);
            target.MonsterType = catalog.MonsterType(3665); target.AiType = catalog.MonsterAiType(3665);
            target.MaxHpPlies = catalog.MonsterHpPliesFor(3665, 0, target.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, target);
            Require(target.BodyOffsets.Count == 9, "Original nine-cell target identity is unavailable.");
            source.X = target.X - 2; source.Y = target.Y;
            Require(checkpoint.Grid.Contains(new FightGridPoint(source.X, source.Y)), "Source is outside the battlefield.");
            if (distinct)
            {
                int nextId = checkpoint.Units.Max(value => value.Id) + 1;
                foreach (var position in new[] {new FightGridPoint(source.X - 2, source.Y),
                    new FightGridPoint(source.X, source.Y + 3)})
                {
                    var ally = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(target));
                    ally.Id = ally.Turn.Id = nextId++; ally.Side = source.Side;
                    ally.X = position.X; ally.Y = position.Y;
                    Require(checkpoint.Grid.Contains(position), "Distinct-target ally is outside the battlefield.");
                    checkpoint.Units.Add(ally);
                }
            }
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == source ? 1000 : 1;
                    if (property.Id == catalog.PropertyType("DodgePercent")
                        || property.Id == catalog.PropertyType("BlindingPercent")) property.Value = 0;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 1000000000;
                unit.Turn.Speed = unit == source ? 1000 : 1;
            }
            var properties = catalog.CalculateSkillProps(probe.SkillId, 5, 20, source.SubRank)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
            var skill = catalog.CreateSkill(probe.SkillId, 0, distinct ? 10211 : 10188, 5, 20, source.SubRank, properties);
            skill.LimitedTimes = 1;
            source.Skills.Add(skill); source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
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
                if (cast.ActorId != 1 || cast.SkillEntityId != SkillId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
                var source = controller.TryGetUnitTransform(1);
                var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
                Require(renderer, "The source is not the original native player.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == (distinctSupport ? "100/skill_31" : "200/skill_45")
                        && time > .04f && weight > 0) animated = true;
                };
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (settled) return;
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            if (!step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == SkillId)) return;
            var source = checkpoint.Units.Single(value => value.Id == 1);
            var hits = step.DamageEvents.Where(value => value.SourceId == 1 && value.SkillEntityId == SkillId).ToArray();
            if (distinctSupport)
            {
                var allies = checkpoint.Units.Where(value => value.Side == source.Side).ToArray();
                Require(allies.Length == 3 && hits.Length == 3
                    && hits.Select(value => value.TargetId).Distinct().Count() == 3
                    && allies.All(value => value.Statuses.Count(status => status.ClassId == 24719) == 1),
                    "Original 13992 spent distinct-target quota on one ally's body cells or lost 24719.");
            }
            else Require(hits.Length > 9 && new[] {13662, 13663, 13664}.All(id => hits.Any(value => value.HitEntityId == id))
                && hits.All(value => value.HasHitGridPosition && value.Damage < 0 && value.AfterHp < value.BeforeHp),
                "The three original random hit nodes did not settle all covered body cells.");
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(saved.RandomSeed == checkpoint.RandomSeed
                && saved.Units.Single(value => value.Id == 1).SkillState.Slots[0].ReleasedTimes == 1
                && saved.Receipts.Last().Event.DamageEvents.Count(value => value.SkillEntityId == SkillId) == hits.Length
                && (!distinctSupport || saved.Units.Where(value => value.Side == source.Side)
                    .All(value => value.Statuses.Count(status => status.ClassId == 24719) == 1)),
                "Random stream, one ordinary release or body-cell events were lost in the checkpoint.");
            Debug.Log(distinctSupport
                ? "[Random hit] original 13990/13992: source and both nine-cell allies receive 24719; one release and checkpoint passed."
                : "[Random hit] original 13660/10188: " + hits.Length
                    + " body-cell hits across three random nodes; one release and checkpoint passed.");
            settled = true;
            source.Skills.Clear(); source.SkillState.Slots.Clear(); source.Skills.Add(finishingAttack);
            source.SkillState.Slots.Add(catalog.CreateSkillSlot(finishingAttack, 0));
            foreach (var property in source.Properties.Concat(source.BaseProperties ?? source.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || !animated || nativeCasts == 0 || Time.realtimeSinceStartup - castTime < .6f) return;
            var source = controller.TryGetUnitTransform(1);
            if (!source || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                && value.isVisible && !value.forceRenderingOff
                && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "random-hit-" + SkillId;
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(settled && animated && captured && nativeCasts == 1 && checkpoint.Finished,
                "Original random skill, native action, simulated input or settlement was incomplete.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Random-hit diagnostic: " + message); }
    }
}
