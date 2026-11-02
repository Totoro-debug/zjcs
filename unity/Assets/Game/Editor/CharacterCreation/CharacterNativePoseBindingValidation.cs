using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterNativePoseBindingValidation
    {
        private sealed class Snapshot
        {
            internal MeshFilter Filter;
            internal Transform Parent;
            internal int Order;
            internal bool Enabled;
            internal Vector3[] World;
        }

        [MenuItem("Zjcs/Validate/Native Character Pose Restoration")]
        public static void Run()
        {
            var assets = CharacterCreationAssetCatalog.LoadDefault();
            foreach (string sex in new[] { "male", "female" })
            {
                var model = UnityEngine.Object.Instantiate(sex == "male"
                    ? assets.MaleNativeModelPrefab : assets.FemaleNativeModelPrefab);
                try
                {
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)
                        .Where(value => value.name.StartsWith("__skin__", StringComparison.Ordinal)))
                        for (Transform node = renderer.transform; node != model.transform; node = node.parent)
                            node.gameObject.SetActive(true);
                    model.transform.SetPositionAndRotation(new Vector3(2, -1, 0), Quaternion.Euler(0, 0, 13));
                    model.transform.localScale = new Vector3(-.8f, 1.2f, 1);
                    var geometry = model.AddComponent<CharacterNativeAttachmentGeometry>();
                    geometry.Initialize(sex);
                    var before = model.GetComponentsInChildren<MeshFilter>(true)
                        .Where(value => value.name == "Official native attachment geometry")
                        .Select(value => new Snapshot { Filter = value, Parent = value.transform.parent,
                            Order = value.GetComponent<Renderer>().sortingOrder,
                            Enabled = value.GetComponent<Renderer>().enabled, World = WorldVertices(value) }).ToArray();
                    Require(before.Length == (sex == "male" ? 654 : 706), "original attachment inventory");
                    CheckLeases(model, geometry, before);
                    foreach (string animation in new[] { "map/win_01", "map/injuried_idle" })
                    {
                        var pose = model.AddComponent<OfficialSkillResultNativePose>();
                        pose.Initialize(sex, animation, Array.Empty<string>());
                        pose.Apply(.4f); geometry.RefreshForCapture();
                        Require(before.Any(row => row.Filter.GetComponent<Renderer>().enabled
                            && !Same(row.World, WorldVertices(row.Filter))), "original result pose reaches native vertices");
                        UnityEngine.Object.DestroyImmediate(pose);
                        geometry.RefreshForCapture();
                        AssertRestored(before);
                        Debug.Log("Native pose restoration passed: " + sex + "/" + animation + ".");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }

        private static void CheckLeases(GameObject model, CharacterNativeAttachmentGeometry geometry, Snapshot[] before)
        {
            var names = geometry.OriginalBoneNames;
            var wanted = new HashSet<string>(names, StringComparer.Ordinal);
            var named = model.GetComponentsInChildren<Transform>(true)
                .Where(value => wanted.Contains(value.name))
                .ToDictionary(value => value.name, value => value.localToWorldMatrix);
            var bones = names.Select(name => named[name]).ToArray();
            var first = geometry.BindOriginalPose(bone => Matrix4x4.Translate(Vector3.right) * bones[bone],
                (skin, slot, attachment) => true, slot => 117);
            var firstWorld = before.Select(row => WorldVertices(row.Filter)).ToArray();
            Require(before.All(row => row.Filter.transform.parent == model.transform), "pose owns attachment parents");
            Require(before.Any(row => !Same(row.World, WorldVertices(row.Filter))), "pose overrides native bone matrices");
            var second = geometry.BindOriginalPose(bone => Matrix4x4.Translate(Vector3.up * 2) * bones[bone],
                (skin, slot, attachment) => true, slot => 118);
            first.Dispose(); // A retired lower binding must not replace the live upper one.
            Require(before.All(row => row.Filter.GetComponent<Renderer>().sortingOrder == 118), "out-of-order release keeps current pose");
            second.Dispose(); second.Dispose();
            AssertRestored(before);
            using (var outer = geometry.BindOriginalPose(bone => Matrix4x4.Translate(Vector3.right) * bones[bone],
                (skin, slot, attachment) => true, slot => 117))
            {
                using (var inner = geometry.BindOriginalPose(bone => bones[bone],
                    (skin, slot, attachment) => false, slot => 119))
                    Require(before.All(row => !row.Filter.GetComponent<Renderer>().enabled), "pose controls attachment visibility");
                for (int i = 0; i < before.Length; i++)
                    Require(Same(firstWorld[i], WorldVertices(before[i].Filter)), "inner release restores the previous live pose");
                int calls = 0;
                bool failed = false;
                try
                {
                    geometry.BindOriginalPose(bone => bones[bone], (skin, slot, attachment) => {
                        if (++calls == 4) throw new InvalidOperationException("isolated partial-bind failure");
                        return true;
                    }, slot => 120);
                }
                catch (InvalidOperationException error) when (error.Message == "isolated partial-bind failure") { failed = true; }
                Require(failed, "partial bind failure is reported");
                for (int i = 0; i < before.Length; i++)
                    Require(Same(firstWorld[i], WorldVertices(before[i].Filter)), "failed bind restores the previous live pose");
            }
            AssertRestored(before);
        }

        private static Vector3[] WorldVertices(MeshFilter filter) => filter.sharedMesh.vertices
            .Select(filter.transform.TransformPoint).ToArray();
        private static bool Same(Vector3[] expected, Vector3[] actual) => expected.Length == actual.Length
            && expected.Zip(actual, (a, b) => (a - b).sqrMagnitude).All(value => value < 1e-8f);
        private static void AssertRestored(Snapshot[] before)
        {
            foreach (var row in before)
            {
                var renderer = row.Filter.GetComponent<Renderer>();
                Require(row.Filter.transform.parent == row.Parent, "original attachment parent restores: " + row.Filter.sharedMesh.name);
                Require(renderer.enabled == row.Enabled && renderer.sortingOrder == row.Order,
                    "original attachment visibility and order restore: " + row.Filter.sharedMesh.name);
                Require(Same(row.World, WorldVertices(row.Filter)), "original bone geometry restores: " + row.Filter.sharedMesh.name);
            }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Native pose restoration: " + message); }
    }
}
