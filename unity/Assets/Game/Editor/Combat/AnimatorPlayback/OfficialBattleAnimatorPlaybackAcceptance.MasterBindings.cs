using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunMasterBindings()
        {
            RunCasts(); SessionState.SetBool(Key + ".masterBindings", true);
        }

        private static IEnumerator CheckMasterBindings(GameObject actor, GameObject child,
            Func<int, float, IEnumerator> pump, Action<string> capture)
        {
            var root = new GameObject("Shared scene original master binding validation");
            var state = new FightBattleCheckpoint();
            var masterState = new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100 };
            var childState = new FightBattleUnitState { Id = 2, CharacterType = 1, MasterId = 1, Hp = 100, MaxHp = 100, X = 2 };
            state.Units.Add(masterState); state.Units.Add(childState);
            var mapper = new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 8, Height = 8 });
            using var scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                _ => null, mapper, (_, __) => null, (_, __) => { }, OfficialBattleHudCatalog.LoadDefault(), root.transform);
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            using var playback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope,
                new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope, _ => null));
            var resolver = (IBattleMasterEntityResolver)scene;
            try
            {
                scene.Refresh(state); scene.AttachUnitTransform(1, actor.transform); scene.AttachUnitTransform(2, child.transform);
                // AttachUnitTransform mounts the borrowed model below a direct
                // child entity root; keep this fixture independent of debug accessors.
                GameObject EntityRoot(Transform model)
                {
                    while (model.parent != root.transform && model.parent != null) model = model.parent;
                    Require(model.parent == root.transform, "attached model has a scene-owned entity root");
                    return model.gameObject;
                }
                var masterRoot = EntityRoot(actor.transform);
                var childRoot = EntityRoot(child.transform);
                Require(resolver.TryGetMasterEntity(childRoot, out var actual) && actual == masterRoot,
                    "direct current MasterId resolves the owning entity's scene root");
                Require(!resolver.TryGetMasterEntity(masterRoot, out _) && !resolver.TryGetMasterEntity(null, out _)
                    && !resolver.TryGetMasterEntity(actor, out _), "masterless, null and model descendants do not fabricate a role root");
                masterState.Hp = 0;
                Require(resolver.TryGetMasterEntity(childRoot, out actual) && actual == masterRoot,
                    "death alone does not erase a still-present master entity");
                masterState.Hp = 100;
                childState.MasterId = 99;
                Require(!resolver.TryGetMasterEntity(childRoot, out _), "an absent master is not replaced by the caster or victim");
                childState.MasterId = 1; state.Units.Remove(masterState);
                Require(!resolver.TryGetMasterEntity(childRoot, out _), "a detached checkpoint entity cannot resolve through a retained attached-model root");
                state.Units.Add(masterState);
                playback.BindSceneHosts(null, masterResolver: resolver);
                var prefab = Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/13280/playable13280");
                Require(prefab, "complete original 13280 graph");
                var graph = scope.Play(prefab, childRoot, childRoot);
                Require(graph.mySource == masterRoot && graph.myTarget == childRoot,
                    "original full graph replaces source with direct master and retains its distinct target through production injection");
                for (int i = 0; i < 30; i++)
                {
                    yield return pump(1, .01f); scope.Tick(.01f); scope.LateTick(); scene.Tick(.01f);
                }
                capture("master-13280-active");
                scope.Stop(graph, true);
                for (int i = 0; i < 2; i++)
                {
                    yield return pump(1, .01f); scope.Tick(.01f); scope.LateTick();
                }
                childState.MasterId = 99;
                var replay = scope.Play(prefab, childRoot, childRoot);
                Require(ReferenceEquals(graph, replay) && replay.mySource == childRoot && replay.myTarget == childRoot,
                    "pooled original graph reinitializes its source when the current master is absent: reused="
                    + ReferenceEquals(graph, replay) + ", source=" + replay.mySource?.name
                    + ", target=" + replay.myTarget?.name + ", child=" + childRoot.name
                    + ", original status=" + graph.myStatus + ", replay status=" + replay.myStatus);
                scope.Dispose();
                Debug.Log("[Original master binding] real shared scene roots, direct MasterId, null/model/missing/removed branches, retained dead entity, complete 13280 graph, production host injection and pooled reinitialization passed. Full battle/save/Windows Player remain separate.");
            }
            finally
            {
                playback.Dispose(); scene.Dispose(); Object.DestroyImmediate(root);
            }
            yield return null;
        }
    }
}
