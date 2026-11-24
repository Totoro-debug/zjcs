using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static AcquiredGuardianProbe acquiredGuardian;

        private static bool HasGuardianCompanion(int active)
        {
            if (active != 10032) return false;
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if (string.IsNullOrWhiteSpace(path)) return false;
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            return save.Player.Wardrobe.Profession == 4
                && save.Player.Skills.Plans[save.Player.Skills.SelectedPlan].PassiveSkills[0] == 10593;
        }

        private static void BeginAcquiredGuardianValidation(int item, string output)
        {
            acquiredGuardian = item == 10593 || item == 10575 || item == 10580 || item == 10572
                ? new AcquiredGuardianProbe(item, output) : null;
        }

        internal static void ConfigureGuardianBattleRequest(OfficialBattleRequest request)
        {
            acquiredGuardian?.Configure(request);
        }

        private static FightBattleUnitState AcquiredGuardianStatusOwner(FightBattleCheckpoint state,
            FightBattleUnitState actor, int item)
        {
            return item == 10593 ? state.Units.Single(unit => unit.Id == acquiredGuardian.AllyId) : actor;
        }

        private sealed partial class AcquiredGuardianProbe
        {
            private readonly int item;
            private readonly string output;
            private readonly OfficialCombatAssetCatalog assets;
            public int AllyId { get; private set; }
            private bool verified;

            public AcquiredGuardianProbe(int item, string output)
            {
                this.item = item;
                this.output = output;
                assets = OfficialCombatAssetCatalog.LoadDefault();
            }

            public void Configure(OfficialBattleRequest request)
            {
                var checkpoint = request.Checkpoint;
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                if (item == 10572)
                {
                    actor.Hp = Math.Max(1L, actor.MaxHp / 2L);
                    actor.Turn.Hp = actor.Hp;
                    return;
                }
                if (item == 10575)
                {
                    ConfigureSoulGuard(actor);
                    return;
                }
                Require(actor.Statuses.Any(status => status.ClassId == (item == 10580 ? 25074 : 25109)
                    && status.ApplyingSkillItemId == item), "The guardian fixture lacks its actually equipped secret.");
                var ally = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(actor));
                AllyId = checkpoint.Units.Max(unit => unit.Id) + 1;
                ally.Id = ally.MasterId = AllyId;
                ally.Hp = Math.Max(1L, ally.MaxHp / 2);
                ally.Turn.Id = AllyId;
                ally.Turn.MasterId = ally.MasterId;
                ally.Turn.Hp = ally.Hp;
                ally.Turn.Speed = Math.Max(1, actor.Turn.Speed - 1);
                ally.Skills.Clear(); ally.SkillState.Slots.Clear(); ally.ReportSkillSnapshots.Clear();
                ally.Statuses.Clear(); ally.PetEnergy = new FightBattlePetEnergyState();
                FightGridPoint[] offsets = { new FightGridPoint(1, 0), new FightGridPoint(-1, 0),
                    new FightGridPoint(0, 1), new FightGridPoint(0, -1) };
                if (item == 10580)
                    offsets = offsets.OrderBy(offset => checkpoint.Units.Where(unit => unit.Side != actor.Side)
                        .Min(enemy => Math.Abs(actor.X + offset.X - enemy.X)
                            + Math.Abs(actor.Y + offset.Y - enemy.Y))).ToArray();
                bool placed = false;
                foreach (var offset in offsets)
                {
                    int x = actor.X + offset.X, y = actor.Y + offset.Y;
                    if (x < 0 || y < 0 || x >= checkpoint.Grid.Width || y >= checkpoint.Grid.Height
                        || checkpoint.Units.Any(unit => unit.X == x && unit.Y == y)) continue;
                    ally.X = x; ally.Y = y; placed = true; break;
                }
                Require(placed, "The controlled allied adventurer has no adjacent cell.");
                checkpoint.Units.Add(ally);
                var original = request.PlayerAppearance;
                Require(original != null, "The native player appearance source is absent.");
                var allyAppearance = new OfficialBattlePlayerAppearanceSource(AllyId, AllyId,
                    original.ReadPlayer, original.ItemType);
                var previousAppearance = request.PlayerAppearanceResolver;
                request.PlayerAppearanceResolver = unit => unit.Id == AllyId
                    ? allyAppearance : previousAppearance?.Invoke(unit);
                var previousPortrait = request.TimelinePortrait;
                request.TimelinePortrait = unit => previousPortrait?.Invoke(unit.Id == AllyId ? actor : unit);
            }

            public void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                if (item == 10572)
                {
                    ObserveVitalitySpring(step, checkpoint);
                    return;
                }
                if (item == 10575)
                {
                    ObserveSoulGuard(step, checkpoint);
                    return;
                }
                if (item == 10580)
                {
                    ObserveSanctuary(step, checkpoint);
                    return;
                }
                if (verified) return;
                var ally = checkpoint.Units.Single(unit => unit.Id == AllyId);
                var shield = ally.Statuses.FirstOrDefault(status => status.ClassId == 24180
                    && status.CreatorId == 1 && status.ApplyingSkillItemId == 10032);
                if (shield == null) return;
                var formula = assets.LoadDamageFormula();
                var source = shield.SourceProperties.ToDictionary(value => value.Id, value => value.Value);
                var values = shield.StatusProperties.ToDictionary(value => value.Id, value => value.Value);
                long basis = (long)(formula.ReadProperty(source, formula.PropertyId("Defence"))
                    * formula.ReadProperty(values, formula.PropertyId("ShieldByDefence")))
                    + (long)formula.ReadProperty(values, formula.PropertyId("SkillFixedShield"));
                long expected = (long)Math.Max(0.0, basis
                    * (1.0 + formula.ReadProperty(source, formula.PropertyId("StatusShieldAddPercent")))
                    + formula.ReadProperty(source, formula.PropertyId("StatusFixedShieldAdd")));
                File.WriteAllText(Path.Combine(output, "companion-saint-guard-observed.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                Require(shield.ShieldValue == expected && expected > basis && shield.OwnerId == AllyId
                    && shield.CreatorCharacterType == 1 && step.DamageEvents.Any(hit => hit.SkillItemId == 10032
                        && hit.TargetId == AllyId), "The actual allied defence shield lost its bonus, target or player origin: actual="
                    + shield.ShieldValue + ", expected=" + expected + ", basis=" + basis + ", creator="
                    + shield.CreatorId + "/" + shield.CreatorCharacterType + ", targets="
                    + string.Join(",", step.DamageEvents.Select(hit => hit.SkillItemId + ":" + hit.TargetId)));
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                var replay = assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter);
                Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring the actual allied shield changed its value or settled its receipt again.");
                File.WriteAllText(Path.Combine(output, "companion-saint-guard-checkpoint.json"), saved);
                File.WriteAllText(Path.Combine(output, "companion-saint-guard-shield.json"),
                    JsonUtility.ToJson(new GuardianShieldReceipt { ally = AllyId, basis = basis,
                        expected = expected, actual = shield.ShieldValue, restored = true }, true));
                verified = true;
                Debug.Log("[Acquired passive combat] 10593 cultivated allied defence shield, player source and restore replay passed.");
            }

            public bool Ready(OfficialBattleRuntimeController controller)
            {
                if (item == 10572) return VitalityCureCount > 0 && IsNativeSkillParticleVisible("playable11560");
                if (item == 10575) return soulOpened && IsNativeSkillParticleVisible("playable11570");
                if (item == 10580) return sanctuaryTransferred && IsNativeSkillParticleVisible("playable11780");
                if (!verified || controller == null) return false;
                var field = typeof(OfficialBattleRuntimeController).GetField("sceneView", BindingFlags.Instance | BindingFlags.NonPublic);
                var view = (OfficialBattleSceneView)field.GetValue(controller);
                Transform ally = view?.TryGetUnitTransform(AllyId);
                Require(ally && ally.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled),
                    "The allied adventurer was not rendered with the native player model.");
                return IsNativeSkillParticleVisible("playable11170") || IsNativeSkillParticleVisible("playable11171")
                    || IsNativeSkillParticleVisible("playable24180");
            }

            public void Complete(bool captured)
            {
                if (item == 10572)
                {
                    Require(VitalityCureCount > 0 && vitalityHealed && captured,
                        "The cultivated vitality spring did not heal, restore and render its original self-cure.");
                    return;
                }
                if (item == 10575)
                {
                    Require(soulOpened && soulReturned && captured,
                        "The cultivated soul guard did not render its opening, return HP and preserve its receipts.");
                    return;
                }
                if (item == 10580)
                {
                    Require(sanctuaryGranted && sanctuaryTransferred && sanctuaryOpeningCaptured && captured,
                        "The cultivated sanctuary did not render its grant and actual transferred damage.");
                    return;
                }
                Require(verified && captured, "The cultivated allied defence shield was not rendered and persisted.");
            }
        }

        [Serializable]
        private sealed class GuardianShieldReceipt
        {
            public int ally;
            public long basis, expected, actual;
            public bool restored;
        }
    }
}
