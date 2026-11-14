using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateArcShield => Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_LIFECYCLE") == "1";
        private long arcBaseBlock, arcBaseFixed;
        private int arcInstance, arcForeignTurns, arcEndCount;
        private float arcStartClock, arcEndClock = -1f, arcDeadline;
        private bool arcVisible, arcRestored, arcComplete;
        private readonly HashSet<int> arcRoundFrames = new HashSet<int>();
        private readonly List<MyPlayable> arcGraphs = new List<MyPlayable>();

        private static long ArcProperty(FightBattleUnitState unit, int id)
            => unit.Properties.Where(value => value.Id == id).Sum(value => value.Value);

        private void ConfigureArcShieldDiagnostic(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateArcShield) return;
            Require(firstPromotionItemId == 10024 && skillId == 10760,
                "The arc-shield lifecycle requires original 10024/10760.");
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            arcBaseBlock = ArcProperty(player, 62); arcBaseFixed = ArcProperty(player, 110);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = 1000;
                unit.Turn.Speed = 1000;
            }
            checkpoint.MaxRound = 24;
        }

        private MyFightStatus ArcShieldView() => UnityEngine.Object.FindObjectsOfType<MyFightStatus>()
            .SingleOrDefault(view => view.GetComponent<MyEntity>()?.ClassId == 24030);

        private void ObserveArcShieldStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateArcShield || arcComplete) return;
            var step = checkpoint.Receipts.Last().Event;
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            foreach (var value in step.StatusViewEvents.Where(value => value.Status.ClassId == 24030))
            {
                if (value.Kind == FightBattleStatusViewEventKind.Start)
                {
                    var status = player.Statuses.Single(item => item.InstanceId == value.Status.InstanceId);
                    Require(arcInstance == 0 && value.OwnerId == 1 && status.DurationRound == 3
                        && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20
                        && status.RootSkillId == 10760 && status.ApplyingSkillItemId == 10024
                        && status.ShieldKind == 0, "Original arc-shield source or property-buff kind changed.");
                    arcInstance = value.Status.InstanceId; arcStartClock = Time.time + value.Delay;
                    arcDeadline = Time.realtimeSinceStartup + 180f;
                }
                if (value.Kind == FightBattleStatusViewEventKind.End)
                {
                    arcEndCount++;
                    Require(step.ActorId == 1 && step.ActorRound == 4 && arcEndCount == 1,
                        "Arc shield ended on another clock or more than once.");
                    arcEndClock = Time.time + value.Delay;
                }
            }
            if (arcInstance == 0) return;
            bool active = player.Statuses.Any(status => status.InstanceId == arcInstance);
            Require(ArcProperty(player, 62) == arcBaseBlock + (active ? 1800 : 0)
                && ArcProperty(player, 110) == arcBaseFixed + (active ? 18 : 0),
                "Arc-shield block properties were lost or remained after natural expiry.");
            if (step.ActorId != 1 && active) arcForeignTurns++;
        }

        private bool ObserveArcShieldFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateArcShield || arcComplete || arcInstance == 0) return false;
            Require(Time.realtimeSinceStartup < arcDeadline, "Original arc-shield lifecycle timed out.");
            var view = ArcShieldView();
            var player = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            var body = controller.TryGetUnitTransform(1);
            if (Time.time + .0001f < arcStartClock)
            {
                Require(!view, "Original arc-shield view appeared before the hit.");
                return false;
            }
            if (view)
            {
                Require(body && view.Context.Owner == body.gameObject && view.CanShowSpriteIcon()
                    && !view.IsIconShowCustomCount && !view.IsIconShowStatusRoundReplaceStack
                    && view.GetComponent<MyEntity>().Id.EntityId == arcInstance,
                    "Original arc-shield owner, icon flags or identity changed.");
                var graph = (MyPlayable)typeof(MyFightStatus)
                    .GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                if (graph && !arcGraphs.Contains(graph)) arcGraphs.Add(graph);
                bool rendered = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(follow =>
                    follow.myTarget == body.gameObject && follow.myAttach.myBoneName == "body"
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && follow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
                if (rendered && arcRoundFrames.Add(player.SkillState.Round))
                {
                    arcVisible = true; CaptureName = "arc-shield-round-" + player.SkillState.Round;
                    return true;
                }
            }
            if (!captured) return false;
            if (arcVisible && !arcRestored && view && !controller.IsPresentingStep)
            {
                string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "arc-shield-checkpoint.json");
                string saved = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                File.WriteAllText(path, saved);
                var state = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                    FightBattleCheckpointSerializer.Load(File.ReadAllText(path))).State;
                Require(FightBattleCheckpointSerializer.Save(state) == saved,
                    "Disk restore changed the original arc-shield checkpoint.");
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                graph.RefreshGridSnapshot(state);
                var restored = ArcShieldView();
                Require(restored && restored != view && !restored.GetComponent<MyEntity>().myNewEntity
                    && restored.GetComponent<MyEntity>().Id.EntityId == arcInstance
                    && restored.Context.Owner == body.gameObject,
                    "Arc shield did not rebuild its original non-fresh native status from disk.");
                arcRestored = true; CaptureName = "arc-shield-restored"; return true;
            }
            if (arcEndClock >= 0 && Time.time >= arcEndClock + 1f)
            {
                Require(!view && arcVisible && arcRestored && arcForeignTurns >= 2 && arcEndCount == 1
                    && arcRoundFrames.IsSupersetOf(new[] { 1, 2, 3 })
                    && !player.Statuses.Any(status => status.InstanceId == arcInstance)
                    && ArcProperty(player, 62) == arcBaseBlock && ArcProperty(player, 110) == arcBaseFixed
                    && arcGraphs.Count >= 1 && arcGraphs.All(graph => !graph || graph.myStatus == MyPlayable.PlayableStatus.Stop),
                    "Original arc-shield expiry did not retire its native status/graph and block properties.");
                arcComplete = true; CaptureName = "arc-shield-ended";
                Debug.Log("[Arc shield] Original 24030 body particles, disk rebind, three owner rounds, property rollback and retirement passed.");
                return true;
            }
            return true;
        }

        private void VerifyArcShieldDiagnostic()
        {
            if (ValidateArcShield) Require(arcComplete && (!ArcColdSceneRequested || arcColdSceneComplete),
                "Original arc-shield native lifecycle or requested full scene rebuild did not finish.");
            VerifyArcShieldSound();
        }
    }
}
