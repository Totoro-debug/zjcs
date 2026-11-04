using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunPlayerAttachments()
        {
            RunPlayerActors();
            SessionState.SetBool(Key + ".player-attachments", true);
        }

        private static void CheckPlayerAttachmentRegistry(ProfessionNativeBattlePlayerActor actor)
        {
            var data = (Dictionary<string, object>)OfficialEquipmentJson.Parse(Resources.Load<TextAsset>("Combat/player-attachments").text);
            var rows = ((List<object>)data["nodes"]).Cast<Dictionary<string, object>>().ToArray();
            Require(rows.Length == 69 && actor.Attachments.Transforms.Count == 69, "all original attachment Transform branches");
            var agent = actor.Attachments.Agent;
            Require(agent.gameObject == actor.Root && agent.myBoneNameList.Count == 49, "original entity registry and 49 serialized entries");
            var entry = rows.SelectMany(row => ((List<object>)row["components"]).Cast<Dictionary<string, object>>())
                .Single(row => Equals(row["type"], "AttachAgent"));
            var fields = (Dictionary<string, object>)entry["fields"];
            var names = ((List<object>)fields["myBoneNameList"]).Cast<string>().ToArray();
            var references = (Dictionary<string, object>)entry["references"];
            Require(names.SequenceEqual(agent.myBoneNameList), "registry preserves exact original names and ordering");
            for (int index = 0; index < names.Length; index++)
            {
                var expected = references.TryGetValue("/myBoneList/" + index, out var reference)
                    ? actor.Attachments.Transforms[(string)((Dictionary<string, object>)reference)["sourceKey"]] : null;
                Require(agent.myBoneList[index] == expected && agent.GetBone(names[index]) == expected,
                    "original registry reference/null preserved: " + names[index]);
            }
            foreach (var row in rows)
            {
                string path = (string)row["path"];
                var actual = path.Length == 0 ? actor.Root.transform : actor.Root.transform.Find(path);
                Require(actual == actor.Attachments.Transforms[(string)row["sourceKey"]], "exact original attachment path: " + path);
                Require((actual is RectTransform) == Equals(row["transformType"], "RectTransform"), "original attachment transform type");
            }
            Require(actor.Attachments.Followers.Count == 3 && actor.Attachments.Followers.Count(f => f.enabled) == 1,
                "original shadow follower enabled; two original weapon attachment followers disabled");
            Require(AttachPosRotCfg.GetBone(actor.Root, "body") == actor.Root.transform.Find("3100_view/body"),
                "original skill attachment resolves body instead of falling back to the entity root");
            var shadow = actor.Attachments.Followers.Single(f => f.enabled);
            var skeleton = (Zjcs.Official.ProfessionUI.IProfessionFollowerSkeleton)actor.Renderer.State;
            var bone = skeleton.FindBone("shadow");
            float before = actor.Renderer.PlayerPhysicsTime;
            shadow.ForceUpdate();
            Require(Vector3.Distance(shadow.transform.position,
                    actor.Renderer.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0))) < .00001f
                && actor.Renderer.PlayerPhysicsTime == before, "original shadow follower consumes the current native pose without advancing physics");
            Debug.Log("Original 3100 player attachments passed: 69 source paths, 49 exact ordered registry entries including nulls, original Transform types and three application followers.");
        }

        private static IEnumerator CheckPlayerAttachmentGraphs(ProfessionNativeBattlePlayerActor actor,
            OfflineBattleNativePlayableScope scope, int profession, string label, Action<string> capture)
        {
            int id = profession == 1 ? 11170 : 10500;
            var prefab = Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/" + id + "/playable" + id);
            Require(prefab, "complete original skill graph exists");
            var clip = prefab.GetComponentsInChildren<MyPlayableOverrideAnimator>(true).Single().OverrideClip;
            Require(clip && clip.name.StartsWith(profession == 1 ? "100/" : "200/"), "original profession skill clip selection");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var state = actor.Renderer.State; float before = actor.Renderer.PlayerPhysicsTime;
                var graph = scope.Play(prefab, actor.Root, actor.Root);
                Require(graph && graph.mySource == actor.Root, "complete original graph source is entity 3100");
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                ((AnimatorOverrideController)actor.Animator.runtimeAnimatorController).GetOverrides(overrides);
                Require(overrides.Any(pair => pair.Key.name == "Skill" && pair.Value == clip), "original graph overrides the root-owned Skill clip");
                bool sampled = false; float elapsed = 0;
                bool attached = false, emitted = false;
                float[] sampleTimes = repeat == 0 ? new[] { .2f, .45f, .75f } : new[] { .45f };
                int nextSample = 0;
                for (int frame = 0; elapsed < 3.5f && frame < 1000; frame++)
                {
                    scope.Tick(Time.deltaTime); scope.LateTick(); elapsed += Time.deltaTime;
                    foreach (var leaf in graph ? graph.GetComponentsInChildren<MyPlayableAttachToBone>(true) : Array.Empty<MyPlayableAttachToBone>())
                        if (leaf.myStatus == MyPlayable.PlayableStatus.Play && leaf.myApplyComponent)
                        {
                            Require(leaf.myApplyComponent.transform.parent == leaf.myAttach.GetBone(leaf),
                                "actual skill effect is parented to its original registry target");
                            attached = true;
                        }
                    foreach (var leaf in graph ? graph.GetComponentsInChildren<MyPlayableFollowBone>(true) : Array.Empty<MyPlayableFollowBone>())
                        if (leaf.myStatus == MyPlayable.PlayableStatus.Play && leaf.myApplyComponent)
                        {
                            var bone = leaf.myAttach.GetBone(leaf);
                            Require(bone && Vector3.Distance(leaf.myApplyComponent.transform.position,
                                bone.TransformPoint(leaf.myAttach.myOffset)) < .0001f,
                                "original world-follow effect resolves its exact registry target at nonzero world position");
                            attached = true;
                        }
                    emitted |= actor.Root.GetComponentsInChildren<ParticleSystem>().Any(system => system.particleCount > 0);
                    if (graph) emitted |= graph.GetComponentsInChildren<ParticleSystem>().Any(system => system.particleCount > 0);
                    yield return null;
                    if (actor.Animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip == clip && info.weight > .7f))
                    {
                        sampled = true;
                        if (nextSample < sampleTimes.Length && actor.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= sampleTimes[nextSample])
                        {
                            capture(label + "-skill-" + id + "-" + repeat + "-" + nextSample);
                            nextSample++;
                        }
                    }
                }
                Require(sampled && actor.Renderer.State == state && actor.Renderer.PlayerPhysicsTime > before,
                    "actual original skill state renders while retaining the physical owner");
                Require(nextSample == sampleTimes.Length, "actual skill motion captured at configured progression points");
                Require(attached && emitted, "original graph actually attaches and emits its particle effect");
                Require(!graph || graph.myStatus == MyPlayable.PlayableStatus.Stop, "complete original graph reaches its configured end");
                Debug.Log("Original 3100 complete skill graph passed: " + label + "; skill=" + id + "; repeat=" + repeat + "; clip=" + clip.name);
            }
        }
    }
}
