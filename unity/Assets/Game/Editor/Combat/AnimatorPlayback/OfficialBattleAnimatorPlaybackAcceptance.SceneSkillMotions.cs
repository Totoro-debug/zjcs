using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneSkillMotions()
        {
            RunSceneBoundMixNext(); SessionState.SetBool(Key + ".sceneSkillMotions", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 1200);
        }

        // This is a graph/pose probe, not a replacement skill rule or cue prefab.
        // It crosses the existing original playable with real current skill clips.
        private static OfficialBattleSkillPlayableCue SceneMotionProbeCue() => new OfficialBattleSkillPlayableCue {
            kind = 1, origin_clip_name = "Skill", trigger_param = "skill", my_duration = 0,
            end_resume_param = false, speed = 1
        };

        private static ProfessionNativeSceneMotionCatalog SceneMotionCatalog()
        {
            var catalog = Resources.Load<ProfessionNativeSceneMotionCatalog>("Combat/SceneAppearance/player-motions");
            Require(catalog && catalog.Entries.Count == 903, "complete selected original male/female scene motion closure");
            Require(catalog.Entries.Count(row => row.Sex == "male") == 448
                && catalog.Entries.Count(row => row.Sex == "female") == 455,
                "exact current source sex identities and complete clip counts");
            return catalog;
        }

        private static IEnumerator ObserveSceneSkillMotion(ProfessionNativeSceneRenderer renderer,
            Animator animator, string suffix)
        {
            var catalog = SceneMotionCatalog();
            string sex = renderer.State.Source.Sex;
            string group = animator.runtimeAnimatorController.name.Contains("Fashi") ? "200" : "100";
            var motion = catalog.Load(sex, group + "/skill_41");
            Require(motion && motion.name == group + "/skill_41", "real current skill motion, not a hit/idle substitute");
            var original = animator.runtimeAnimatorController; float speed = animator.speed;
            var transform = renderer.transform.localToWorldMatrix;
            var notices = new List<SceneMixNotice>();
            var weaponRoot = renderer.GetComponent<AppearanceRendererPartHandler>().LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            ProfessionPartClipApplied applied = (animation, name, layer, weight, time, previous, backward) =>
            {
                if (name == motion.name && weight == 1)
                    notices.Add(new SceneMixNotice(animation, name, weight, time, previous));
            };
            try
            {
                ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
                var before = Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
                renderer.OnClipApplied += applied;
                using (var playback = new OfficialBattleAnimatorPlayback(animator, renderer.transform))
                {
                    animator.speed = .1f; playback.Play(SceneMotionProbeCue(), motion);
                    for (int frame = 0; notices.Count < 2 && frame < 180; frame++) yield return null;
                    Require(notices.Count >= 2 && notices[notices.Count - 1].Frame > notices[0].Frame
                        && notices[notices.Count - 1].Time > notices[0].Time
                        && animator.GetCurrentAnimatorStateInfo(0).IsName("Skill")
                        && animator.GetCurrentAnimatorClipInfo(0).Single().clip == motion,
                        "actual original playable selects a real skill and actual renderer LateUpdate advances its native clock");
                    Require(before.Where((matrix, i) => matrix != renderer.State.Pose.World(i)).Any(),
                        "actual original skill changes native body matrices without manual sampling or mesh calls");
                    weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                    string label = "skill-live-" + sex + "-" + original.name + suffix;
                    AssertSceneMixPortrait(CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label));
                    Require(renderer.transform.localToWorldMatrix == transform, "real skill preserves caller scale and blood-bar anchor");
                    Debug.Log("[Native current skill live] " + label + ": real current " + motion.name
                        + ", original playable, actual LateUpdate body matrices/clock and complete portrait passed before physics.");
                }
            }
            finally
            {
                renderer.OnClipApplied -= applied; animator.speed = speed;
                Require(animator.runtimeAnimatorController == original, "real skill playable restores the selected source controller");
                ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
            }
        }

        private static void CheckSceneSkillMotions(PlayerAnimatorAppearance player)
        {
            var renderer = player.GetComponent<ProfessionNativeSceneRenderer>(); var animator = player.Animator;
            var catalog = SceneMotionCatalog(); var state = renderer.State;
            var constantIk = new ProfessionNativeAnimationChannels(state.Source, "200/skill_14")
                .Constraints.SampleIk(5f / 12f, state.SkinConstraints);
            Require(constantIk.Single(value => value.Name == "IK_tuiL_2").Mix == 1
                && constantIk.Single(value => value.Name == "IK_tuiL_3").Mix == 1,
                "real current skill constant IK keys stay exactly one without weighted-curve rounding overshoot");
            string sex = state.Source.Sex, group = animator.runtimeAnimatorController.name.Contains("Fashi") ? "200" : "100";
            var original = animator.runtimeAnimatorController; var transform = renderer.transform.localToWorldMatrix;
            var weaponRoot = renderer.GetComponent<AppearanceRendererPartHandler>().LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var names = state.Source.AnimationNames.Where(name => name.StartsWith(group + "/skill_", StringComparison.Ordinal)).OrderBy(name => name).ToArray();
            Require(names.Length > 0 && names.Length == catalog.Entries.Count(row => row.Sex == sex && row.Clip.name.StartsWith(group + "/skill_", StringComparison.Ordinal)),
                "every actual current profession skill motion is bound without historical clip substitution");
            foreach (string name in state.Source.AnimationNames)
            {
                var clip = catalog.Load(sex, name);
                Require(clip && clip.name == name && ReferenceEquals(clip, catalog.Load(sex, name)),
                    "complete current scene clip lookup retains source name and actual Unity object identity");
            }
            Require(!catalog.Load("missing", names[0]) && !catalog.Load(sex, "missing/skill"),
                "unknown source identity is not replaced by another sex, idle or map motion");
            string label = "skill-entry-" + sex + "-" + original.name + (player.AnimController ? "-controller" : "-direct");
            int motions = 0, poses = 0, captures = 0, hidden = 0;
            ProfessionNativeAnimatorPose.AppliedFrame Draw()
            {
                ProfessionNativeAnimatorPose.AppliedFrame frame;
                try { frame = renderer.ApplyAnimatorPose(); }
                catch (ArgumentException error)
                {
                    var clip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
                    var channels = new ProfessionNativeAnimationChannels(state.Source, clip.name);
                    float time = animator.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length;
                    if (clip.isLooping && channels.Duration > 0) time %= channels.Duration;
                    var constraints = channels.Constraints.SampleIk(time, state.SkinConstraints);
                    throw new InvalidOperationException("Current original skill " + clip.name + " at " + time.ToString("R")
                        + "; IK " + string.Join(", ", constraints.Select(value => value.Name + ":mix=" + value.Mix.ToString("R")
                            + ":order=" + value.Order + ":bones=" + value.Bones.Length)), error);
                }
                renderer.LateUpdateMesh();
                weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                return frame;
            }
            void Seek(float normalized)
            { animator.Play("Skill", 0, normalized); animator.Update(0); }
            try
            {
                BindSceneMixNext(renderer, animator);
                ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
                var idleDrawnSlots = state.CreateDrawSlots().Where(slot => slot.Active && slot.Attachment is ProfessionNativeRenderAttachment)
                    .Select(slot => state.SlotPose.Name(slot.Index)).ToArray();
                Require(idleDrawnSlots.Length > 10, "actual current idle supplies the visible source body slot baseline");
                using (var playback = new OfficialBattleAnimatorPlayback(animator, renderer.transform))
                {
                    foreach (string name in names)
                    {
                        var clip = catalog.Load(sex, name);
                        ResetSceneMixAnimator(animator); playback.Play(SceneMotionProbeCue(), clip);
                        animator.Update(.01f); animator.Update(.01f);
                        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Skill") && animator.GetCurrentAnimatorClipInfo(0).Single().clip == clip,
                            "original playable actually installs each current motion by clip identity: " + name);
                        Seek(.25f); var first = Draw(); CheckCurrentSceneMotionFrame(first, clip, state, animator); poses++;
                        var before = Enumerable.Range(0, state.Pose.Count).Select(state.Pose.World).ToArray();
                        bool capture = name == group + "/skill_06" || name == group + "/skill_41"
                            || group == "100" && (name == "100/skill_52" || name == "100/skill_61");
                        var bounds = capture ? SceneCharacterBounds(renderer, weapon) : default;
                        Seek(.65f); var next = Draw(); CheckCurrentSceneMotionFrame(next, clip, state, animator); poses++;
                        Require(next.Current.MotionTime > first.Current.MotionTime,
                            "native pose consumes actual current skill graph time at both source-relative samples: " + name);
                        var bodyParts = renderer.GetComponentsInChildren<ProfessionNativeSceneMeshPart>(true)
                            .Where(part => part.MeshRenderer.enabled && part.gameObject.activeInHierarchy
                                && !part.GetComponent<ProfessionNativeSceneWeaponRenderer>()).ToArray();
                        Require(bodyParts.All(part => part.Mesh.vertices.All(vertex =>
                            !float.IsNaN(vertex.x) && !float.IsInfinity(vertex.x) && !float.IsNaN(vertex.y) && !float.IsInfinity(vertex.y))),
                            "complete current motion yields finite real body geometry: " + name);
                        if (bodyParts.Sum(part => part.Mesh.vertexCount) == 0)
                        {
                            CheckCurrentSceneMotionHidden(state, next.Current, idleDrawnSlots); hidden++;
                        }
                        if (capture)
                        {
                            bounds.Encapsulate(SceneCharacterBounds(renderer, weapon));
                            Seek(.25f); Draw(); var early = CaptureSceneRendererCamera(bounds, label + "-" + name.Replace('/', '-') + "-early");
                            Seek(.65f); Draw(); var later = CaptureSceneRendererCamera(bounds, label + "-" + name.Replace('/', '-') + "-later");
                            AssertSceneMixPortrait(early); AssertSceneMixPortrait(later); captures += 2;
                            Require(!early.SequenceEqual(later) && before.Where((matrix, i) => matrix != state.Pose.World(i)).Any(),
                                "real skill source time changes actual current body matrices and GPU pixels: " + name);
                        }
                        playback.Stop(); motions++;
                    }
                    if (group == "100")
                        CheckSceneSkillColorMix(renderer, animator, catalog.Load(sex, "100/skill_52"), playback, weaponRoot, weapon, label);
                }
                Require(animator.runtimeAnimatorController == original && renderer.transform.localToWorldMatrix == transform,
                    "all current motion probes restore the source controller and retain caller model/HP-anchor transform");
                Debug.Log("[Native current skill motions] " + label + ": " + motions + " exact source motions, " + poses
                    + " real graph/native pose samples, " + captures + " representative camera frames, " + hidden
                    + " source-keyed empty body phases; original playable identity, source body visibility and controller restoration passed before physics.");
            }
            finally
            {
                animator.runtimeAnimatorController = original; ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
            }
        }

        private static void CheckCurrentSceneMotionHidden(ProfessionNativeAppearanceSkeleton state,
            ProfessionNativeAnimatorPose.Frame frame, string[] idleDrawnSlots)
        {
            Require(state.CreateDrawSlots().All(slot => !slot.Active || !(slot.Attachment is ProfessionNativeRenderAttachment)),
                "an empty rendered body has no active source render attachments");
            var tracks = (IReadOnlyDictionary<string, object>)frame.Animation.OriginalFields["slots"];
            foreach (string name in idleDrawnSlots)
            {
                Require(tracks.TryGetValue(name, out object fields), "empty body explicitly keys each visible original idle slot: " + name);
                var channels = (IReadOnlyDictionary<string, object>)fields;
                Require(channels.TryGetValue("attachment", out object raw), "empty body has an original attachment timeline: " + name);
                var reached = ((List<object>)raw).Cast<IReadOnlyDictionary<string, object>>()
                    .LastOrDefault(key => !key.TryGetValue("time", out object time) || Convert.ToSingle(time) <= frame.Time);
                Require(reached != null && (!reached.TryGetValue("name", out object attachment) || attachment == null)
                    && state.SlotPose.Attachment(state.Source.FindSlot(name)) == null,
                    "empty body follows a reached original null-attachment key, not missing geometry: " + name);
            }
        }

        private static void CheckCurrentSceneMotionFrame(ProfessionNativeAnimatorPose.AppliedFrame frame,
            AnimationClip clip, ProfessionNativeAppearanceSkeleton state, Animator animator)
        {
            Require(!frame.Incoming.HasValue && frame.Current.Clip == clip && frame.Current.Animation.Name == clip.name
                && ReferenceEquals(frame.Current.Animation.OriginalFields, state.Source.GetAnimation(clip.name))
                && Mathf.Abs(frame.Current.MotionTime - animator.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length) < .0001f,
                "real current carrier selects its exact current skeleton channels without a separate animation clock");
        }

        private static void CheckSceneSkillColorMix(ProfessionNativeSceneRenderer renderer, Animator animator,
            AnimationClip clip, OfficialBattleAnimatorPlayback playback, GameObject weaponRoot,
            ProfessionNativeSceneWeaponRenderer weapon, string label)
        {
            ResetSceneMixAnimator(animator); playback.Play(SceneMotionProbeCue(), clip);
            animator.Update(.01f); animator.Update(.01f);
            ResetSceneMixAnimator(animator);
            animator.CrossFadeInFixedTime("Skill", .2f, 0, 1f); animator.Update(.05f);
            var frame = renderer.ApplyAnimatorPose(); renderer.LateUpdateMesh();
            Require(frame.Incoming.HasValue && frame.Current.Animation.ColorTrackCount == 0
                && frame.Incoming.Value.Animation.ColorTrackCount > 0 && frame.IncomingWeight > 0 && frame.IncomingWeight < 1,
                "actual original skill transition reaches animated source slot colors");
            var incoming = frame.Incoming.Value;
            var slotTracks = (IReadOnlyDictionary<string, object>)incoming.Animation.OriginalFields["slots"];
            var slots = renderer.State.CreateDrawSlots().ToDictionary(slot => slot.Index);
            int checkedColors = 0, linearIntervals = 0, activeIntervals = 0;
            foreach (var pair in slotTracks)
            {
                var channels = (IReadOnlyDictionary<string, object>)pair.Value;
                if (!channels.TryGetValue("rgba", out object raw)) continue;
                var keys = ((List<object>)raw).Cast<IReadOnlyDictionary<string, object>>().ToArray();
                var earlier = keys.LastOrDefault(key => key.TryGetValue("time", out object value) && Convert.ToSingle(value) <= incoming.Time);
                var later = keys.FirstOrDefault(key => key.TryGetValue("time", out object value) && Convert.ToSingle(value) > incoming.Time);
                // Original skill_52 hides attachments during its zero-alpha hold.
                // Probe its original linear fade-in after those attachments return.
                if (earlier == null || later == null || earlier.ContainsKey("curve")) continue;
                int fromAlpha = Convert.ToByte(((string)earlier["color"]).Substring(6, 2), 16);
                int toAlpha = Convert.ToByte(((string)later["color"]).Substring(6, 2), 16);
                if (fromAlpha != 0 || toAlpha != 255) continue;
                linearIntervals++;
                int index = renderer.State.Source.FindSlot(pair.Key); var slot = slots[index];
                if (!slot.Active || slot.Attachment == null) continue;
                activeIntervals++;
                if (renderer.State.SlotPose.Color(index).a != 255) continue;
                float fromTime = Convert.ToSingle(earlier["time"]), toTime = Convert.ToSingle(later["time"]);
                float sourceAlpha = 255 * (incoming.Time - fromTime) / (toTime - fromTime);
                int expected = Mathf.RoundToInt(255 + (sourceAlpha - 255) * frame.IncomingWeight);
                Require(Mathf.Abs(slot.Color.a - expected) <= 1,
                    "actual MixNext blends the original linear alpha keys by the real incoming weight: " + pair.Key);
                checkedColors++;
            }
            Require(checkedColors > 10, "actual original skill fade exercises visible current head/body slot colors: motion="
                + incoming.Animation.Name + "; time=" + incoming.Time + "; weight=" + frame.IncomingWeight
                + "; linearIntervals=" + linearIntervals + "; activeIntervals=" + activeIntervals + "; checked=" + checkedColors);
            weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
            AssertSceneMixPortrait(CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-color-mix"));
            playback.Stop();
            Debug.Log("[Native current skill color mix] " + label + ": " + checkedColors
                + " active original slot alpha tracks follow actual Animator MixNext weight; independent weapon remains an explicit diagnostic pose.");
        }
    }
}
