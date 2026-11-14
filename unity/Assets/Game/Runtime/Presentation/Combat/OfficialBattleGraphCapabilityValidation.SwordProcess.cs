using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        [Serializable]
        public sealed class SwordProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint, FinisherCheckpoint, ClipName;
            public int ProcessId, EnemyId, SecretInstance;
            public bool Shield;
        }

        public bool ReadyForSwordProcess(OfficialBattleRuntimeController controller) => ValidateSwordCounterattack
            && !settled && !controller.IsPresentingStep && controller.Checkpoint.Receipts.Count > 0
            && controller.Checkpoint.Receipts.Sum(value => value.Event.DamageEvents.Count(hit => hit.SkillEntityId == 59460)) == 1
            && controller.Checkpoint.Receipts.All(value => value.Event.SkillCasts.All(cast => cast.SkillEntityId != 11210));

        public SwordProcessHandoff CaptureSwordProcess(OfficialBattleRuntimeController controller, string saveRoot, string slot)
        {
            Require(ReadyForSwordProcess(controller) && nativeCasts == 0 && swordCounterattackPlaying.Count == 0,
                "Sword process preparation must follow one actual incoming hit and precede the first counterattack.");
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            var status = player.Statuses.Single(value => value.ClassId == 25017);
            Require(status.LogicTriggeredCount == 0 && player.SkillState.Slots.Count == 0
                && (!SwordCounterattackShield || player.Statuses.Any(value => value.ClassId == 24435 && value.ShieldRemainValue > 0)),
                "Prepared secret already fired, consumed an equipment slot or lost its original shield.");
            string checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
            var carrier = FightBattleCheckpointSerializer.Load(checkpoint);
            var actor = carrier.Units.Single(value => value.Id == 1);
            actor.Skills.Add(finisher); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            return new SwordProcessHandoff {
                SaveRoot = saveRoot, Slot = slot, Checkpoint = checkpoint,
                FinisherCheckpoint = FightBattleCheckpointSerializer.Save(carrier), ClipName = clipName,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                EnemyId = swordCounterattackEnemy, SecretInstance = status.InstanceId, Shield = SwordCounterattackShield
            };
        }

        public static OfficialBattleGraphCapabilityValidation ResumeSwordProcess(
            OfficialBattleRuntimeController controller, SwordProcessHandoff saved)
        {
            Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                && FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Sword process resume changed its checkpoint or did not restart the native process.");
            var carrier = FightBattleCheckpointSerializer.Load(saved.FinisherCheckpoint);
            var probe = new OfficialBattleGraphCapabilityValidation(OfficialCombatAssetCatalog.LoadDefault().Load(),
                carrier.Units.Single(value => value.Id == 1).Skills.Single(), 11210, saved.ClipName) {
                swordCounterattackEnemy = saved.EnemyId, previousCaptureFramerate = Time.captureFramerate
            };
            Require(probe.ValidateSwordCounterattack && probe.SwordCounterattackShield == saved.Shield
                && probe.ReadyForSwordProcess(controller), "Sword resume lost its original diagnostic or first hit.");
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            var status = player.Statuses.Single(value => value.ClassId == 25017);
            Require(status.InstanceId == saved.SecretInstance && status.LogicTriggeredCount == 0
                && status.ApplyingSkillItemId == 10537 && status.SourceSkillRank == 7
                && status.SourceSkillLevel == 30 && status.SourceSubRank == 8,
                "Sword resume lost the unfired secret's identity or cultivation.");
            Time.captureFramerate = 60;
            probe.BindPresentation(controller);
            return probe;
        }

        public bool SwordProcessBodyVisible(OfficialBattleRuntimeController controller, SwordProcessHandoff saved)
        {
            var body = controller.TryGetUnitTransform(1);
            return body && body.GetComponentsInChildren<Renderer>().Any(value => value.enabled && value.isVisible)
                && UnityEngine.Object.FindObjectsOfType<MyFightStatus>().Any(value =>
                    value.GetComponent<MyEntity>()?.Id.EntityId == saved.SecretInstance
                    && value.Context.Owner == body.gameObject && !value.GetComponent<MyEntity>().myNewEntity);
        }
    }
}
