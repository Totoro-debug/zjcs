using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // Explicit isolated diagnostic; the public weekly host owns combat, input and settlement.
    public sealed class OfficialBattleStatusAbsorptionValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly int actorId;
        private bool armed, absorbed, captured;
        private int nativeCasts;
        private float castTime;
        public string CaptureName { get; private set; }

        private OfficialBattleStatusAbsorptionValidation(FightOfficialCombatCatalog catalog, int actorId)
        { this.catalog = catalog; this.actorId = actorId; }

        public static OfficialBattleStatusAbsorptionValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var actor = checkpoint.Units.First(value => value.Id != 1);
            var player = checkpoint.Units.Single(value => value.Id == 1);
            actor.MonsterId = 3665; actor.MonsterGroupNo = 0;
            actor.MonsterType = 3; actor.MaxHpPlies = 1;
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, actor);
            Require(actor.MonsterTemplateId == 3665, "Original 64280 source model identity differs.");
            player.X = actor.X;
            player.Y = actor.Y + actor.BodyOffsets.Max(value => value.Y) + 1;
            Require(player.X >= 0 && player.Y >= 0 && player.X < checkpoint.Grid.Width
                && player.Y < checkpoint.Grid.Height, "Adjacent diagnostic target is outside the battlefield.");
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear();
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == actor ? 1000 : 1;
                }
                unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 5000000;
                unit.Turn.Speed = unit == actor ? 1000 : 1;
            }
            player.Hp = player.Turn.Hp = 1500000;
            player.Statuses.Add(catalog.CreateStatus(24202, player.Id, player.Id, 9964202));
            var toAbsorb = catalog.CreateStatus(24750, player.Id, actor.Id, 9964750, 64280, 17392);
            toAbsorb.StackCount = 3; player.Statuses.Add(toAbsorb);
            actor.Statuses.Add(catalog.CreateStatus(24774, actor.Id, actor.Id, 9964774, 64280, 17392));
            actor.Skills.Clear(); actor.SkillState.Slots.Clear();
            var skill = catalog.CreateSkill(64280, 0, 17392, 1, 1, actor.SubRank, null);
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 8;
            return new OfficialBattleStatusAbsorptionValidation(catalog, actor.Id);
        }

        public void BindPresentation(OfficialBattleRuntimeController controller)
        {
            var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            entities.CastStarted += cast => {
                if (cast.ActorId != actorId || cast.SkillEntityId != 64280) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            var checkpoint = controller.Checkpoint;
            var actor = checkpoint.Units.Single(value => value.Id == actorId);
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var step = checkpoint.Receipts.Last().Event;
            var energy = actor.Statuses.Single(value => value.ClassId == 24752);
            if (!armed)
            {
                Require(energy.StackCount == 100 && checkpoint.Units.All(value => value.SkillState.Round == 0),
                    "Original startup must create 100 layers before the first personal turn.");
                FightStatusLifecycle.AddStack(energy, -80);
                armed = true;
            }
            if (absorbed || !step.DamageEvents.Any(value => value.SourceId == actorId
                && value.TargetId == 1 && value.SkillEntityId == 64280)) return;
            Require(!player.Statuses.Any(value => value.ClassId == 24750) && energy.StackCount == 50,
                "Actual 64280 hit did not absorb three target layers and add thirty own layers.");
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(restored.Units.Single(value => value.Id == actorId).Statuses
                .Single(value => value.ClassId == 24752).StackCount == 50,
                "Actual absorption result was lost in the Unity checkpoint.");
            absorbed = true;
            actor.Skills.Clear(); actor.SkillState.Slots.Clear();
            foreach (var property in player.Properties.Concat(player.BaseProperties))
            {
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
                if (property.Id == catalog.PropertyType("Speed")) property.Value = 1000;
            }
            foreach (var property in actor.Properties.Concat(actor.BaseProperties))
                if (property.Id == catalog.PropertyType("Speed")) property.Value = 1;
            player.Turn.Speed = 1000;
            actor.Turn.Speed = 1;
            Debug.Log("[Status absorption] native 3665/64280 hit -> target 24750 x3 ended -> own 24752 20+30=50; checkpoint passed.");
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || nativeCasts == 0 || Time.realtimeSinceStartup - castTime < .4f) return;
            var source = controller.TryGetUnitTransform(actorId);
            if (!source || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.isVisible
                && value.enabled && !value.forceRenderingOff && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            var portrait = OfficialBattleMonsterPortraitCatalog.TryLoadDefault()?.Find(3665);
            var card = GameObject.Find("GameRoundCharacterItem_" + actorId);
            Require(portrait && portrait.name == "G_3665" && portrait.texture.format == TextureFormat.RGBA32
                && card && card.GetComponentsInChildren<SpriteRenderer>(true).Any(value =>
                    value.gameObject.activeInHierarchy && value.sprite == portrait),
                "Public timeline did not bind the original 3665 portrait.");
            captured = true; CaptureName = "status-absorption";
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(armed && absorbed && captured && nativeCasts == 1 && checkpoint.Finished,
                "Original absorption battle/presentation/settlement was incomplete.");
            Debug.Log("[Status absorption] original source and skill graph, public battle input, absorption and settlement passed.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Status absorption diagnostic: " + message); }
    }
}
