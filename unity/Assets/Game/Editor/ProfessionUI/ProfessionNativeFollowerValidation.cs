using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeFollowerValidation
    {
        [Serializable] private sealed class Fixture { public Frame[] cases; }
        [Serializable] private sealed class Frame { public string sex, animation; public string[] skins; public float time; public World[] expected; }
        [Serializable] private sealed class World { public int bone; public float a, b, c, d, x, y; }
        private const string Active = "Zjcs.ProfessionNativeFollowers.Active";
        private static bool ready;
        static ProfessionNativeFollowerValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 300));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native follower validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("idle-transform-validation.json")));
            Require(fixture.cases.Length == 96, "all verified independent source frames");
            int boneViews = 0, slotViews = 0, updates = 0;
            foreach (var group in fixture.cases.GroupBy(f => f.sex))
            {
                var source = ProfessionNativeSkeletonData.LoadResource(group.Key);
                var state = new ProfessionNativeAppearanceSkeleton(source);
                IProfessionFollowerSkeleton skeleton = state;
                var boneRows = (List<object>)source.SourceFields["bones"];
                var node = new GameObject("Actual native follower target " + group.Key);
                var graphic = node.AddComponent<GraphicEndpoint>(); graphic.State = state;
                node.transform.SetPositionAndRotation(new Vector3(10, 20, 4), Quaternion.Euler(0, 0, 30));
                node.transform.localScale = new Vector3(2, 3, 5);
                var go = new GameObject("Original UI weapon-bone follower", typeof(RectTransform));
                go.SetActive(false); go.transform.SetParent(node.transform, false);
                var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(100, 100);
                var follower = go.AddComponent<SkeletonGraphicFollower>(); follower.SkeletonGraphic = graphic; follower.BoneName = "pWeapon_R2";
                go.SetActive(true);
                var external = new GameObject("Original external world follower"); external.SetActive(false);
                var world = external.AddComponent<SkeletonRendererFollower>(); world.SkeletonRenderer = graphic; world.BoneName = follower.BoneName;
                external.SetActive(true);
                foreach (var frame in group)
                {
                    var combined = state.CreateSkin("actual source combined");
                    foreach (string name in frame.skins) combined.AddSkin(source.SkinCatalog.FindSkin(name));
                    state.SetSkin(combined); state.SetToSetupPose();
                    state.EvaluateUnmixedIdleBeforePhysics(frame.animation, frame.time);
                    foreach (var expected in frame.expected)
                    {
                        string name = (string)((Dictionary<string, object>)boneRows[expected.bone])["name"];
                        var bone = skeleton.FindBone(name); Require(bone.Active && ReferenceEquals(bone, skeleton.FindBone(name)), "stable actual bone identity/activation");
                        Near(expected.a, bone.A); Near(expected.b, bone.B); Near(expected.c, bone.C); Near(expected.d, bone.D);
                        Near(expected.x * source.DataScale, bone.WorldX); Near(expected.y * source.DataScale, bone.WorldY);
                        Require(Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(expected.c, expected.a) * Mathf.Rad2Deg, bone.WorldRotationX)) < .003f, "source world-axis rotation");
                        var localScale = state.Pose.LocalScale(expected.bone); Near(localScale.x, bone.ScaleX); Near(localScale.y, bone.ScaleY); boneViews++;
                    }
                    var order = skeleton.DrawOrder;
                    for (int i = 0; i < source.SlotCount; i++)
                    {
                        int slot = state.SlotPose.DrawSlot(i); var view = skeleton.FindSlot(state.SlotPose.Name(slot));
                        Require(ReferenceEquals(view, order[i]) && ReferenceEquals(view, state.FindSlot(state.SlotPose.Name(slot))) && view.Index == slot, "shared actual slot identity/order across application interfaces");
                        string boneName = (string)((Dictionary<string, object>)boneRows[state.SlotPose.Bone(slot)])["name"];
                        Require(ReferenceEquals(view.Bone, skeleton.FindBone(boneName)) && ReferenceEquals(view.Attachment, state.GetAttachment(slot)), "actual slot bone and live attachment"); slotViews++;
                    }
                    var expectedWeapon = frame.expected.Single(e => e.bone == source.FindBone("pWeapon_R2"));
                    follower.ForceUpdate(); world.ForceUpdate();
                    NearVector(rect.localPosition, new Vector3(expectedWeapon.x * source.DataScale * rect.rect.width, expectedWeapon.y * source.DataScale * rect.rect.height, 0));
                    NearAngle(rect.localRotation.eulerAngles.z, Mathf.Atan2(expectedWeapon.c, expectedWeapon.a) * Mathf.Rad2Deg);
                    NearVector(external.transform.position, node.transform.TransformPoint(new Vector3(expectedWeapon.x * source.DataScale, expectedWeapon.y * source.DataScale, 0)));
                    NearAngle(external.transform.eulerAngles.z, node.transform.eulerAngles.z + Mathf.Atan2(expectedWeapon.c, expectedWeapon.a) * Mathf.Rad2Deg);
                    updates += 2;
                }
                CheckSlotFollower(state, graphic, node);
                var before = follower.Bone;
                var replacement = new ProfessionNativeAppearanceSkeleton(source);
                var skin = replacement.CreateSkin("rebuilt source default"); skin.AddSkin(source.SkinCatalog.FindSkin("default")); replacement.SetSkin(skin); replacement.SetToSetupPose();
                graphic.Rebuild(replacement);
                Require(!ReferenceEquals(before, follower.Bone) && ReferenceEquals(follower.Bone, replacement.FindBone("pWeapon_R2")), "original rebuild registers real new skeleton identity");
                follower.Clear(); world.Clear(); Require(graphic.ListenerCount == 0, "original clear unregisters both actual graph listeners");
                Require(skeleton.FindBone("missing") == null && skeleton.FindSlot("missing") == null, "source missing lookup");
                Object.DestroyImmediate(external); Object.DestroyImmediate(node);
            }
            Debug.Log("Actual native follower bindings passed Direct3D Play Mode: 96 source frames, " + boneViews + " independent world bone views, " + slotViews + " actual shared slot views, " + updates + " original UI/world follower updates. Data scale, world axes/rotation, local scale, parent/external transforms, attachment visibility, zero/custom-depth boundary, noncustom draw-order spacing, live overrides and rebuild/clear identity pass. Actual part assets, custom nonzero depth derivation, physics/mixing and full profession page/input/save/Windows Player remain required.");
        }
        private static void CheckSlotFollower(ProfessionNativeAppearanceSkeleton state, GraphicEndpoint graphic, GameObject target)
        {
            IProfessionFollowerSkeleton skeleton = state;
            var slot = skeleton.DrawOrder.First(s => s.Attachment is ProfessionNativeRenderAttachment && s.Bone.Active);
            string name = state.SlotPose.Name(slot.Index), attachment = state.SlotPose.Attachment(slot.Index);
            var go = new GameObject("Original actual slot follower", typeof(RectTransform)); go.SetActive(false); go.transform.SetParent(target.transform, false);
            var child = new GameObject("Source attachment visibility"); child.transform.SetParent(go.transform, false);
            var follower = go.AddComponent<SkeletonGraphicFollower>(); follower.SkeletonGraphic = graphic; follower.SlotName = name; follower.AttachmentName = attachment; follower.Gos = new[] { child };
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(4, 5);
            go.SetActive(true); graphic.Custom = true; graphic.Spacing = 0; follower.ForceUpdate();
            Require(child.activeSelf && ReferenceEquals(follower.Bone, slot.Bone), "original visible source attachment and slot bone");
            Near(rect.localPosition.z, 0); Reject<NotSupportedException>(() => { var ignored = slot.zOrder; });
            graphic.Custom = false; graphic.Spacing = .01f; follower.Initialize(); follower.ForceUpdate();
            int order = Array.IndexOf(skeleton.DrawOrder, slot); Near(rect.localPosition.z, order + .5f);
            var originalAttachment = slot.Attachment; state.SetSlotAttachment(slot.Index, null); follower.ForceUpdate(); Require(!child.activeSelf, "original attachment disappearance hides part");
            state.SetSlotAttachment(slot.Index, originalAttachment); follower.ForceUpdate(); Require(child.activeSelf, "same actual attachment reactivates part");
            int bone = state.SlotPose.Bone(slot.Index); var matrix = state.Pose.World(bone); matrix.m03 += 7; matrix.m13 -= 3; state.Pose.OverrideWorld(bone, matrix);
            follower.ForceUpdate(); Near(rect.localPosition.x, matrix.m03 * state.Source.DataScale * 4); Near(rect.localPosition.y, matrix.m13 * state.Source.DataScale * 5);
            follower.Clear(); Object.DestroyImmediate(go); graphic.Custom = true; graphic.Spacing = 0;
        }
        public sealed class GraphicEndpoint : MonoBehaviour, IProfessionFollowerGraphic, IProfessionFollowerRenderer
        {
            internal ProfessionNativeAppearanceSkeleton State; internal bool Custom = true; internal float Spacing;
            private Action<IProfessionFollowerAnimation> rebuilt;
            public IProfessionFollowerSkeleton Skeleton => State;
            public bool UseCustomZSpacing => Custom;
            public int UpdateWhenInvisible => 3;
            public int UpdateMode => 0;
            public float ZSpacing => Spacing;
            public int ListenerCount => rebuilt?.GetInvocationList().Length ?? 0;
            public event Action<IProfessionFollowerAnimation> OnAnimationRebuild { add { rebuilt += value; } remove { rebuilt -= value; } }
            internal void Rebuild(ProfessionNativeAppearanceSkeleton state) { State = state; rebuilt?.Invoke(this); }
        }
        private static void Near(float expected, float actual) { Require(!float.IsNaN(actual) && Mathf.Abs(expected - actual) <= .001f + .000007f * Mathf.Abs(expected), "independent source scalar"); }
        private static void NearVector(Vector3 a, Vector3 b) { Near(a.x, b.x); Near(a.y, b.y); Near(a.z, b.z); }
        private static void NearAngle(float actual, float expected) { Require(Mathf.Abs(Mathf.DeltaAngle(actual, expected)) < .003f, "independent source angle"); }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Actual native followers: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
