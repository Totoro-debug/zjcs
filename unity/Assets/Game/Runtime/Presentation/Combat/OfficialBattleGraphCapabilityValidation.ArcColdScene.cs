using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        public sealed class ArcColdSceneSnapshot
        {
            internal MyFightStatus View;
            internal Transform Body;
            public string Checkpoint { get; internal set; }
        }

        private bool ArcColdSceneRequested => Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_COLD_SCENE") == "1";
        private bool arcColdSceneComplete;

        public bool ReadyForArcColdScene(OfficialBattleRuntimeController controller) => ValidateArcShield
            && ArcColdSceneRequested && arcRestored && !arcColdSceneComplete && !controller.IsPresentingStep
            && CaptureName == "arc-shield-restored";

        public ArcColdSceneSnapshot CaptureArcColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForArcColdScene(controller) && ArcShieldView(),
                "Arc shield is not ready for a complete host rebuild.");
            return new ArcColdSceneSnapshot { View = ArcShieldView(), Body = controller.TryGetUnitTransform(1),
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint) };
        }

        public void BindArcColdScene(OfficialBattleRuntimeController controller, ArcColdSceneSnapshot saved)
        {
            Require(!saved.View && !saved.Body, "Old arc-shield status or body survived host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Arc-shield scene resume changed the checkpoint or replayed a receipt.");
            var body = controller.TryGetUnitTransform(1);
            var view = ArcShieldView();
            var owner = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            var status = owner.Statuses.Single(value => value.ClassId == 24030);
            Require(body && view && view.Context.Owner == body.gameObject
                && view.GetComponent<MyEntity>().Id.EntityId == arcInstance
                && !view.GetComponent<MyEntity>().myNewEntity && status.InstanceId == arcInstance
                && status.DurationRound == 3 && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20
                && status.ApplyingSkillItemId == 10024 && status.ShieldKind == 0
                && ArcProperty(owner, 62) == arcBaseBlock + 1800 && ArcProperty(owner, 110) == arcBaseFixed + 18,
                "Arc-shield scene resume lost its original identity, source, properties or non-fresh binding.");
            arcColdSceneComplete = true;
            arcDeadline = Time.realtimeSinceStartup + 180f;
            BindPresentation(controller);
        }

        public bool ArcColdSceneVisible(OfficialBattleRuntimeController controller)
        {
            var body = controller.TryGetUnitTransform(1);
            return body && ArcShieldView() && UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(value =>
                value.myTarget == body.gameObject && value.myAttach.myBoneName == "body"
                && value.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                && value.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
        }
    }
}
