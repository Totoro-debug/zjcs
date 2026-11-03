using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySounds;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunCasts()
        {
            RunScenePlayerAnimator(); SessionState.SetBool(Key + ".casts", true);
        }

        // The dry backend verifies exact source samples and dispatch, not Wwise DSP.
        private sealed class CastDryBackend : IProfessionSoundHost, IDisposable
        {
            private readonly GameObject root = new GameObject("Dry original cast sample validation only");
            private readonly AudioClip clip = Resources.Load<AudioClip>("Combat/Casts/Media/248412158");
            public readonly List<GameObject> Emitters = new List<GameObject>();
            public CastDryBackend(bool listener = true)
            {
                Require(clip && clip.channels == 2 && clip.frequency == 48000 && clip.samples == 129600,
                    "original cast sample dimensions");
                var values = new float[clip.samples * clip.channels]; Require(clip.GetData(values, 0), "cast PCM readable");
                var raw = new byte[values.Length * 2];
                for (int i = 0; i < values.Length; i++)
                {
                    short sample = (short)Mathf.Clamp(Mathf.RoundToInt(values[i] * 32768), short.MinValue, short.MaxValue);
                    raw[2 * i] = (byte)sample; raw[2 * i + 1] = (byte)(sample >> 8);
                }
                using var hash = SHA256.Create();
                Require(BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant()
                    == "ae864e568833337e2aacfd2a1dcf8b0271335038051115eb718d4a92d3b2874c", "exact native cast PCM hash");
                if (listener) root.AddComponent<AudioListener>();
            }
            public uint PostEvent(uint id, GameObject emitter)
            {
                Require(id == 3777790779, "original serialized cast event ID"); Emitters.Add(emitter);
                var voice = new GameObject("Dry Skill_10032"); voice.transform.SetParent(root.transform, false);
                var source = voice.AddComponent<AudioSource>(); source.spatialBlend = 0; source.clip = clip;
                voice.AddComponent<CastAudioProbe>(); source.Play(); return (uint)Emitters.Count;
            }
            public void Dispose() => Object.Destroy(root);
        }

        public sealed class CastAudioProbe : MonoBehaviour
        {
            public static int NonzeroBuffers;
            private void OnAudioFilterRead(float[] samples, int channels)
            {
                foreach (float sample in samples)
                    if (Math.Abs(sample) > .00001f) { Interlocked.Increment(ref NonzeroBuffers); break; }
            }
        }

        private static IEnumerator CheckCasts(PlayerAnimatorAppearance player)
        {
            var actor = player.gameObject; var native = actor.GetComponent<ProfessionNativeSceneRenderer>();
            var animator = player.Animator; var originalController = animator.runtimeAnimatorController;
            if (!originalController.animationClips.Any(clip => clip.name == "100/idle_01")) yield break;
            var originalSpeed = animator.speed; animator.speed = 0;
            var controller = actor.GetComponent<AnimationController>(); bool ownController = !controller;
            if (ownController) controller = actor.AddComponent<AnimationController>();
            bool originalCreateOverride = controller.CreateOverrideController;
            controller.myAnimator = animator; controller.NotCreateGraph = true; controller.CreateOverrideController = false;
            var temporaryController = new AnimatorOverrideController(originalController);
            controller.SetRuntimeAnimationController(temporaryController);
            var scopeRoot = new GameObject("Complete original cast validation");
            var target = new GameObject("Distinct original cast victim"); target.transform.SetParent(scopeRoot.transform, false);
            target.transform.position = actor.transform.position + new Vector3(1.3f, .4f, 0);
            var oldHost = OfficialWwiseEvent.Host;
            float captureDelta = Time.captureDeltaTime;
            var sampler = new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics);
            string label = "cast-" + native.State.Source.Sex + (ownController ? "-direct" : "-controller");
            void Draw()
            {
                sampler.ApplyUnmixed(native.State); native.LateUpdateMesh();
                foreach (var follower in actor.GetComponentsInChildren<SkeletonRendererFollower>(true)) follower.ForceUpdate();
                foreach (var weapon in actor.GetComponentsInChildren<ProfessionNativeSceneWeaponRenderer>(true)) weapon.UpdateAnimationAndMesh();
            }
            animator.Play("Idle", 0, 0); animator.Update(0); Draw();
            var geometry = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var bounds = geometry[0].bounds; foreach (var renderer in geometry.Skip(1)) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(new Vector3(2, 2, 0)); bounds.Encapsulate(target.transform.position);
            CaptureSceneRendererCamera(bounds, label + "-visibility"); Draw();
            CastAudioProbe.NonzeroBuffers = 0;
            using var dry = new CastDryBackend();
            using var other = new CastDryBackend(false);
            using var scope = new OfflineBattleNativePlayableScope(scopeRoot.transform, dry);
            IEnumerator Pump(int frames, float delta)
            {
                Time.captureDeltaTime = delta;
                for (int i = 0; i < frames; i++)
                {
                    yield return null; scope.Tick(delta); animator.speed = 1; animator.Update(delta); animator.speed = 0;
                    Draw(); scope.LateTick(); Require(OfficialWwiseEvent.Host == other, "scope restores unrelated global sound host");
                }
            }
            try
            {
                Time.captureDeltaTime = .01f; yield return null;
                OfficialWwiseEvent.Host = other;
                var prefab = Resources.Load<GameObject>("Combat/Casts/Prefabs/EcEntity/Fight/FightSkill/11170/playable11170");
                Require(prefab && prefab.GetComponentsInChildren<MyPlayable>(true).Length == 4, "all original parent playable nodes retained");
                var graph = scope.Play(prefab, actor, target); yield return Pump(2, .01f);
                var animation = graph.GetComponentInChildren<MyPlayableOverrideAnimator>(true);
                var effect = graph.GetComponentInChildren<MyPlayableFollowBone>(true);
                Require(animation.myApplyComponent == controller && animator.GetCurrentAnimatorClipInfo(0).Single().clip == animation.OverrideClip
                    && animation.OverrideClip.name == "100/skill_34", "complete graph applies original override before skill trigger");
                Require(dry.Emitters.Count == 1 && dry.Emitters[0] == target && other.Emitters.Count == 0,
                    "original sound node posts once on distinct victim through explicit backend");
                Require(!effect.myApplyComponent.gameObject.activeSelf, "native sequence holds effect before original .14-second offset");
                var firstPose = Enumerable.Range(0, native.State.Pose.Count).Select(native.State.Pose.World).ToArray();
                Draw(); CaptureSceneRendererCamera(bounds, label + "-cast-start");
                yield return Pump(18, .01f);
                Require(effect.myApplyComponent.gameObject.activeSelf && effect.Time > 0 && effect.Time < .1f,
                    "original delayed follow-bone leaf starts on parent clock");
                Require(firstPose.Where((matrix, i) => matrix != native.State.Pose.World(i)).Any(), "original cast motion advances native body bones");
                Require(Vector3.Distance(effect.myApplyComponent.transform.position, actor.transform.position) < .0001f,
                    "original missing body-node fallback follows source root");
                Require(graph.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.particleCount) > 0,
                    "complete cast graph emits original native particles");
                Draw(); CaptureSceneRendererCamera(bounds, label + "-effect-start");
                yield return Pump(45, .01f); Draw(); CaptureSceneRendererCamera(bounds, label + "-effect-advanced");
                scope.Stop(graph, true); yield return Pump(3, .01f);
                Require(!effect.myApplyComponent.gameObject.activeSelf && dry.Emitters.Count == 1,
                    "parent interruption stops visual leaf without fabricating a null stop event");
                var replay = scope.Play(prefab, actor, target); yield return Pump(2, .01f);
                Require(ReferenceEquals(graph, replay) && dry.Emitters.Count == 2 && !effect.myApplyComponent.gameObject.activeSelf,
                    "same pooled full graph resets delayed leaf and posts sound once again");
                scope.Stop(replay, true); yield return Pump(3, .01f);
                foreach (var entry in new[] { ("playable11171", true), ("playable11172", false) })
                {
                    var dependency = Resources.Load<GameObject>("Combat/Casts/Prefabs/EcEntity/Fight/FightSkill/11170/" + entry.Item1);
                    Require(dependency, "complete original cast dependency graph");
                    var child = scope.Play(dependency, actor, target); yield return Pump(8, .01f);
                    Require(child.mySource == actor && child.myTarget == target, "child visual graph retains original caster and victim");
                    if (entry.Item2) CheckTrackPosition(child.GetComponentInChildren<MyPlayableTrack>(true), false);
                    Require(child.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.particleCount) > 0,
                        "complete cast dependency retains its original particles");
                    Draw(); CaptureSceneRendererCamera(bounds, label + "-" + entry.Item1);
                    scope.Stop(child, true); yield return Pump(3, .01f);
                }
                yield return new WaitForSecondsRealtime(.2f);
                Require(Volatile.Read(ref CastAudioProbe.NonzeroBuffers) > 0, "original cast sample reaches actual native audio buffers");
                using (var independent = new OfflineBattleNativePlayableScope(scopeRoot.transform, other))
                {
                    var sibling = independent.Play(prefab, actor, target);
                    bool rejected = false;
                    try { scope.Stop(sibling, true); } catch (ArgumentException) { rejected = true; }
                    Require(rejected && sibling.gameObject.activeSelf && other.Emitters.Count == 1,
                        "one scope cannot interrupt another scope's complete graph");
                    // Only this temporary full-graph fixture configures a long
                    // sound with a stop event. Official serialized assets stay intact.
                    var fixture = Object.Instantiate(prefab, scopeRoot.transform); fixture.SetActive(false);
                    try
                    {
                        var sound = fixture.GetComponentInChildren<MyPlayableSound>(true);
                        sound.Duration = 10; sound.StopSoundEvent = sound.SoundEvent;
                        int before = dry.Emitters.Count;
                        scope.Play(fixture, actor, target);
                        Require(dry.Emitters.Count == before + 1, "complete fixture starts original sound once");
                        scope.Dispose();
                        Require(dry.Emitters.Count == before + 2 && dry.Emitters.Last() == target
                            && other.Emitters.Count == 1 && sibling.gameObject.activeSelf && OfficialWwiseEvent.Host == other,
                            "active scope disposal posts original stop through its own backend and preserves the other scope");
                    }
                    finally { Object.DestroyImmediate(fixture); }
                    independent.Stop(sibling, true);
                }
                Debug.Log("[Original complete cast] " + label + ": four parent nodes, original clip/trigger/.14 delay, source-root fallback, native particles, exact PCM, victim sound event, interruption, same-pool replay, cross-scope isolation and active stop-event teardown passed. Wwise DSP and formal entity body-node binding remain pending.");
            }
            finally
            {
                scope.Dispose(); OfficialWwiseEvent.Host = oldHost; Time.captureDeltaTime = captureDelta;
                controller.CreateOverrideController = false; controller.SetRuntimeAnimationController(originalController);
                controller.CreateOverrideController = originalCreateOverride;
                if (ownController) Object.DestroyImmediate(controller);
                if (temporaryController != originalController) Object.DestroyImmediate(temporaryController);
                animator.runtimeAnimatorController = originalController; animator.speed = originalSpeed;
                animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(native.State,
                    new ProfessionNativeAnimationChannels(native.State.Source, "100/idle_01"), .4f, false);
                native.LateUpdateMesh(); Object.DestroyImmediate(scopeRoot);
            }
        }
    }
}
