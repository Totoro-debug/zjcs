using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static partial class OfficialBattleMonsterRuntimeValidation
    {
        private static IEnumerable<int> AnimatorSteps()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            assets = BattleNativeMonsterAssets.Load();
            target = new RenderTexture(450, 800, 24, RenderTextureFormat.ARGB32); target.Create();
            camera = new GameObject("Monster Animator validation camera").AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .16f, .2f); camera.targetTexture = target;
            var light = new GameObject("Animator validation light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            var failures = new List<string>();
            int models = 0, states = 0, samples = 0, empty = 0, carriers = 0, transitions = 0;
            foreach (int id in assets.EntityIds.OrderBy(value => value))
            {
                var native = BattleNativeMonsterRenderer.Create(assets, id, null, "BattleUnit_" + id);
                root = native.EntityRoot;
                native.BindOriginalAnimator(assets, id);
                var controller = (AnimatorController)native.Animator.runtimeAnimatorController;
                var layer = controller.layers.Single();
                if (layer.stateMachine.stateMachines.Length != 0) throw new InvalidDataException("Bind nested original monster states.");
                var reference = new ProfessionNativeAppearanceSkeleton(native.State.Source);
                reference.SetSkin(native.State.Skin);
                var physics = reference.Source.Constraints.Any(value => value.Kind == "physics")
                    ? new ProfessionNativePlayerPhysicsPose(reference.Source) : null;
                foreach (var child in layer.stateMachine.states)
                {
                    if (!child.state.motion) { empty++; continue; }
                    if (child.state.motion is not AnimationClip clip) throw new InvalidDataException("Unverified original monster blend tree.");
                    if (!native.AnimationNames.Contains(clip.name)) { carriers++; continue; }
                    string path = layer.name + "." + child.state.name;
                    try
                    {
                        native.Animator.Rebind(); native.Animator.Update(0);
                        native.Animator.Play(path, 0, 0); native.Animator.Update(0);
                        for (int sample = 0; sample <= 8; sample++)
                        {
                            float delta = sample == 0 ? 0 : clip.length / 8;
                            native.Advance(delta);
                            ApplyReference(native, reference, physics, delta);
                            ComparePose(native.State, reference);
                            samples++;
                        }
                        states++;
                    }
                    catch (Exception error) { failures.Add(id + " " + path + ": " + error); }
                }
                // Let original state transitions run, driven by original parameters.
                foreach (var child in layer.stateMachine.states.Where(value => value.state.motion is AnimationClip
                    && native.AnimationNames.Contains(value.state.motion.name)))
                {
                    native.Animator.Rebind(); native.Animator.Update(0);
                    native.Animator.Play(layer.name + "." + child.state.name, 0, 0); native.Animator.Update(0);
                    try
                    {
                        for (int frame = 0; frame < 12; frame++)
                        {
                            native.Advance(.08f);
                            ApplyReference(native, reference, physics, .08f);
                            ComparePose(native.State, reference);
                            if (!native.Animator.IsInTransition(0)) continue;
                            if (!native.AnimatorFrame.Incoming.HasValue) throw new InvalidDataException("Original Animator transition was not mixed.");
                            transitions++;
                        }
                    }
                    catch (Exception error) { failures.Add(id + " transition " + child.state.name + ": " + error); }
                }
                native.Animator.Rebind(); native.Animator.Update(0); native.Advance(.25f);
                Bounds bounds = native.GetComponent<MeshRenderer>().bounds;
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.2f;
                yield return 0; yield return 0;
                RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(work, "monster-animator-" + id + ".png"), pixels.EncodeToPNG());
                var colors = pixels.GetPixels32(); var background = colors[0];
                if (colors.Count(p => Math.Abs(p.r-background.r)>8 || Math.Abs(p.g-background.g)>8 || Math.Abs(p.b-background.b)>8) < 100
                    || colors.Count(p => p.r>240 && p.b>240 && p.g<15) > 100)
                    failures.Add(id + " original Animator rendering is invalid.");
                UnityEngine.Object.Destroy(pixels); RenderTexture.active = previous;
                native.Release(); root = null; models++;
                yield return 0;
            }
            File.WriteAllText(Path.Combine(work, "monster-animator-result.json"), JsonUtility.ToJson(new AnimatorResult {
                models=models, states=states, samples=samples, transitions=transitions, emptyStates=empty,
                unboundCarriers=carriers, failures=failures.ToArray() }, true));
            if (models != 60 || states + empty + carriers != 569 || samples != states * 9 || transitions == 0)
                throw new InvalidDataException("Original monster Animator coverage differs from its verified inventory.");
            if (failures.Count != 0) throw new InvalidDataException("Original monster Animator validation failed: " + failures.Count);
            Debug.Log("[Original monster Animator] models=" + models + " states=" + states + " samples=" + samples + " transitionFrames=" + transitions);
        }
        private static void ComparePose(ProfessionNativeAppearanceSkeleton actual, ProfessionNativeAppearanceSkeleton expected)
        {
            for (int bone = 0; bone < actual.Pose.Count; bone++)
                for (int axis = 0; axis < 16; axis++)
                    if (!float.IsFinite(actual.Pose.World(bone)[axis])
                        || Mathf.Abs(actual.Pose.World(bone)[axis] - expected.Pose.World(bone)[axis]) > .001f)
                        throw new InvalidDataException("Original Animator bone pose differs: " + bone + " axis=" + axis);
            var a = actual.CreateDrawSlots(); var b = expected.CreateDrawSlots();
            for (int slot = 0; slot < a.Length; slot++)
                if (a[slot].Index != b[slot].Index || !ReferenceEquals(a[slot].Attachment, b[slot].Attachment)
                    || !a[slot].Color.Equals(b[slot].Color)) throw new InvalidDataException("Original Animator slot pose differs: " + slot);
        }
        private static void ApplyReference(BattleNativeMonsterRenderer native, ProfessionNativeAppearanceSkeleton reference,
            ProfessionNativePlayerPhysicsPose physics, float delta)
        {
            var current = native.Animator.GetCurrentAnimatorClipInfo(0).Single();
            var state = native.Animator.GetCurrentAnimatorStateInfo(0);
            var outgoing = new ProfessionNativeAnimationChannels(reference.Source, current.clip.name, originalBezierSegments: true);
            float from = state.normalizedTime * current.clip.length;
            from = state.loop && outgoing.Duration > 0 ? Mathf.Repeat(from, outgoing.Duration) : Mathf.Clamp(from, 0, outgoing.Duration);
            if (native.AnimatorFrame.Current.Clip != current.clip || Mathf.Abs(native.AnimatorFrame.Current.Time - from) > .00001f)
                throw new InvalidDataException("Original Animator clock or clip identity differs.");
            if (native.Animator.IsInTransition(0))
            {
                var next = native.Animator.GetNextAnimatorClipInfo(0).Single();
                var nextState = native.Animator.GetNextAnimatorStateInfo(0);
                var incoming = new ProfessionNativeAnimationChannels(reference.Source, next.clip.name, originalBezierSegments: true);
                float to = nextState.normalizedTime * next.clip.length;
                to = nextState.loop && incoming.Duration > 0 ? Mathf.Repeat(to, incoming.Duration) : Mathf.Clamp(to, 0, incoming.Duration);
                if (physics != null) physics.ApplyMixNext(reference, outgoing, from, state.loop, incoming, to, nextState.loop, next.weight, delta);
                else ProfessionNativeOrderedConstraintPose.EvaluateMixNextBeforePhysics(reference,
                    outgoing, from, state.loop, incoming, to, nextState.loop, next.weight);
            }
            else if (physics != null) physics.Apply(reference, outgoing, from, state.loop, delta);
            else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(reference, outgoing, from, state.loop);
        }
        [Serializable] private sealed class AnimatorResult
        { public int models,states,samples,transitions,emptyStates,unboundCarriers; public string[] failures; }
    }
}
