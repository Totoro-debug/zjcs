using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateMoveScopeLifecycle => Environment.GetEnvironmentVariable("ZJCS_MOVE_SCOPE_COLD_SCENE") == "1";
        private bool moveScopeRestored, moveScopeComplete;
        private MoveScopeColdSnapshot moveScopeSnapshot;
        private FightBattleMovementEvent moveScopeWalkingPath;
        private bool moveScopeWalkingObserved;
        private int moveScopeSavedTriggerCount;
        private float moveScopeRetirementDeadline;
        private float moveScopeGraphRetirementDeadline;
        private MyFightStatus[] moveScopeRestoredViews;
        private GameObject[] moveScopeRestoredOwners;
        private MyPlayable[] moveScopeRestoredPlayables;
        private readonly HashSet<int> moveScopeEnded = new HashSet<int>();
        private readonly HashSet<int> moveScopeRemoved = new HashSet<int>();

        public sealed class MoveScopeColdSnapshot
        {
            internal int[] Ids;
            internal Transform[] Bodies;
            internal MyFightStatus[] Views;
            internal MyPlayable[] Playables;
            public string Checkpoint { get; internal set; }
        }

        public bool ReadyForMoveScopeColdScene(OfficialBattleRuntimeController controller) =>
            ValidateMoveScopeLifecycle && captured && !moveScopeRestored && !finisherScheduled
            && !controller.IsPresentingStep;

        private static MyPlayable MoveScopePlayable(MyFightStatus view) => (MyPlayable)typeof(MyFightStatus)
            .GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private MyFightStatus RequireMoveScopeGrid(OfficialBattleRuntimeController controller, int id, bool fresh)
        {
            var item = controller.Checkpoint.GridItems.Single(value => value.Id == id);
            var body = controller.TryGetUnitTransform(id);
            var entity = body ? body.GetComponent<MyEntity>() : null;
            var state = item.Runtime.Statuses.Single(value => value.ClassId == 11632);
            var view = body ? body.GetComponent<MyFightStatusAgent>().MyStatuses.SingleOrDefault(value =>
                value.GetComponent<MyEntity>().Id.EntityId == state.InstanceId) : null;
            Require(!item.Ended && item.GridItemClassId == 3320 && body && entity
                && entity.ClassId == item.EntityClassId && entity.myNewEntity == fresh
                && view && view.GetComponent<MyEntity>().myNewEntity == fresh
                && view.Context.Owner == body.gameObject
                && view.Context.Creator == controller.TryGetUnitTransform(1).gameObject
                && view.Context.Master == controller.TryGetUnitTransform(1).gameObject
                && view.myPlayablePrefab && MoveScopePlayable(view)
                && MoveScopePlayable(view).mySource == body.gameObject,
                "Original 3320/11632 terrain, identity, source or native graph is missing: " + id
                + "; ended=" + item.Ended + "; grid=" + item.GridItemClassId
                + "; body=" + (bool)body + "; entity=" + (bool)entity
                + "; class=" + (entity ? entity.ClassId.ToString() : "null") + "/" + item.EntityClassId
                + "; bodyFresh=" + (entity ? entity.myNewEntity.ToString() : "null") + "/" + fresh
                + "; status=" + (bool)view + "; instance=" + state.InstanceId
                + "; statusFresh=" + (view ? view.GetComponent<MyEntity>().myNewEntity.ToString() : "null")
                + "; owner=" + (view && view.Context.Owner == body.gameObject)
                + "; creator=" + (view && view.Context.Creator == controller.TryGetUnitTransform(1).gameObject)
                + "; master=" + (view && view.Context.Master == controller.TryGetUnitTransform(1).gameObject)
                + "; prefab=" + (view && view.myPlayablePrefab) + "; graph=" + (view && MoveScopePlayable(view)));
            return view;
        }

        public MoveScopeColdSnapshot CaptureMoveScopeColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForMoveScopeColdScene(controller), "Movement-burning checkpoint is not between original turns.");
            var checkpoint = controller.Checkpoint;
            var items = checkpoint.GridItems.Where(value => !value.Ended && value.HasRuntime
                && value.Runtime.Statuses.Any(status => status.ClassId == 11632)).ToArray();
            Require(items.Length >= 5 && items.Any(value => value.Runtime.Statuses.Any(status => status.RootSkillId == 25027)),
                "No live original movement-burning terrain remains for scene restoration.");
            var ids = items.Select(value => value.Id).ToArray();
            var views = ids.Select(id => RequireMoveScopeGrid(controller, id, true)).ToArray();
            moveScopeSnapshot = new MoveScopeColdSnapshot {
                Ids = ids, Bodies = ids.Select(controller.TryGetUnitTransform).ToArray(), Views = views,
                Playables = views.Select(MoveScopePlayable).ToArray(),
                Checkpoint = FightBattleCheckpointSerializer.Save(checkpoint)
            };
            moveScopeSavedTriggerCount = checkpoint.Units.Single(value => value.Id == 1).Statuses
                .Single(value => value.ClassId == 25027).Components.MoveScope.TriggeredCount;
            return moveScopeSnapshot;
        }

        public void BindMoveScopeColdScene(OfficialBattleRuntimeController controller, MoveScopeColdSnapshot saved)
        {
            Require(saved.Bodies.All(value => !value) && saved.Views.All(value => !value)
                && saved.Playables.All(value => !value || !value.gameObject.activeInHierarchy),
                "Old ground bodies/statuses/playables survived complete host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Ground scene restoration changed the saved RNG, counters, lifetimes or receipts.");
            moveScopeRestoredViews = saved.Ids.Select(id => RequireMoveScopeGrid(controller, id, false)).ToArray();
            moveScopeRestoredPlayables = moveScopeRestoredViews.Select(MoveScopePlayable).ToArray();
            moveScopeRestoredOwners = moveScopeRestoredViews.Select(value => value.Context.Owner).ToArray();
            moveScopeRestored = true;
            moveScopeRetirementDeadline = Time.realtimeSinceStartup + 90f;
            BindPresentation(controller);
        }

        public bool MoveScopeColdFireVisible(OfficialBattleRuntimeController controller) =>
            moveScopeSnapshot.Ids.All(id => {
                var view = RequireMoveScopeGrid(controller, id, !moveScopeRestored);
                var graph = MoveScopePlayable(view);
                // The authored fire is two SpriteRenderers, attached through the
                // original FollowBone apply object. It is not a particle system.
                var renderers = graph.GetComponentsInChildren<MyPlayableFollowBone>(true)
                    .Where(value => value.myApplyComponent)
                    .SelectMany(value => value.myApplyComponent.GetComponentsInChildren<SpriteRenderer>()).ToArray();
                return renderers.Length == 2 && renderers.All(r => r.sprite && r.sprite.texture
                    && r.sharedMaterial && r.enabled && !r.forceRenderingOff && r.color.a > 0f)
                    && renderers.Any(r => r.isVisible);
            });

        private void ObserveMoveScopeStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateMoveScopeLifecycle || moveScopeSnapshot == null || moveScopeComplete) return;
            foreach (var value in controller.Checkpoint.Receipts.Last().Event.GridViewEvents)
            {
                if (!moveScopeSnapshot.Ids.Contains(value.GridId)) continue;
                if (value.Kind == FightBattleGridViewEventKind.StatusEnd) moveScopeEnded.Add(value.GridId);
                if (value.Kind == FightBattleGridViewEventKind.Remove) moveScopeRemoved.Add(value.GridId);
            }
        }

        private bool ObserveMoveScopeFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateMoveScopeLifecycle || !captured || moveScopeComplete) return false;
            if (!moveScopeRestored) return true;
            var remaining = moveScopeSnapshot.Ids.Where(id => controller.TryGetUnitTransform(id)).ToArray();
            Require(Time.realtimeSinceStartup < moveScopeRetirementDeadline,
                "Restored ground views did not retire: " + string.Join(",", remaining)
                + "; counter=" + controller.Checkpoint.Counter + "; presenting=" + controller.IsPresentingStep);
            // A later turn may already be presenting. Its clock does not own
            // the saved tiles whose individual end/remove events are checked below.
            if (remaining.Length > 0 || moveScopeRestoredViews.Any(value => value)) return true;
            Require(moveScopeSnapshot.Ids.All(id => moveScopeEnded.Contains(id) && moveScopeRemoved.Contains(id)
                && controller.Checkpoint.GridItems.Single(value => value.Id == id).Ended),
                "A restored ground tile disappeared without its original per-cell status/entity end.");
            bool graphsRetired = moveScopeRestoredPlayables.All(value => !value
                || !value.gameObject.activeInHierarchy || value.myStatus == MyPlayable.PlayableStatus.Stop
                || value.mySource && value.mySource.GetComponent<MyFightRoleGridItem>()
                    && !moveScopeRestoredOwners.Any(old => ReferenceEquals(old, value.mySource)));
            // Original Stop() requests fade-out; PostProcessFrame consumes it.
            // Entity destruction can finish before that next native playable frame.
            // Still require every old graph to stop, within its authored fade-out.
            if (!graphsRetired && moveScopeGraphRetirementDeadline == 0f)
                moveScopeGraphRetirementDeadline = Time.realtimeSinceStartup + 5f
                    + moveScopeRestoredPlayables.Where(value => value).Select(value => value.myFadeOutDuration).DefaultIfEmpty().Max();
            Require(graphsRetired || Time.realtimeSinceStartup < moveScopeGraphRetirementDeadline,
                "Original restored fire status/graph survived its tile's native retirement: "
                + string.Join(",", moveScopeRestoredPlayables.Where(value => value).Select(value =>
                    value.GetInstanceID() + ":" + value.myStatus + ":source="
                    + (value.mySource ? value.mySource.name : "destroyed")
                    + ":active=" + value.gameObject.activeInHierarchy)));
            if (!graphsRetired) return true;
            Require(controller.Checkpoint.Units.Single(value => value.Id == 1).Statuses
                .Single(value => value.ClassId == 25027).Components.MoveScope.TriggeredCount == moveScopeSavedTriggerCount,
                "Restoration or stationary turns repeated the original first/landing trigger.");
            moveScopeComplete = true;
            CaptureName = "move-scope-restored-expired";
            Debug.Log("[Move scope cold scene] Original 3320/11632 native fire restored from ordinary disk with identical RNG/receipts/lifetimes; each saved tile emitted status/entity ends and retired without replaying 25027 activation.");
            return true;
        }

        private void VerifyMoveScopeLifecycle()
        {
            if (ValidateMoveScopeLifecycle) Require(moveScopeRestored && moveScopeComplete,
                "Original movement-burning scene restore and native retirement are incomplete.");
            if (ValidateMoveScopeLifecycle && firstPromotionItemId == 10055) Require(moveScopeWalkingObserved,
                "Original walking action did not render between its real path endpoints.");
        }

        private bool ObserveMoveScopeWalkingFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateMoveScopeLifecycle || moveScopeWalkingPath == null || moveScopeWalkingObserved) return false;
            var body = controller.TryGetUnitTransform(1);
            var path = moveScopeWalkingPath.Path;
            var from = entityViews.ToWorldPos(entityViews.GridToPos(new Zjcs.Official.World.NativeTerrain.Common.Pos2d(path[0].X, path[0].Y)));
            var last = path[path.Count - 1];
            var to = entityViews.ToWorldPos(entityViews.GridToPos(new Zjcs.Official.World.NativeTerrain.Common.Pos2d(last.X, last.Y)));
            if (!body || Vector3.Distance(body.position, from) < .01f || Vector3.Distance(body.position, to) < .01f) return false;
            bool active = body.root.GetComponentsInChildren<Zjcs.Official.BattleSkillPlayables.MyFightActionSkillMove>()
                .Any(value => value.myEntity.Entity?.ClassId == 11073 && value.myApplyEntity
                    && value.myApplyEntity.transform == body);
            if (!active) return false;
            moveScopeWalkingObserved = true;
            CaptureName = "move-scope-native-walking";
            return true;
        }
    }
}
