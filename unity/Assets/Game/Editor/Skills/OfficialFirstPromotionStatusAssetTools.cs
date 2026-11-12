using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Editor.Skills
{
    public static class OfficialFirstPromotionStatusAssetTools
    {
        [Serializable] private sealed class Catalog { public Entry[] graphs; }
        [Serializable] private sealed class Entry { public int id; public string resource; }
        [Serializable] private sealed class Receipt { public Output[] outputs; }
        [Serializable] private sealed class Output { public string path; public string sha256; }

        public static void BuildWindowsValidation()
        {
            Validate();
            ProjectTools.Validate();
            OfficialSkillAssetTools.BuildWindowsValidation();
        }

        [MenuItem("Zjcs/Validate/Original First Promotion Status Assets")]
        public static void Validate()
        {
            var text = Resources.Load<TextAsset>("Combat/first-promotion-statuses");
            if (!text) throw new BuildFailedException("Original first-promotion status catalog is absent.");
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog?.graphs == null || catalog.graphs.Length != 83)
                throw new BuildFailedException("Original first-promotion status closure must contain 83 graphs.");
            var ids = new HashSet<int>();
            foreach (var entry in catalog.graphs)
            {
                var prefab = Resources.Load<GameObject>(entry.resource);
                if (!ids.Add(entry.id) || !prefab || !prefab.GetComponent<MyEntity>() || !prefab.GetComponent<MyFightStatus>())
                    throw new BuildFailedException("Original status entity binding is invalid: " + entry.id);
                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!component) throw new BuildFailedException("Original status script binding is absent: " + entry.id);
            }
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrWhiteSpace(repository) || !Path.IsPathRooted(repository))
                throw new BuildFailedException("Original status source validation requires ZJCS_REPOSITORY_ROOT.");
            int outputs = 0;
            foreach (string name in new[] { "first-promotion-status-assets", "first-promotion-status-runtime" })
            {
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(repository,
                    "assets/reference/battle-animation/" + name + "-source-manifest.json")));
                foreach (var output in receipt.outputs)
                {
                    if (!output.path.StartsWith("unity/Assets/", StringComparison.Ordinal))
                        throw new BuildFailedException("Original status output path is invalid.");
                    string path = Path.Combine(Application.dataPath, output.path.Substring("unity/Assets/".Length));
                    // Validation workspaces add a prefix to original asset paths.
                    // Preserve those names when Windows' legacy path limit is exceeded.
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                        path = @"\\?\" + Path.GetFullPath(path).Replace('/', '\\');
                    using (var hash = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                        if (actual != output.sha256) throw new BuildFailedException("Original status output differs: " + output.path);
                    }
                    outputs++;
                }
            }
            Debug.Log("Original first-promotion status assets validated: 83 hierarchies / " + outputs + " exact source outputs.");
        }
    }
}
