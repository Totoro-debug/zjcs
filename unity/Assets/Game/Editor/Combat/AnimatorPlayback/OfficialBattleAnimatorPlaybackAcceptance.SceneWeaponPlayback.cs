using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneWeaponPlayback()
        {
            RunSceneMecanimParts();
            SessionState.SetBool(Key + ".sceneWeaponPlayback", true);
        }

        private static Matrix4x4[] WeaponMatrices(ProfessionNativeAppearanceSkeleton state)
            => Enumerable.Range(0, state.Pose.Count).Select(state.Pose.World).ToArray();

        private static void SameWeaponPose(ProfessionNativeAppearanceSkeleton actual,
            ProfessionNativeAppearanceSkeleton expected, string message)
        {
            for (int bone = 0; bone < actual.Pose.Count; bone++)
            for (int axis = 0; axis < 16; axis++)
                Require(Mathf.Abs(actual.Pose.World(bone)[axis] - expected.Pose.World(bone)[axis]) < .002f, message);
        }

        private static IEnumerator ObserveSceneWeaponPlayback(ProfessionNativeSceneRenderer body,
            AppearanceRendererPartHandler parts, string suffix)
        {
            var weapon = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject
                .GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            string skin = ((ProfessionNativeSkin)weapon.State.Skin).Name;
            string group = skin.Contains("100") ? "100" : "200";
            string label = "weapon-clock-" + body.State.Source.Sex + "-" + group + suffix;
            var expected = new ProfessionNativeAppearanceSkeleton(weapon.State.Source);
            expected.SetSkin(expected.Data.FindSkin(skin)); expected.SetToSetupPose();
            // All six exact source motions, loop boundaries, final-frame hold,
            // repeated application, same-name restart and independent ownership.
            var independent = new ProfessionNativeSceneWeaponPlayback();
            foreach (string name in weapon.AnimationNames)
            foreach (bool loop in new[] { false, true })
            {
                float duration = new ProfessionNativeAnimationChannels(expected.Source, name).Duration;
                independent.Play(expected, name, loop);
                var reference = new ProfessionNativeAppearanceSkeleton(expected.Source);
                reference.SetSkin(reference.Data.FindSkin(skin)); reference.SetToSetupPose();
                foreach (float delta in new[] { 0f, .37f, duration, duration + .19f })
                {
                    independent.Play(expected, name, loop); independent.Advance(delta); independent.Apply(expected);
                    reference.EvaluateUnmixedWeaponAnimation(name, duration == 0 ? 0 : loop ? delta % duration : Mathf.Min(delta, duration));
                    SameWeaponPose(expected, reference, "source loop/hold time and every original weapon bone");
                    var before = WeaponMatrices(expected); expected.SetToSetupPose(); independent.Apply(expected); independent.Apply(expected);
                    Require(before.SequenceEqual(WeaponMatrices(expected)), "skin/setup redraw neither advances nor resets playback");
                }
            }
            var cameraGo = new GameObject("Actual native weapon clock portrait camera");
            var camera = cameraGo.AddComponent<Camera>();
            var target = new RenderTexture(675, 1463, 24);
            var bounds = SceneCharacterBounds(body, weapon);
            camera.orthographic = true; camera.aspect = 675f / 1463;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.3f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.targetTexture = target;
            var observation = cameraGo.AddComponent<SceneWeaponClockObserver>(); observation.enabled = false;
            try
            {
                // Explicit original request: current Game body clips have no
                // corresponding part mapping, so no mapping is invented here.
                string motion = group + "/loop_L";
                weapon.PlayAnimation(motion, true);
                CaptureSceneRendererCamera(SceneCharacterBounds(body, weapon), label + "-start");
                yield return null; yield return null;
                Require(weapon.GetComponent<MeshRenderer>().isVisible, "persistent portrait camera sees original independent weapon");
                weapon.PlayAnimation(motion, true);
                float start = Time.time;
                expected.EvaluateUnmixedWeaponAnimation(motion, 0);
                SameWeaponPose(weapon.State, expected, "application Play request applies frame zero immediately");
                observation.Weapon = weapon; observation.Expected = expected; observation.Animation = motion;
                observation.StartTime = start; observation.enabled = true;
                for (int frame = 0; observation.Samples < 4 && frame < 30; frame++) yield return null;
                observation.enabled = false;
                Require(observation.Samples >= 4 && observation.Changed, "real Update clock and LateUpdate draw advance the original weapon without explicit pose sampling");
                weapon.UpdateAnimationAndMesh(); var live = WeaponMatrices(weapon.State);
                weapon.State.SetToSetupPose(); weapon.UpdateAnimationAndMesh(); weapon.UpdateAnimationAndMesh();
                Require(live.SequenceEqual(WeaponMatrices(weapon.State)), "same-frame renderer redraw preserves current live time after setup reset");
                CaptureSceneRendererCamera(SceneCharacterBounds(body, weapon), label + "-advanced");
                weapon.enabled = false;
                var paused = WeaponMatrices(weapon.State);
                yield return null; yield return null;
                weapon.UpdateAnimationAndMesh();
                Require(paused.SequenceEqual(WeaponMatrices(weapon.State)), "disabled renderer retains its actual weapon clock");
                weapon.enabled = true;
                var retained = weapon.State;
                parts.SetSkins(new Dictionary<SkinType, string>());
                yield return null; yield return null;
                parts.SetSkins(new Dictionary<SkinType, string> { { SkinType.pWeapon, skin } });
                var rented = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject
                    .GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                rented.UpdateAnimationAndMesh();
                Require(weapon == rented && ReferenceEquals(retained, rented.State)
                    && paused.SequenceEqual(WeaponMatrices(rented.State)), "actual original pool return/rent pauses and retains this weapon's independent clock");
                float previousScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0; paused = WeaponMatrices(weapon.State);
                    yield return null; yield return null;
                    weapon.UpdateAnimationAndMesh();
                    Require(paused.SequenceEqual(WeaponMatrices(weapon.State)), "source scaled-time clock pauses with Unity time scale zero");
                }
                finally { Time.timeScale = previousScale; }
                camera.enabled = false;
                // Visibility changes are committed by Unity rendering, after
                // Update. Wait for that notification before asserting pause.
                for (int frame = 0; weapon.GetComponent<MeshRenderer>().isVisible && frame < 10; frame++) yield return null;
                Require(!weapon.GetComponent<MeshRenderer>().isVisible, "actual camera culling reaches original Nothing invisible update mode");
                weapon.UpdateAnimationAndMesh(); paused = WeaponMatrices(weapon.State);
                yield return null; yield return null;
                weapon.UpdateAnimationAndMesh();
                Require(paused.SequenceEqual(WeaponMatrices(weapon.State)), "original invisible Nothing update mode pauses live weapon time");
                camera.enabled = true;
                weapon.PlayAnimation(group + "/loop_R", false);
                expected.EvaluateUnmixedWeaponAnimation(group + "/loop_R", 0);
                SameWeaponPose(weapon.State, expected, "changing source animation resets and applies original right-facing pose immediately");
                CaptureSceneRendererCamera(SceneCharacterBounds(body, weapon), label + "-right");
                weapon.Initialize(true, false);
                expected.EvaluateUnmixedWeaponAnimation(group + "/" + group + "_loop", 0);
                SameWeaponPose(weapon.State, expected, "rebuild binds a fresh original skeleton and restarts original prefab animation");
                CaptureSceneRendererCamera(SceneCharacterBounds(body, weapon), label + "-rebuilt");
                Debug.Log("[Native weapon clock] " + label + ": six source motions, loop/hold/reset/redraw, actual visible Update/LateUpdate, disable, pool return/rent, scaled pause, invisible pause and rebuild passed; original Game part mappings unchanged.");
            }
            finally
            {
                weapon.enabled = true; camera.targetTexture = null;
                Object.DestroyImmediate(cameraGo); target.Release(); Object.DestroyImmediate(target);
            }
        }

        [DefaultExecutionOrder(10000)]
        public sealed class SceneWeaponClockObserver : MonoBehaviour
        {
            internal ProfessionNativeSceneWeaponRenderer Weapon;
            internal ProfessionNativeAppearanceSkeleton Expected;
            internal string Animation;
            internal float StartTime;
            internal int Samples;
            internal bool Changed;
            private Matrix4x4[] previous;
            private void LateUpdate()
            {
                float elapsed = Time.time - StartTime;
                float duration = new ProfessionNativeAnimationChannels(Expected.Source, Animation).Duration;
                Expected.EvaluateUnmixedWeaponAnimation(Animation, duration > 0 ? elapsed % duration : 0);
                SameWeaponPose(Weapon.State, Expected, "automatic rendered weapon clock matches actual scaled Unity time");
                var next = WeaponMatrices(Weapon.State);
                if (previous != null && !previous.SequenceEqual(next)) Changed = true;
                previous = next; Samples++;
            }
        }
    }
}
