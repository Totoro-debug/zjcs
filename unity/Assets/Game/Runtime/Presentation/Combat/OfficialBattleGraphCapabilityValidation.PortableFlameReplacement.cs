using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidatePortableFlameReplacement =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_REPLACEMENT") == "1";
        private int flameReplacementCaster;
        private bool flameReplacementArmed, flameReplacementSettled, flameReplacementFrame,
            flameReplacementRebound, flameReplacementRestoreFrame, flameReplacementAnimated;
        private float flameReplacementRestoreClock;
        private readonly Dictionary<int, MyFightStatus> flameReplacementOldViews = new Dictionary<int, MyFightStatus>();

        private void BindPortableFlameReplacementPresentation(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameReplacement) return;
            // The fixture cloned the player's appearance unchanged. Bind that same
            // completed original photograph to its second local request identity.
            var field = typeof(OfficialBattleRuntimeController).GetField("timelinePortraitResolver",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var original = (Func<FightBattleUnitState, Sprite>)field.GetValue(controller);
            var player = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            var portrait = original(player);
            Require(portrait && portrait.texture.width == 100 && portrait.texture.height == 120,
                "The copied fixture appearance requires the completed original player photograph.");
            field.SetValue(controller, (Func<FightBattleUnitState, Sprite>)(unit =>
                original(unit.Id == flameReplacementCaster ? player : unit)));
            var scene = (OfficialBattleSceneView)typeof(OfficialBattleRuntimeController)
                .GetField("sceneView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var appearanceField = typeof(OfficialBattleSceneView)
                .GetField("playerAppearance", BindingFlags.Instance | BindingFlags.NonPublic);
            var appearance = (OfficialBattlePlayerAppearanceSource)appearanceField.GetValue(scene);
            var state = controller.Checkpoint.Units.Single(unit => unit.Id == flameReplacementCaster);
            // Use the same complete native-player creation path as the first hero.
            // Attaching a complete fight prefab as a map avatar would duplicate its hierarchy.
            try
            {
                appearanceField.SetValue(scene, new OfficialBattlePlayerAppearanceSource(state.Id,
                    appearance.PlayerId, appearance.ReadPlayer, appearance.ItemType));
                typeof(OfficialBattleSceneView).GetMethod("RemoveUnit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scene, new object[] { state.Id });
                var units = (System.Collections.IDictionary)typeof(OfficialBattleSceneView)
                    .GetField("units", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene);
                units.Remove(state.Id);
                var view = typeof(OfficialBattleSceneView).GetMethod("TryCreatePlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scene, new object[] { state });
                Require(view != null, "The second original player view was not created.");
                var model = (IOfficialBattleNativeModel)view.GetType().GetField("NativeModel").GetValue(view);
                typeof(OfficialBattleSceneView).GetMethod("PlaceUnit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scene, new object[] { state.Id, model.Transform, state, null });
            }
            finally { appearanceField.SetValue(scene, appearance); }
        }

        private void ConfigurePortableFlameReplacement(FightBattleCheckpoint checkpoint)
        {
            Require(firstPromotionFacingIndex == 2 && !ValidatePortableFlameMovement && !PortableFlameColdRequested,
                "The two-caster fire-ring diagnostic faces up in its own native flow.");
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var ally = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(player));
            // JsonUtility materializes a null base list as an empty list. Preserve
            // the uncaptured base so the executor can initialize real role properties.
            ally.BaseProperties = player.BaseProperties?.Select(prop => new FightPropertyValue { Id = prop.Id, Value = prop.Value }).ToList();
            ally.Properties = player.Properties.Select(prop => new FightPropertyValue { Id = prop.Id, Value = prop.Value }).ToList();
            ally.Id = checkpoint.Units.Max(unit => unit.Id) + 1; ally.Turn.Id = ally.Id;
            ally.Skills.Clear(); ally.SkillState.Slots.Clear();
            ally.X = player.X - 1; ally.Y = player.Y;
            Require(ally.X >= 0 && checkpoint.Units.All(unit => unit.X != ally.X || unit.Y != ally.Y),
                "The second original hero requires an unoccupied fixture cell.");
            flameReplacementCaster = ally.Id; checkpoint.Units.Add(ally);
            Require(flameReplacementCaster == 3, "The cloned local appearance must retain its isolated result identity.");
            foreach (var unit in checkpoint.Units)
            {
                unit.Turn.Speed = 1000;
                foreach (var prop in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (prop.Id == catalog.PropertyType("Speed")) prop.Value = 1000;
                    if (prop.Id == catalog.PropertyType("MoveDist") || prop.Id == catalog.PropertyType("FixedMoveDist")) prop.Value = 0;
                }
            }
            checkpoint.MaxRound = 24;
        }

        private void ObservePortableFlameReplacementStep(FightBattleCheckpoint checkpoint)
        {
            var step = checkpoint.Receipts.Last().Event;
            var events = step.StatusViewEvents.Where(value => value.Status.ClassId == 24210).ToArray();
            bool replacing = step.SkillCasts.Any(cast => cast.ActorId == flameReplacementCaster && cast.SkillEntityId == 11200);
            if (replacing)
            {
                Require(flameReplacementArmed && !flameReplacementSettled
                    && events.Count(value => value.Kind == FightBattleStatusViewEventKind.End) == 2
                    && events.Count(value => value.Kind == FightBattleStatusViewEventKind.Start) == 2,
                    "The second original cast must replace both previous rings exactly once.");
                foreach (var end in events.Where(value => value.Kind == FightBattleStatusViewEventKind.End))
                    Require(flameHolders[end.OwnerId].InstanceId == end.Status.InstanceId,
                        "The replacement ended an unrelated original ring.");
                flameHolders.Clear(); flameReplacementSettled = true;
                var hits = step.DamageEvents.Where(value => value.SourceId == flameReplacementCaster && value.SkillEntityId == 10860).ToArray();
                Require(hits.Length == 2 && hits.All(hit => hit.HitEntityId == 10861 && hit.DamageEntityId == 10862
                    && hit.Damage < 0 && hit.BeforeHp - hit.AfterHp == -hit.Damage),
                    "Each replacement must acquire one original child hit from the new caster.");
                Debug.Log("[Portable flame replacement] Two original old entities ended, new creator/cultivation and two acquisition hits passed.");
            }
            foreach (var value in events)
            {
                if (value.Kind == FightBattleStatusViewEventKind.Start)
                {
                    var owner = checkpoint.Units.Single(unit => unit.Id == value.OwnerId);
                    var status = owner.Statuses.Single(item => item.ClassId == 24210);
                    Require(status.InstanceId == value.Status.InstanceId
                        && status.CreatorId == (replacing ? flameReplacementCaster : 1)
                        && status.SourceSkillRank == (replacing ? 3 : 2) && status.SourceSkillLevel == (replacing ? 30 : 20)
                        && status.ApplyingSkillItemId == 10061 && status.DurationRound == 2 && !status.CanDispersed
                        && FightStatusLifecycle.IconRemainingRound(status, checkpoint.Units) == 2,
                        "The native ring replacement lost its identity, new cultivation or reset lifetime.");
                    flameHolders.Add(owner.Id, new PortableFlameHolder { OwnerId = owner.Id,
                        InstanceId = status.InstanceId, ExpiryRound = status.StartRound + 2 });
                    flameDeadline = Time.realtimeSinceStartup + 180f;
                }
                if (!replacing && value.Kind == FightBattleStatusViewEventKind.End)
                {
                    var holder = flameHolders[value.OwnerId];
                    Require(flameReplacementSettled && holder.EndClock < 0 && step.ActorId == holder.OwnerId
                        && step.ActorRound == holder.ExpiryRound && value.Status.InstanceId == holder.InstanceId,
                        "A replacement expired on the old ring's clock or ended more than once.");
                    holder.EndClock = Time.time + value.Delay;
                }
            }
            if (flameHolders.Count > 0) Require(flameHolders.Count == 2, "Each hero must own one independent original ring.");
        }

        private bool ObservePortableFlameReplacementFrame(OfficialBattleRuntimeController controller)
        {
            if (flameComplete || flameHolders.Count == 0) return false;
            Require(Time.realtimeSinceStartup < flameDeadline, "Two-caster fire-ring native validation timed out.");
            var views = PortableFlameViews();
            bool ready = views.Length == 2 && views.All(view => {
                var owner = flameHolders.Values.Single(holder => controller.TryGetUnitTransform(holder.OwnerId)?.gameObject == view.Context.Owner);
                if (view.GetComponent<MyEntity>().Id.EntityId != owner.InstanceId) return false;
                var playable = (MyPlayable)typeof(MyFightStatus).GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                if (playable && !flameGraphs.Contains(playable)) flameGraphs.Add(playable);
                return UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(follow => follow.myTarget == view.Context.Owner
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && follow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
            });
            if (!flamePartyFrame && ready)
            { flamePartyFrame = true; CaptureName = "portable-flame-replacement-first"; return true; }
            if (!captured) return false;
            if (!flameReplacementArmed && ready && !controller.IsPresentingStep)
            {
                foreach (var holder in flameHolders.Values)
                    flameReplacementOldViews.Add(holder.OwnerId, views.Single(view => view.Context.Owner == controller.TryGetUnitTransform(holder.OwnerId).gameObject));
                var caster = controller.Checkpoint.Units.Single(unit => unit.Id == flameReplacementCaster);
                var skill = catalog.CreateSkill(11200, 0, 10061, 3, 30, 1,
                    catalog.CalculateSkillProps(11200, 3, 30, 1).Select(prop => new FightPropertyValue { Id = prop.Key, Value = prop.Value }).ToList());
                skill.LimitedTimes = 1; caster.Skills.Add(skill); caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var renderer = controller.TryGetUnitTransform(caster.Id).GetComponentInChildren<ProfessionNativeSceneRenderer>(true);
                Require(renderer, "The second caster lacks its original profession renderer.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == clipName && time > .04f && weight > 0f) flameReplacementAnimated = true;
                };
                flameReplacementArmed = true; CaptureName = "portable-flame-replacement-before"; return true;
            }
            if (!flameReplacementSettled || controller.IsPresentingStep) return true;
            if (!flameReplacementFrame && ready)
            {
                Require(flameReplacementAnimated && views.All(view => !flameReplacementOldViews.Values.Contains(view))
                    && flameReplacementOldViews.Values.All(view => !view),
                    "The second caster's original action or old native ring retirement was not observed.");
                flameReplacementFrame = true; CaptureName = "portable-flame-replacement-after"; return true;
            }
            if (flameReplacementFrame && !flameReplacementRebound && ready)
            {
                string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "portable-flame-replacement-checkpoint.json");
                string saved = FightBattleCheckpointSerializer.Save(controller.Checkpoint); File.WriteAllText(path, saved);
                var state = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(FightBattleCheckpointSerializer.Load(File.ReadAllText(path))).State;
                Require(FightBattleCheckpointSerializer.Save(state) == saved, "Replacement restore changed the settled checkpoint.");
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                graph.RefreshGridSnapshot(state);
                var rebuilt = PortableFlameViews();
                Require(rebuilt.Length == 2 && rebuilt.All(view => !views.Contains(view) && !view.GetComponent<MyEntity>().myNewEntity),
                    "Replacement restore did not rebuild two non-fresh native views.");
                flameReplacementRebound = true; flameReplacementRestoreClock = Time.time; return true;
            }
            if (flameReplacementRebound && !flameReplacementRestoreFrame && ready && Time.time >= flameReplacementRestoreClock + .15f)
            {
                flameReplacementRestoreFrame = true; CaptureName = "portable-flame-replacement-restored";
                Debug.Log("[Portable flame replacement] Old native views retired, second original hero action, unique new views and disk rebind passed.");
                return true;
            }
            var player = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            if (!flameSecondRoundFrame && flameReplacementRestoreFrame && player.SkillState.Round == flameHolders[1].ExpiryRound - 1 && ready)
            { flameSecondRoundFrame = true; CaptureName = "portable-flame-replacement-next-round"; return true; }
            if (flameHolders.Values.All(holder => holder.EndClock >= 0) && Time.time >= flameHolders.Values.Max(holder => holder.EndClock) + 1f)
            {
                // MyPlayableManager intentionally reuses its returned graph objects.
                // Fresh status views were checked per stage; graph identity need not change.
                Require(views.Length == 0 && flameSecondRoundFrame && flameGraphs.Count >= 2
                    && flameGraphs.All(graph => !graph || graph.myStatus == MyPlayable.PlayableStatus.Stop),
                    $"Replacement retirement is incomplete: views={views.Length}, secondRound={flameSecondRoundFrame}, "
                    + $"graphs={flameGraphs.Count}, activeGraphs={flameGraphs.Count(graph => graph && graph.myStatus != MyPlayable.PlayableStatus.Stop)}.");
                flameComplete = true; CaptureName = "portable-flame-replacement-ended";
                Debug.Log("[Portable flame replacement] New two-personal-round clocks and complete native retirement passed.");
            }
            return true;
        }

        private void VerifyPortableFlameReplacement()
        {
            Require(flameComplete && flameReplacementFrame && flameReplacementRestoreFrame && flameReplacementAnimated
                && flameSoundPlayingIds.Count == 2 && flameSoundPlayingIds.All(id => ArcNativeSoundEvent(id) == 0),
                "Two-caster replacement or the two original native casting sounds did not finish.");
            Debug.Log("[Portable flame replacement] Two original caster sounds completed without a restore replay.");
        }
    }

}
