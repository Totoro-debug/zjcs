using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Presentation.Combat;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using MyPlayableAnimator = Zjcs.Official.StoryAnimation.MyPlayableAnimator;

namespace Zjcs.Editor.ProfessionUI
{
    public static partial class OfficialBattleMonsterSceneValidation
    {
        [Serializable] private sealed class ConfiguredGraphProof { public ConfiguredGraphCounts counts; public ConfiguredSelection selection; public OriginalBoneGap[] original_bone_gaps; }
        [Serializable] private sealed class ConfiguredSelection { public int[] entity_ids; }
        [Serializable] private sealed class ConfiguredGraphCounts { public int models, active_models, casts; }
        [Serializable] private sealed class OriginalBoneGap { public int monster, skill; public string bone; }
        [Serializable] private sealed class ConfiguredGraphResult { public int actors, casts; public string[] unresolvedOriginalReferences; }
        // Uses actual group-zero item/entity bindings, independently checked
        // against the selected APK/cloud tables by the preparation script.
        // A bounded view probe does not select AI targets or settle damage.
        private static IEnumerable<int> ConfiguredMonsterGraphSteps(OfficialBattleGridMapper mapper, string output)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = assets.Load();
            var items = assets.LoadSkillItems();
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(repository)) throw new InvalidOperationException("Set the validation repository root.");
            var proof = JsonUtility.FromJson<ConfiguredGraphProof>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/battle-animation/configured-monster-graphs-source-manifest.json")));
            var expected = proof.counts;
            var unresolved = new HashSet<string>();
            Require(expected.models == proof.selection.entity_ids.Length && proof.selection.entity_ids.All(BoundMonsterIds().Contains),
                "Prepared original loadout selection differs.");
            int casts = 0, actors = 0;
            var rows = new List<string>();
            foreach (int monster in proof.selection.entity_ids.OrderBy(value => value))
            {
                var configured = combat.MonsterActiveSkillItems(monster, 0);
                if (configured.Count == 0) continue;
                actors++;
                foreach (int item in configured)
                {
                    int skill = items.Required(item).EntityId;
                    Debug.Log($"[Monster configured graph] begin {monster}/{item}/{skill}");
                    var source = new FightBattleUnitState { Id = 1, MonsterId = monster, MonsterTemplateId = monster,
                        Hp = 100, MaxHp = 100, X = 2, Y = 2, Direction = 3 };
                    var target = new FightBattleUnitState { Id = 2, MonsterId = 3904, MonsterTemplateId = 3904,
                        Hp = 100, MaxHp = 100, X = 3, Y = 2, Direction = 1 };
                    var checkpoint = new FightBattleCheckpoint();
                    checkpoint.Units.Add(source); checkpoint.Units.Add(target);
                    var scope = new OfflineBattleNativePlayableScope(root.transform, new SoundProbe());
                    var entities = new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope,
                        id => checkpoint.Units.FirstOrDefault(value => value.Id == id));
                    var graphs = OfficialBattleSkillGraphCatalog.TryLoadDefault();
                    var prefab = graphs.Find(skill, "RightPlayablePrefab");
                    Require(prefab, $"Configured original skill prefab is unbound: {monster}/{skill}");
                    bool hasAnimator = prefab.GetComponentsInChildren<MyPlayableAnimator>(true).Length > 0;
                    nativeGraphPlayback = new OfficialBattleSkillGraphPlayback(graphs, scope, entities);
                    scene = new OfficialBattleSceneView(null, root.transform, null, mapper, null, null, nativePlayableScope: scope);
                    scene.Refresh(checkpoint);
                    Transform entity = root.transform.Find("BattleUnit_1");
                    Transform targetEntity = root.transform.Find("BattleUnit_2");
                    var renderer = entity.GetComponentInChildren<BattleNativeMonsterRenderer>();
                    var controller = entity.GetComponent<AnimationController>();
                    var definition = combat.CreateSkill(skill, 0, item, 1, 1, 0, new List<FightPropertyValue>());
                    var cast = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = skill,
                        TargetIds = new List<int> { 2 }, Presentation = FightBattleSkillCastPresentation.Capture(source, definition,
                            new List<FightBattleUnitState> { target }) };
                    var step = new FightBattleStepEvent { ActorId = 1 };
                    step.SkillCasts.Add(cast);
                    scene.Refresh(checkpoint, step);
                    nativeGraphPlayback.Play(step, checkpoint, id => id == 1 ? entity : id == 2 ? targetEntity : null);
                    Require(scope.PlayableSlot.GetComponentsInChildren<MyPlayable>(true).Any(value => value.myInited),
                        $"Configured original graph was not instantiated: {monster}/{skill}");
                    bool bound = false, animated = false;
                    var motions = new HashSet<string>();
                    for (int frame = 0; frame < 120; frame++)
                    {
                        nativeGraphPlayback.Tick(1f / 60); scene.Tick(1f / 60); nativeGraphPlayback.LateTick();
                        string motion = renderer.AnimatorFrame.Current.Animation?.Name;
                        if (!string.IsNullOrEmpty(motion)) motions.Add(motion);
                        animated |= motion != null && motion != "idle_01";
                        foreach (var leaf in scope.PlayableSlot.GetComponentsInChildren<MyPlayableAnimator>(true))
                            if (leaf.myInited)
                            {
                                Require(leaf.myApplyComponent == controller,
                                    $"Configured monster Animator leaf bound to another entity: {monster}/{skill}");
                                bound = true;
                            }
                        foreach (var leaf in scope.PlayableSlot.GetComponentsInChildren<MyPlayable>(true))
                        {
                            if (!leaf.myInited) continue;
                            var byBone = leaf.GetComponents<MonoBehaviour>().FirstOrDefault(value => value && value.GetType().Name == "ApplyByBone");
                            string bone = byBone ? (string)byBone.GetType().GetField("BoneName").GetValue(byBone) : null;
                            if (bone != null)
                            {
                                int origin = Convert.ToInt32(byBone.GetType().GetField("AttachAgentGoType").GetValue(byBone));
                                GameObject owner = origin == 1 ? leaf.mySource : origin == 2 ? leaf.myTarget : null;
                                var agent = owner ? owner.GetComponent<AttachAgent>() : null;
                                if (agent && agent.GetBone(bone)) continue;
                            }
                            else
                            {
                                if (leaf.myStatus == MyPlayable.PlayableStatus.Stop || leaf.myStatus == MyPlayable.PlayableStatus.Delay
                                    || leaf.myApplyComponent) continue;
                                var attribute = (MyPlayableApplyAttribute)Attribute.GetCustomAttribute(leaf.GetType(), typeof(MyPlayableApplyAttribute), true);
                                if (attribute == null || attribute.myTypes.Length == 0) continue;
                            }
                            Require(bone != null && proof.original_bone_gaps.Any(value => value.monster == monster
                                && value.skill == skill && value.bone == bone),
                                $"Configured original graph has an unexplained application gap: {monster}/{skill}/{leaf.GetType().Name}/{bone}");
                            unresolved.Add($"{monster}/{skill}/{bone}");
                        }
                        Require(!entity.Find("OfficialBattleAnimatorPlayables"), "Configured skill duplicated its flat animation cues.");
                        if (frame == 30)
                        {
                            var bounds = renderer.GetComponent<MeshRenderer>().bounds;
                            bounds.Encapsulate(targetEntity.GetComponentInChildren<MeshRenderer>().bounds);
                            camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.8f;
                            Capture(Path.Combine(output, $"configured-{monster}-{skill}.png"));
                        }
                        yield return 0;
                    }
                    // Some original skills, e.g. 64570, only dispatch effects;
                    // their source prefab deliberately has no Animator leaf.
                    Require(!hasAnimator || bound && animated, $"Configured original skill did not bind/animate: {monster}/{skill}");
                    rows.Add($"{monster}/{item}/{skill}:{string.Join(",", motions.OrderBy(value => value))}");
                    nativeGraphPlayback.Dispose(); nativeGraphPlayback = null;
                    scene.Dispose(); scene = null;
                    yield return 0;
                    Require(!entity && !targetEntity && !controller, "Configured monster skill leaked its scene/controller.");
                    casts++;
                }
            }
            Require(actors == expected.active_models && casts == expected.casts, "Configured monster group-zero skill coverage changed.");
            File.WriteAllText(Path.Combine(output, "configured-monster-graphs.txt"), string.Join("\n", rows));
            File.WriteAllText(Path.Combine(output, "configured-monster-bindings.json"), JsonUtility.ToJson(new ConfiguredGraphResult {
                actors = actors, casts = casts, unresolvedOriginalReferences = unresolved.OrderBy(value => value).ToArray() }));
            Debug.Log($"[Monster configured graph] completed: {actors} actual loadouts, {casts} original prefabs, {unresolved.Count} unresolved source references; full visual completion remains open.");
        }
    }
}
