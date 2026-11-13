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
        private bool ValidateHitScopeLifecycle => Environment.GetEnvironmentVariable("ZJCS_HIT_SCOPE_COLD_SCENE") == "1";
        private bool ValidateTerrainScopeLifecycle => ValidateMoveScopeLifecycle || ValidateHitScopeLifecycle;
        private int TerrainScopeStatusId => ValidateHitScopeLifecycle ? 11582 : 11632;
        private int TerrainScopePassiveId => ValidateHitScopeLifecycle ? 25045 : 25027;
        private int TerrainScopeRootSkillId => ValidateHitScopeLifecycle ? 11580 : 25027;
        private int TerrainScopeMinimumTiles => ValidateHitScopeLifecycle ? 2 : 5;
        public string TerrainScopeCapturePrefix => ValidateHitScopeLifecycle ? "hit-scope" : "move-scope";
        private int TerrainScopeTriggerCount(FightBattleCheckpoint checkpoint)
        {
            var components = checkpoint.Units.Single(value => value.Id == 1).Statuses
                .Single(value => value.ClassId == TerrainScopePassiveId).Components;
            return ValidateHitScopeLifecycle ? components.HitScope.TriggeredCount : components.MoveScope.TriggeredCount;
        }
        private bool terrainScopeRestored, terrainScopeComplete;
        private TerrainScopeColdSnapshot terrainScopeSnapshot;
        private FightBattleMovementEvent terrainScopeWalkingPath;
        private bool terrainScopeWalkingObserved;
        private int terrainScopeSavedTriggerCount;
        private float terrainScopeRetirementDeadline;
        private float terrainScopeGraphRetirementDeadline;
        private MyFightStatus[] terrainScopeRestoredViews;
        private GameObject[] terrainScopeRestoredOwners;
        private MyPlayable[] terrainScopeRestoredPlayables;
        private readonly HashSet<int> terrainScopeEnded = new HashSet<int>();
        private readonly HashSet<int> terrainScopeRemoved = new HashSet<int>();

        public sealed class TerrainScopeColdSnapshot
        {
            internal int[] Ids;
            internal Transform[] Bodies;
            internal MyFightStatus[] Views;
            internal MyPlayable[] Playables;
            public string Checkpoint { get; internal set; }
        }

        public bool ReadyForTerrainScopeColdScene(OfficialBattleRuntimeController controller) =>
            ValidateTerrainScopeLifecycle && captured && !terrainScopeRestored && !finisherScheduled
            && !controller.IsPresentingStep;

        private static MyPlayable TerrainScopePlayable(MyFightStatus view) => (MyPlayable)typeof(MyFightStatus)
            .GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private MyFightStatus RequireTerrainScopeGrid(OfficialBattleRuntimeController controller, int id, bool fresh)
        {
            var item = controller.Checkpoint.GridItems.Single(value => value.Id == id);
            var body = controller.TryGetUnitTransform(id);
            var entity = body ? body.GetComponent<MyEntity>() : null;
            var state = item.Runtime.Statuses.Single(value => value.ClassId == TerrainScopeStatusId);
            var view = body ? body.GetComponent<MyFightStatusAgent>().MyStatuses.SingleOrDefault(value =>
                value.GetComponent<MyEntity>().Id.EntityId == state.InstanceId) : null;
            Require(!item.Ended && item.GridItemClassId == 3320 && body && entity
                && entity.ClassId == item.EntityClassId && entity.myNewEntity == fresh
                && view && view.GetComponent<MyEntity>().myNewEntity == fresh
                && view.Context.Owner == body.gameObject
                && view.Context.Creator == controller.TryGetUnitTransform(1).gameObject
                && view.Context.Master == controller.TryGetUnitTransform(1).gameObject
                && view.myPlayablePrefab && TerrainScopePlayable(view)
                && TerrainScopePlayable(view).mySource == body.gameObject,
                "Original 3320/" + TerrainScopeStatusId + " terrain, identity, source or native graph is missing: " + id
                + "; ended=" + item.Ended + "; grid=" + item.GridItemClassId
                + "; body=" + (bool)body + "; entity=" + (bool)entity
                + "; class=" + (entity ? entity.ClassId.ToString() : "null") + "/" + item.EntityClassId
                + "; bodyFresh=" + (entity ? entity.myNewEntity.ToString() : "null") + "/" + fresh
                + "; status=" + (bool)view + "; instance=" + state.InstanceId
                + "; statusFresh=" + (view ? view.GetComponent<MyEntity>().myNewEntity.ToString() : "null")
                + "; owner=" + (view && view.Context.Owner == body.gameObject)
                + "; creator=" + (view && view.Context.Creator == controller.TryGetUnitTransform(1).gameObject)
                + "; master=" + (view && view.Context.Master == controller.TryGetUnitTransform(1).gameObject)
                + "; prefab=" + (view && view.myPlayablePrefab) + "; graph=" + (view && TerrainScopePlayable(view)));
            return view;
        }

        public TerrainScopeColdSnapshot CaptureTerrainScopeColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForTerrainScopeColdScene(controller), "Terrain checkpoint is not between original turns.");
            var checkpoint = controller.Checkpoint;
            var items = checkpoint.GridItems.Where(value => !value.Ended && value.HasRuntime
                && value.Runtime.Statuses.Any(status => status.ClassId == TerrainScopeStatusId)).ToArray();
            Require(items.Length >= TerrainScopeMinimumTiles && items.Any(value => value.Runtime.Statuses.Any(status => status.RootSkillId == TerrainScopeRootSkillId)),
                "No live original burning terrain remains for scene restoration.");
            var ids = items.Select(value => value.Id).ToArray();
            var views = ids.Select(id => RequireTerrainScopeGrid(controller, id, true)).ToArray();
            terrainScopeSnapshot = new TerrainScopeColdSnapshot {
                Ids = ids, Bodies = ids.Select(controller.TryGetUnitTransform).ToArray(), Views = views,
                Playables = views.Select(TerrainScopePlayable).ToArray(),
                Checkpoint = FightBattleCheckpointSerializer.Save(checkpoint)
            };
            terrainScopeSavedTriggerCount = TerrainScopeTriggerCount(checkpoint);
            return terrainScopeSnapshot;
        }

        public void BindTerrainScopeColdScene(OfficialBattleRuntimeController controller, TerrainScopeColdSnapshot saved)
        {
            Require(saved.Bodies.All(value => !value) && saved.Views.All(value => !value)
                && saved.Playables.All(value => !value || !value.gameObject.activeInHierarchy),
                "Old ground bodies/statuses/playables survived complete host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Ground scene restoration changed the saved RNG, counters, lifetimes or receipts.");
            terrainScopeRestoredViews = saved.Ids.Select(id => RequireTerrainScopeGrid(controller, id, false)).ToArray();
            terrainScopeRestoredPlayables = terrainScopeRestoredViews.Select(TerrainScopePlayable).ToArray();
            terrainScopeRestoredOwners = terrainScopeRestoredViews.Select(value => value.Context.Owner).ToArray();
            terrainScopeRestored = true;
            terrainScopeRetirementDeadline = Time.realtimeSinceStartup + 90f;
            BindPresentation(controller);
        }

        public bool TerrainScopeColdFireVisible(OfficialBattleRuntimeController controller) =>
            terrainScopeSnapshot.Ids.All(id => {
                var view = RequireTerrainScopeGrid(controller, id, !terrainScopeRestored);
                var graph = TerrainScopePlayable(view);
                // The authored fire is two SpriteRenderers, attached through the
                // original FollowBone apply object. It is not a particle system.
                var renderers = graph.GetComponentsInChildren<MyPlayableFollowBone>(true)
                    .Where(value => value.myApplyComponent)
                    .SelectMany(value => value.myApplyComponent.GetComponentsInChildren<SpriteRenderer>()).ToArray();
                return renderers.Length == 2 && renderers.All(r => r.sprite && r.sprite.texture
                    && r.sharedMaterial && r.enabled && !r.forceRenderingOff && r.color.a > 0f)
                    && renderers.Any(r => r.isVisible);
            });

        private void ObserveTerrainScopeStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateTerrainScopeLifecycle || terrainScopeSnapshot == null || terrainScopeComplete) return;
            foreach (var value in controller.Checkpoint.Receipts.Last().Event.GridViewEvents)
            {
                if (!terrainScopeSnapshot.Ids.Contains(value.GridId)) continue;
                if (value.Kind == FightBattleGridViewEventKind.StatusEnd) terrainScopeEnded.Add(value.GridId);
                if (value.Kind == FightBattleGridViewEventKind.Remove) terrainScopeRemoved.Add(value.GridId);
            }
        }

        private bool ObserveTerrainScopeFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateTerrainScopeLifecycle || !captured || terrainScopeComplete) return false;
            if (!terrainScopeRestored) return true;
            var remaining = terrainScopeSnapshot.Ids.Where(id => controller.TryGetUnitTransform(id)).ToArray();
            Require(Time.realtimeSinceStartup < terrainScopeRetirementDeadline,
                "Restored ground views did not retire: " + string.Join(",", remaining)
                + "; counter=" + controller.Checkpoint.Counter + "; presenting=" + controller.IsPresentingStep);
            // A later turn may already be presenting. Its clock does not own
            // the saved tiles whose individual end/remove events are checked below.
            if (remaining.Length > 0 || terrainScopeRestoredViews.Any(value => value)) return true;
            Require(terrainScopeSnapshot.Ids.All(id => terrainScopeEnded.Contains(id) && terrainScopeRemoved.Contains(id)
                && controller.Checkpoint.GridItems.Single(value => value.Id == id).Ended),
                "A restored ground tile disappeared without its original per-cell status/entity end.");
            bool graphsRetired = terrainScopeRestoredPlayables.All(value => !value
                || !value.gameObject.activeInHierarchy || value.myStatus == MyPlayable.PlayableStatus.Stop
                || value.mySource && value.mySource.GetComponent<MyFightRoleGridItem>()
                    && !terrainScopeRestoredOwners.Any(old => ReferenceEquals(old, value.mySource)));
            // Original Stop() requests fade-out; PostProcessFrame consumes it.
            // Entity destruction can finish before that next native playable frame.
            // Still require every old graph to stop, within its authored fade-out.
            if (!graphsRetired && terrainScopeGraphRetirementDeadline == 0f)
                terrainScopeGraphRetirementDeadline = Time.realtimeSinceStartup + 5f
                    + terrainScopeRestoredPlayables.Where(value => value).Select(value => value.myFadeOutDuration).DefaultIfEmpty().Max();
            Require(graphsRetired || Time.realtimeSinceStartup < terrainScopeGraphRetirementDeadline,
                "Original restored fire status/graph survived its tile's native retirement: "
                + string.Join(",", terrainScopeRestoredPlayables.Where(value => value).Select(value =>
                    value.GetInstanceID() + ":" + value.myStatus + ":source="
                    + (value.mySource ? value.mySource.name : "destroyed")
                    + ":active=" + value.gameObject.activeInHierarchy)));
            if (!graphsRetired) return true;
            Require(TerrainScopeTriggerCount(controller.Checkpoint) == terrainScopeSavedTriggerCount,
                "Restoration or stationary turns repeated the original terrain trigger: " + TerrainScopePassiveId);
            terrainScopeComplete = true;
            CaptureName = TerrainScopeCapturePrefix + "-restored-expired";
            Debug.Log("[" + TerrainScopeCapturePrefix + " cold scene] Original 3320/" + TerrainScopeStatusId
                + " native fire restored from ordinary disk with identical RNG/receipts/lifetimes; each saved tile emitted status/entity ends and retired without replaying " + TerrainScopePassiveId + " activation.");
            return true;
        }

        private void VerifyTerrainScopeLifecycle()
        {
            if (ValidateTerrainScopeLifecycle) Require(terrainScopeRestored && terrainScopeComplete,
                "Original movement-burning scene restore and native retirement are incomplete.");
            if (ValidateTerrainScopeLifecycle && firstPromotionItemId == 10055) Require(terrainScopeWalkingObserved,
                "Original walking action did not render between its real path endpoints.");
        }

        private bool ObserveTerrainScopeWalkingFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateTerrainScopeLifecycle || terrainScopeWalkingPath == null || terrainScopeWalkingObserved) return false;
            var body = controller.TryGetUnitTransform(1);
            var path = terrainScopeWalkingPath.Path;
            var from = entityViews.ToWorldPos(entityViews.GridToPos(new Zjcs.Official.World.NativeTerrain.Common.Pos2d(path[0].X, path[0].Y)));
            var last = path[path.Count - 1];
            var to = entityViews.ToWorldPos(entityViews.GridToPos(new Zjcs.Official.World.NativeTerrain.Common.Pos2d(last.X, last.Y)));
            if (!body || Vector3.Distance(body.position, from) < .01f || Vector3.Distance(body.position, to) < .01f) return false;
            bool active = body.root.GetComponentsInChildren<Zjcs.Official.BattleSkillPlayables.MyFightActionSkillMove>()
                .Any(value => value.myEntity.Entity?.ClassId == 11073 && value.myApplyEntity
                    && value.myApplyEntity.transform == body);
            if (!active) return false;
            terrainScopeWalkingObserved = true;
            CaptureName = "move-scope-native-walking";
            return true;
        }
    }
}
