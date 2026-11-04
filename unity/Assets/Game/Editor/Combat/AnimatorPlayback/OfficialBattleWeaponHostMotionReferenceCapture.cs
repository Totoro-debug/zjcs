using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    // Optional isolated observations of public calls on an existing binary.
    // The game and native regression tests have no dependency on that binary.
    public static class OfficialBattleWeaponHostMotionReferenceCapture
    {
        [Serializable] public sealed class Frame
        {
            public Vector3 position, scale;
            public float rotation, delta;
            public float[] pose;
        }
        [Serializable] public sealed class Case
        {
            public string name;
            public Vector2 factor;
            public float rotationFactor;
            public Frame[] frames;
        }
        [Serializable] public sealed class Observations
        {
            public string skeleton, reference_binary_sha256, reference_core_binary_sha256;
            public Case[] cases;
        }

        public static void Run()
        {
            SessionState.SetBool("Zjcs.WeaponHostMotionReference", true);
            Register();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Register()
        {
            EditorApplication.update -= Tick;
            if (SessionState.GetBool("Zjcs.WeaponHostMotionReference", false)) EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool("Zjcs.WeaponHostMotionReference", false)) return;
            SessionState.SetBool("Zjcs.WeaponHostMotionReference", false);
            EditorApplication.update -= Tick;
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Capture()
        {
            var work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use an owned validation workspace.");
            var assembly = Assembly.Load("spine-unity");
            var assetType = assembly.GetType("Spine.Unity.SkeletonDataAsset", true);
            var componentType = assembly.GetType("Spine.Unity.SkeletonAnimation", true);
            const string json = "{\"skeleton\":{\"spine\":\"4.2.0\",\"hash\":\"host-observation\",\"referenceScale\":100},"
                + "\"bones\":[{\"name\":\"root\",\"x\":20,\"y\":30,\"length\":100}],\"slots\":[],\"skins\":[],\"animations\":{},"
                + "\"physics\":[{\"name\":\"p\",\"bone\":\"root\",\"x\":1,\"y\":1,\"rotate\":1,\"fps\":60,\"inertia\":0.5,"
                + "\"strength\":100,\"damping\":0.85,\"mass\":1,\"mix\":1}]}";
            var rows = new List<Case>();
            foreach (string motion in new[] { "translation", "rotation", "combined", "zero-delta", "disabled-inheritance" })
            foreach (var scale in new[] { Vector3.one, new Vector3(2, .5f, 1), new Vector3(-.7f, .7f, 1) })
            {
                var go = new GameObject("Public host observation"); go.SetActive(false);
                var asset = ScriptableObject.CreateInstance(assetType);
                var text = new TextAsset(json);
                try
                {
                    assetType.GetField("skeletonJSON").SetValue(asset, text);
                    var atlases = assetType.GetField("atlasAssets");
                    atlases.SetValue(asset, Array.CreateInstance(atlases.FieldType.GetElementType(), 0));
                    var component = go.AddComponent(componentType);
                    ((Behaviour)component).enabled = false;
                    componentType.GetField("skeletonDataAsset").SetValue(component, asset);
                    componentType.GetMethod("Initialize").Invoke(component, new object[] { true, true });
                    var mode = componentType.GetProperty("UpdateMode");
                    mode.SetValue(component, Enum.ToObject(mode.PropertyType, 3));
                    var skeleton = componentType.GetProperty("Skeleton").GetValue(component);
                    if (skeleton == null) throw new InvalidOperationException("Public reference skeleton did not initialize.");
                    var bone = skeleton.GetType().GetProperty("RootBone").GetValue(skeleton);
                    var update = componentType.GetMethod("Update", new[] { typeof(float) });
                    var row = new Case { name = motion + "/" + scale, factor = motion == "disabled-inheritance" ? Vector2.zero : Vector2.one,
                        rotationFactor = motion == "disabled-inheritance" ? 0 : 1, frames = new Frame[32] };
                    componentType.GetProperty("PhysicsPositionInheritanceFactor").SetValue(component, row.factor);
                    componentType.GetProperty("PhysicsRotationInheritanceFactor").SetValue(component, row.rotationFactor);
                    go.transform.localScale = scale;
                    go.transform.position = new Vector3(4, -2, 0); go.transform.rotation = Quaternion.Euler(0, 0, 17);
                    for (int i = 0; i < row.frames.Length; i++)
                    {
                        var frame = new Frame { delta = motion == "zero-delta" && i % 3 == 0 ? 0 : 1f / 60 };
                        frame.position = new Vector3(4 + (motion == "rotation" ? 0 : Mathf.Sin(i * .21f) * 3),
                            -2 + (motion == "rotation" ? 0 : Mathf.Cos(i * .17f) * 2), 0);
                        frame.rotation = 17 + (motion == "translation" ? 0 : i * 13);
                        frame.scale = scale;
                        go.transform.SetPositionAndRotation(frame.position, Quaternion.Euler(0, 0, frame.rotation));
                        update.Invoke(component, new object[] { frame.delta });
                        frame.pose = new[] { "A", "B", "C", "D", "WorldX", "WorldY" }
                            .Select(name => Convert.ToSingle(bone.GetType().GetProperty(name).GetValue(bone))).ToArray();
                        row.frames[i] = frame;
                    }
                    rows.Add(row);
                }
                finally { Object.DestroyImmediate(go); Object.DestroyImmediate(asset); Object.DestroyImmediate(text); }
            }
            File.WriteAllText(Path.Combine(work, "weapon-host-motion-observations.json"),
                JsonUtility.ToJson(new Observations { skeleton = json, cases = rows.ToArray(),
                    reference_binary_sha256 = BinaryHash(assembly), reference_core_binary_sha256 = BinaryHash(Assembly.Load("spine-csharp")) }, true) + "\n");
            Debug.Log("Public weapon host motion observations: " + rows.Count + " cases, " + rows.Sum(r => r.frames.Length) + " frames.");
        }

        private static string BinaryHash(Assembly assembly)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(File.ReadAllBytes(assembly.Location)).Select(b => b.ToString("x2")));
        }
    }
}
