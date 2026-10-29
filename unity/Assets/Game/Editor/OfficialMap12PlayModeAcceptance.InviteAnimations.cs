using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void BuildInviteAnimationVerificationBundle()
        {
            try
            {
                string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
                SteleRequire(!string.IsNullOrEmpty(workspace) && !string.IsNullOrEmpty(repository) &&
                    Path.GetFullPath(workspace).StartsWith(Path.GetFullPath(Path.Combine(repository, "output/validation")) +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Use an owned animation verification workspace.");
                const string folder = "Assets/Official/World/Map/12/UI/Team/InviteAnimations";
                string[] assets = AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !AssetDatabase.IsValidFolder(path)).OrderBy(path => path).ToArray();
                SteleRequire(assets.Length == 18, "Original invitation animation bundle inventory differs.");
                string output = Path.Combine(workspace, "invite-animation-windows");
                Directory.CreateDirectory(output);
                var manifest = BuildPipeline.BuildAssetBundles(output,
                    new[] { new AssetBundleBuild { assetBundleName = "invite-animation-windows.bundle", assetNames = assets } },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode,
                    BuildTarget.StandaloneWindows64);
                SteleRequire(manifest != null, "Windows invitation animation bundle build failed.");
                Debug.Log("Windows invitation animation verification bundle built from all 18 native assets.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        // Exercise Unity's real binding resolver after the independent source
        // curve verification. These objects are animation test targets only.
        private static void VerifyInviteAnimationRuntimeSamples()
        {
            const string folder = "Assets/Official/World/Map/12/UI/Team/InviteAnimations";
            string[] paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            SteleRequire(paths.Length == 14, "Original invitation clip inventory differs.");
            int samples = 0;
            using var native = new Zjcs.Offline.World.NativeMap12InviteAnimations();
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var nativeClip = native.Clip(clip.name);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var root = new GameObject("OriginalInviteAnimationBindings", typeof(RectTransform));
                root.SetActive(false);
                var targets = new Dictionary<string, GameObject> { [""] = root };
                try
                {
                    foreach (var binding in bindings)
                    {
                        string parent = "";
                        foreach (string part in binding.path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string full = parent.Length == 0 ? part : parent + "/" + part;
                            if (!targets.ContainsKey(full))
                            {
                                var child = new GameObject(part, typeof(RectTransform));
                                child.transform.SetParent(targets[parent].transform, false);
                                targets.Add(full, child);
                            }
                            parent = full;
                        }
                        var target = targets[binding.path];
                        if (binding.type != typeof(GameObject) && !target.GetComponent(binding.type))
                            target.AddComponent(binding.type);
                    }
                    // Compiled Mecanim channels resolve through the Animator binding cache.
                    root.AddComponent<Animator>();
                    root.SetActive(true);
                    foreach (float time in new[] { 0f, clip.length * .25f, clip.length * .75f, clip.length * .999f })
                    {
                        nativeClip.SampleAnimation(root, time);
                        foreach (var binding in bindings)
                        {
                            var target = targets[binding.path];
                            UnityEngine.Object component = binding.type == typeof(GameObject) ?
                                (UnityEngine.Object)target : target.GetComponent(binding.type);
                            float expected = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
                            if (binding.type == typeof(Transform) && binding.propertyName.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal))
                            {
                                Vector3 angles = Vector3.zero;
                                foreach (var axisBinding in bindings.Where(b => b.path == binding.path &&
                                    b.type == typeof(Transform) && b.propertyName.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal)))
                                {
                                    float value = AnimationUtility.GetEditorCurve(clip, axisBinding).Evaluate(time);
                                    char axis = axisBinding.propertyName[axisBinding.propertyName.Length - 1];
                                    if (axis == 'x') angles.x = value;
                                    else if (axis == 'y') angles.y = value;
                                    else angles.z = value;
                                }
                                SteleRequire(Quaternion.Angle(target.transform.localRotation, Quaternion.Euler(angles)) < .005f,
                                    "Native invitation Euler animation orientation differs: " + path + "/" + binding.path +
                                    " at " + time + ": " + target.transform.localEulerAngles + " expected " + angles);
                                samples++;
                                continue;
                            }
                            var serialized = new SerializedObject(component);
                            var property = serialized.FindProperty(binding.propertyName);
                            SteleRequire(property != null, "Native invitation animated property is unbound: " + path + "/" + binding.propertyName);
                            float actual = property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1 : 0) :
                                property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
                            SteleRequire(Mathf.Abs(actual - expected) <= .00004f * Mathf.Max(1, Mathf.Abs(expected)),
                                "Native invitation animation sample differs: " + path + "/" + binding.path + "/" + binding.propertyName +
                                " at " + time + ": " + actual + " expected " + expected);
                            samples++;
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("Original invitation animation runtime binding samples passed: " + paths.Length + " clips, " + samples + " values.");
        }
    }
}
