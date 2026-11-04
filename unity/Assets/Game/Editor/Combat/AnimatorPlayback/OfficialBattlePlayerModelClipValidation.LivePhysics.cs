using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunLivePlayerPhysics()
        {
            Run(); SessionState.SetBool(Key + ".player-live-physics", true);
        }

        private static IEnumerator CheckLivePlayerPhysics(ProfessionNativeSceneRenderer native,
            ProfessionNativeSceneWeaponRenderer weapon, string group)
        {
            var animator = native.GetComponent<Animator>();
            var originalController = animator.runtimeAnimatorController;
            var originalPosition = native.transform.position;
            var source = native.State.Source;
            var controller = Resources.Load<RuntimeAnimatorController>("Combat/GameAnimators/Anim/"
                + (source.Sex == "female" ? "Female_" : "Male_") + (group == "100" ? "Zhanshi" : "Fashi") + "_Controller");
            Require(controller, "original Game controller for live body physics");
            var bounds = native.GetComponent<MeshRenderer>().bounds;
            bounds.Encapsulate(weapon.GetComponent<MeshRenderer>().bounds); bounds.Expand(2);
            var cameraObject = new GameObject("Live player physics camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.aspect = .75f; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .75f) * 1.4f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            var target = new RenderTexture(384, 512, 24); camera.targetTexture = target;
            string label = source.Sex + "-" + group + "-live-physics";
            int lastFrame = -1, updates = 0, mixed = 0, physicalChanges = 0;
            float expected = 0;
            bool explicitDrawing = false;
            ProfessionPartClipApplied notice = (_, __, ___, weight, ____, _____, ______) =>
            {
                if (explicitDrawing) return;
                if (lastFrame != Time.frameCount)
                {
                    lastFrame = Time.frameCount; expected += Time.deltaTime; updates++;
                    Require(Mathf.Abs(native.PlayerPhysicsTime - expected) < .0005f,
                        "one actual LateUpdate physical advance per frame, including mixed motions");
                    if (updates <= 5)
                    {
                        float clock = native.PlayerPhysicsTime;
                        explicitDrawing = true;
                        try { native.UpdateAnimationAndMesh(); }
                        finally { explicitDrawing = false; }
                        Require(native.PlayerPhysicsTime == clock, "redraw inside a part notification does not advance physics again");
                    }
                }
                if (weight > 0 && weight < 1) mixed++;
            };
            Matrix4x4[] Matrices() => Enumerable.Range(0, native.State.Pose.Count).Select(native.State.Pose.World).ToArray();
            var beforePhysics = new ProfessionNativeAppearanceSkeleton(source); beforePhysics.SetSkin(native.State.Skin);
            var referencePose = new ProfessionNativeAnimatorPose(animator,
                (state, motion, time, loop) => ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, motion, time, loop),
                (state, mix) => ProfessionNativeConstraintPose.EvaluateMixNextBeforePhysics(state,
                    mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                    mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight), originalBezierSegments: true);
            void Redraw()
            {
                float clock = native.PlayerPhysicsTime; var before = Matrices(); explicitDrawing = true;
                try { native.UpdateAnimationAndMesh(); native.UpdateAnimationAndMesh(); }
                finally { explicitDrawing = false; }
                Require(native.PlayerPhysicsTime == clock && before.SequenceEqual(Matrices()),
                    "repeated mesh/pose requests preserve completed physical history");
            }
            try
            {
                animator.runtimeAnimatorController = controller;
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
                native.BindPlayerAnimator(animator); native.UpdateAnimationAndMesh();
                native.OnClipApplied += notice;
                for (int i = 0; i < 8; i++) yield return null;
                Redraw(); var idle = Capture(bounds, label + "-idle");
                animator.CrossFadeInFixedTime("Damage", .5f, 0, 0);
                for (int i = 0; i < 3; i++) yield return null;
                Redraw(); var hit = Capture(bounds, label + "-hit");
                Require(!idle.SequenceEqual(hit), "actual live hit changes rendered original body pixels");
                for (int i = 0; i < 30; i++)
                {
                    yield return null; Redraw(); referencePose.Apply(beforePhysics);
                    if (Enumerable.Range(0, native.State.Pose.Count).Any(bone =>
                        Enumerable.Range(0, 16).Any(axis => Mathf.Abs(native.State.Pose.World(bone)[axis]
                            - beforePhysics.Pose.World(bone)[axis]) > .0001f))) physicalChanges++;
                }
                Require(mixed > 1 && updates > 20, "actual mixed and unmixed LateUpdate physical frames");
                Require(physicalChanges > 5, "actual physical body differs from the same Animator's pre-physics pose");
                animator.SetBool("die", true);
                float deadline = Time.time + 8;
                while (Time.time < deadline && !animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die")) yield return null;
                Require(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die"), "live original death state");
                for (int i = 0; i < 8; i++) yield return null;
                Redraw(); Capture(bounds, label + "-death");
                float clock = native.PlayerPhysicsTime;
                native.enabled = false;
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime == clock, "disabled renderer retains physical history without accumulating time");
                native.enabled = true;
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime > clock, "reenabled renderer resumes retained physical clock");
                clock = native.PlayerPhysicsTime; native.gameObject.SetActive(false);
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime == clock, "inactive pooled model retains physical history");
                native.gameObject.SetActive(true);
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime > clock, "reactivated model resumes physical history");
                native.transform.position += Vector3.right * 100000;
                for (int i = 0; i < 4; i++) yield return null;
                Require(native.UpdateMode == 0, "actual camera culling selects original invisible update mode");
                clock = native.PlayerPhysicsTime;
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime == clock, "actual invisible model retains physical time");
                native.transform.position = originalPosition;
                for (int i = 0; i < 4; i++) yield return null;
                Require(native.PlayerPhysicsTime > clock, "actual visible model resumes retained physical time");
                animator.SetBool("die", false);
                deadline = Time.time + 8;
                while (Time.time < deadline && animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die")) yield return null;
                for (int i = 0; i < 8; i++) yield return null;
                Require(!animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die"), "live original revival transition");
                clock = native.PlayerPhysicsTime; animator.Play("Idle", 0, 0);
                for (int i = 0; i < 3; i++) yield return null;
                Require(native.PlayerPhysicsTime > clock, "motion restart retains physical clock");
                Redraw(); Capture(bounds, label + "-revived");
                Require(Matrices().All(matrix => Enumerable.Range(0, 16).All(axis => float.IsFinite(matrix[axis]))),
                    "finite live body physical matrices after mixing, death and revival");
                Debug.Log("Native live player physics passed: " + label + ", " + updates + " actual LateUpdate frames, "
                    + mixed + " mixed frames, " + physicalChanges + " physical poses; repeated redraw, disable, inactive pool, culling, resume, death, revival and motion restart.");
            }
            finally
            {
                native.OnClipApplied -= notice; native.enabled = true; native.gameObject.SetActive(true);
                native.transform.position = originalPosition;
                animator.SetBool("die", false); animator.runtimeAnimatorController = originalController; animator.Update(0);
                native.BindAnimatorPose(new ProfessionNativeAnimatorPose(animator, (state, motion, time, loop) =>
                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, motion, time, loop)));
                // Restore the existing explicit clipping diagnostic's source pose.
                var idle = new ProfessionNativeAnimationChannels(source, group + "/idle_01", originalBezierSegments: true);
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(native.State, idle, .4f, false);
                native.LateUpdateMesh(); native.enabled = false;
                camera.enabled = false; camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(target);
            }
        }
    }
}
