using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateVoidMirage => Environment.GetEnvironmentVariable("ZJCS_VOID_MIRAGE_ACCEPTANCE") == "1";
        private bool voidViewsSeen, voidFrameCaptured;
        private float voidViewsClock;

        private static OfficialBattleGraphCapabilityValidation ConfigureVoidMirage(FightBattleCheckpoint checkpoint)
        {
            var basis = ConfigureSwordCounterattack(checkpoint);
            var probe = new OfficialBattleGraphCapabilityValidation(basis.catalog, basis.finisher, 10990, "100/skill_14")
                { previousCaptureFramerate = basis.previousCaptureFramerate };
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            player.Statuses.Clear();
            player.Hp = player.Turn.Hp = player.MaxHp * 51 / 100;
            player.Statuses.Add(basis.catalog.CreateStatus(25022, player.Id, player.Id, 9925022,
                25022, 10556, 2, 20, player.SubRank, player.Properties));
            foreach (var enemy in checkpoint.Units.Where(unit => unit.Side != player.Side))
                foreach (var attack in enemy.Skills)
                { attack.HasCustomDamage = true; attack.CustomDamage = player.MaxHp; }
            checkpoint.RandomSeed = 13;
            return probe;
        }

        private bool ObserveVoidMirageStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateVoidMirage) return false;
            if (settled) return true;
            var checkpoint = controller.Checkpoint;
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var step = checkpoint.Receipts.Last().Event;
            var hit = step.DamageEvents.FirstOrDefault(value => value.TargetId == player.Id
                && value.SkillEntityId == 59460 && !value.IsCure);
            if (hit == null) return true;
            var shield = player.Statuses.SingleOrDefault(value => value.ClassId == 24213);
            var dodge = player.Statuses.SingleOrDefault(value => value.ClassId == 24209);
            Require(hit.BeforeHp > player.MaxHp / 2 && hit.AfterHp > 0 && hit.AfterHp <= player.MaxHp / 2
                && !hit.Dodge && !hit.Blinding && !hit.Immunity && shield != null && dodge != null
                && shield.ShieldInitialized && shield.ShieldRemainValue > 0
                && shield.DurationRound == 2 && dodge.DurationRound == 2
                && !shield.CanDispersed && !dodge.CanDispersed
                && shield.ApplyingSkillItemId == 10556 && dodge.ApplyingSkillItemId == 10556
                && shield.SourceSkillRank == 2 && dodge.SourceSkillLevel == 20
                && step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24213)
                && step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24209),
                "Original threshold, shield, dodge or status view events differ.");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                "Void mirage checkpoint changed on restore.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "void-mirage-checkpoint.json"), saved);
            foreach (var unit in checkpoint.Units) { unit.Skills.Clear(); unit.SkillState.Slots.Clear(); }
            settled = true;
            Debug.Log("[Void mirage] Original threshold, shield, dodge and checkpoint passed.");
            return true;
        }

        private bool ObserveVoidMirageFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateVoidMirage) return false;
            if (!settled) return true;
            var views = UnityEngine.Object.FindObjectsOfType<MyFightStatus>();
            var shield = views.FirstOrDefault(value => value.GetComponent<MyEntity>()?.ClassId == 24213);
            var dodge = views.FirstOrDefault(value => value.GetComponent<MyEntity>()?.ClassId == 24209);
            if (!voidViewsSeen && shield && dodge)
            {
                var body = controller.TryGetUnitTransform(1);
                Require(body && shield.Context.Owner == body.gameObject && dodge.Context.Owner == body.gameObject,
                    "Original protection views are not bound to their holder.");
                voidViewsSeen = true; voidViewsClock = Time.time;
            }
            if (!voidViewsSeen) return true;
            if (!voidFrameCaptured && Time.time - voidViewsClock >= .35f)
            { voidFrameCaptured = true; CaptureName = "void-mirage-native"; return true; }
            if (!voidFrameCaptured) return true;
            if (!finisherScheduled)
            {
                var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
                player.Skills.Add(finisher); player.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
                foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
                finisherScheduled = true;
            }
            return true;
        }

        private void VerifyVoidMirage(FightBattleCheckpoint checkpoint)
        {
            Time.captureFramerate = previousCaptureFramerate;
            Require(settled && voidViewsSeen && voidFrameCaptured && checkpoint.Finished,
                "Original shield/dodge views or completed battle are missing.");
            Debug.Log("[Void mirage] Original shield and dodge views, captured frame and result passed.");
        }
    }
}
