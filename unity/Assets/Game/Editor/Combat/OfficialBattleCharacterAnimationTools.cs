using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    /// <summary>Inspects existing converted Unity assets; does not regenerate Spine animation data.</summary>
    public static class OfficialBattleCharacterAnimationTools
    {
        private const string CatalogPath = "Assets/Game/Resources/Combat/OfficialBattleCharacterAnimationCatalog.asset";
        private const string PrefabRoot = "Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Baked/Preview/";
        [Serializable] private sealed class Source { public string prefab, prefabSha256, controller, controllerSha256; public int clips, emptyStates; }
        [Serializable] private sealed class Evidence { public string path, sha256; }
        [Serializable] private sealed class Receipt
        {
            public string operation = "Bind existing converted clips to non-empty native Animator states by exact motion name. Empty source states are excluded. No runtime library or skeleton rebake is used. Historical conversion dependencies and unconverted animations remain unchanged.";
            public Source[] inputs;
            public Evidence[] existingConversionReceipts;
            public string generatorSha256;
            public string output, outputSha256;
        }

        [MenuItem("Zjcs/Combat/Recover Native Character Animation Bindings")]
        public static void Build()
        {
            var controllers = new List<RuntimeAnimatorController>();
            var bindings = new List<OfficialBattleCharacterAnimationBinding>();
            var sources = new List<Source>();
            foreach (string body in new[] { "Male", "Female" })
            {
                string path = PrefabRoot + "CharacterCreation_" + body + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var controller = prefab?.GetComponent<Animator>()?.runtimeAnimatorController as AnimatorController;
                if (controller == null) throw new InvalidOperationException("Native character controller missing: " + path);
                controllers.Add(controller);
                var states = new List<OfficialBattleCharacterAnimationBinding>();
                int empty = 0;
                foreach (var layer in controller.layers)
                    Collect(controller, layer.stateMachine, layer.name + ".", states, ref empty);
                foreach (var group in states.GroupBy(value => value.clip, StringComparer.Ordinal))
                {
                    var candidates = group.ToArray();
                    var exact = candidates.Where(value => value.state == "Base Layer." + value.clip).ToArray();
                    if (exact.Length == 1) bindings.Add(exact[0]);
                    else if (candidates.Length == 1) bindings.Add(candidates[0]);
                    else throw new InvalidOperationException("Ambiguous native animation state: " + body + "/" + group.Key);
                }
                string controllerPath = AssetDatabase.GetAssetPath(controller);
                sources.Add(new Source { prefab = path, prefabSha256 = Hash(path), controller = controllerPath,
                    controllerSha256 = Hash(controllerPath), clips = states.Select(value => value.clip).Distinct().Count(), emptyStates = empty });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<OfficialBattleCharacterAnimationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialBattleCharacterAnimationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            var controllerRows = serialized.FindProperty("controllers");
            controllerRows.arraySize = controllers.Count;
            for (int i = 0; i < controllers.Count; i++) controllerRows.GetArrayElementAtIndex(i).objectReferenceValue = controllers[i];
            var rows = serialized.FindProperty("bindings");
            rows.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("controller").objectReferenceValue = bindings[i].controller;
                row.FindPropertyRelative("clip").stringValue = bindings[i].clip;
                row.FindPropertyRelative("state").stringValue = bindings[i].state;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            string receiptPath = Path.GetFullPath("../assets/reference/battle-models/character-animation-binding-source-manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            File.WriteAllText(receiptPath, JsonUtility.ToJson(new Receipt { inputs = sources.ToArray(),
                existingConversionReceipts = new[] { "current-native-prefabs-source-manifest.json",
                    "current-native-concurrent-bindings-source-manifest.json", "native-character-model-bake-source-manifest.json" }
                    .Select(name => "assets/reference/character-creation/" + name)
                    .Select(path => new Evidence { path = path, sha256 = Hash("../" + path) }).ToArray(),
                generatorSha256 = Hash("Assets/Game/Editor/Combat/OfficialBattleCharacterAnimationTools.cs"),
                output = "unity/" + CatalogPath, outputSha256 = Hash(CatalogPath) }, true) + "\n");
            Debug.Log("Recovered native character animation bindings: " + bindings.Count + "; "
                + string.Join("; ", sources.Select(value => value.clips + " clips, " + value.emptyStates + " empty source states")));
        }

        private static void Collect(AnimatorController controller, AnimatorStateMachine machine, string prefix,
            List<OfficialBattleCharacterAnimationBinding> rows, ref int empty)
        {
            foreach (var child in machine.states)
            {
                if (child.state.motion == null) { empty++; continue; }
                if (!(child.state.motion is AnimationClip clip))
                    throw new InvalidOperationException("Native animation blend tree needs an explicit binding: " + child.state.name);
                rows.Add(new OfficialBattleCharacterAnimationBinding { controller = controller, clip = clip.name,
                    state = prefix + child.state.name });
            }
            foreach (var child in machine.stateMachines) Collect(controller, child.stateMachine,
                prefix + child.stateMachine.name + ".", rows, ref empty);
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
