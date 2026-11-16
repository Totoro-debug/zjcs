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
        private static bool ValidateElementHarmony => Environment.GetEnvironmentVariable("ZJCS_ELEMENT_HARMONY_ACCEPTANCE") == "1";
        private bool harmonyObserved, harmonyViewObserved, harmonyCaptured;

        private static OfficialBattleGraphCapabilityValidation ConfigureElementHarmony(FightBattleCheckpoint checkpoint)
        {
            var probe = ConfigureFirstPromotion(checkpoint);
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            // Exercise the actual equipped hit graph with one controlled elemental type.
            player.Skills.Single(skill => skill.Slot == 0).ElementType = 102;
            player.Statuses.Add(probe.catalog.CreateStatus(25111, player.Id, player.Id, 9925111,
                25111, 10602, 2, 20, player.SubRank));
            return probe;
        }

        private void ObserveElementHarmonyStep(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            if (!ValidateElementHarmony) return;
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var source = player.Statuses.Single(status => status.ClassId == 25111);
            var buff = player.Statuses.Single(status => status.ClassId == 24388);
            int property = catalog.PropertyType("ElementMaster");
            Require(source.ElementHarmonyStartChecked && buff.StackCount == 1
                && buff.ApplyingSkillItemId == 10602 && buff.SourceSkillRank == 2
                && buff.SourceSkillLevel == 20 && buff.ResolvedProperties.Any(value => value.Id == property && value.Value > 0)
                && step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24388),
                "Original mastery status, source or presentation event differs.");
            string save = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(save)) == save,
                "Element harmony checkpoint changed on restore.");
            harmonyObserved = true;
            Debug.Log("[Element harmony] Original mastery status, start event and checkpoint passed.");
        }

        private bool CaptureElementHarmonyFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateElementHarmony || !harmonyObserved || harmonyCaptured) return false;
            var view = UnityEngine.Object.FindObjectsOfType<MyFightStatus>()
                .FirstOrDefault(value => value.GetComponent<MyEntity>()?.ClassId == 24388);
            if (!view) return false;
            var body = controller.TryGetUnitTransform(1);
            Require(body && view.Context.Owner == body.gameObject, "Mastery status view is not bound to the player.");
            harmonyViewObserved = true;
            harmonyCaptured = true;
            CaptureName = "element-harmony-native";
            return true;
        }

        private void VerifyElementHarmony()
        {
            if (!ValidateElementHarmony) return;
            Require(harmonyObserved && harmonyViewObserved && harmonyCaptured,
                "Original mastery status or its captured view is missing.");
        }
    }
}
