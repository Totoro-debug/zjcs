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
        public sealed class DarkColdSceneSnapshot
        {
            internal MyFightStatus View;
            internal Transform Body;
            internal int Owner, Instance, Count;
            public string Checkpoint { get; internal set; }
        }

        public bool ReadyForDarkColdScene => ValidateDarkStatus && darkStatusRestored
            && !darkStatusComplete && CaptureName == "dark-status-restored";

        public DarkColdSceneSnapshot CaptureDarkColdScene(OfficialBattleRuntimeController controller)
        {
            var view = DarkStatusView();
            Require(ReadyForDarkColdScene && !controller.IsPresentingStep && view
                && view.IconShowCustomCount > 0, "Dark cold-scene checkpoint is not between active status turns.");
            return new DarkColdSceneSnapshot { View = view, Body = controller.TryGetUnitTransform(darkStatusOwner),
                Owner = darkStatusOwner, Instance = darkStatusInstance, Count = view.IconShowCustomCount,
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint) };
        }

        public void BindDarkColdScene(OfficialBattleRuntimeController controller, DarkColdSceneSnapshot saved)
        {
            Require(!saved.View && !saved.Body, "Old battle status or model survived complete host destruction.");
            var view = DarkStatusView();
            var body = controller.TryGetUnitTransform(saved.Owner);
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Cold scene resume changed the saved battle or replayed a receipt.");
            Require(view && body && view.Context.Owner == body.gameObject
                && view.GetComponent<MyEntity>().Id.EntityId == saved.Instance
                && !view.GetComponent<MyEntity>().myNewEntity && view.IconShowCustomCount == saved.Count,
                "Cold scene resume lost original status identity, count, owner or non-fresh binding.");
            BindPresentation(controller);
        }

        public bool DarkColdSceneParticlesVisible(OfficialBattleRuntimeController controller)
        {
            var body = controller.TryGetUnitTransform(darkStatusOwner);
            return body && UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(value =>
                value.myTarget == body.gameObject && value.myAttach.myBoneName == "body"
                && value.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                && value.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
        }
    }
}
