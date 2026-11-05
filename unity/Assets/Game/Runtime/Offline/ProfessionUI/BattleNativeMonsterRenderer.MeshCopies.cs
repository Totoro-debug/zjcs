using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class BattleNativeMonsterRenderer
    {
        private sealed class OriginalMeshCopy
        {
            internal MeshFilter Filter;
            internal MeshRenderer Renderer;
            internal bool Enabled;
            internal readonly Dictionary<Material, Material> Replacements = new Dictionary<Material, Material>();
        }
        private readonly List<OriginalMeshCopy> meshCopies = new List<OriginalMeshCopy>();

        private void BindOriginalMeshCopies(Dictionary<string, object> row, Dictionary<string, Transform> transforms)
        {
            if (!row.TryGetValue("meshCopies", out object value)) return;
            foreach (var source in Arr(value).Select(Obj))
            {
                if (!transforms.TryGetValue((string)source["transform"], out Transform node))
                    throw new InvalidDataException("Original monster mesh-copy transform is unbound.");
                var copy = new OriginalMeshCopy
                {
                    Filter = node.gameObject.AddComponent<MeshFilter>(),
                    Renderer = node.gameObject.AddComponent<MeshRenderer>(),
                    Enabled = Flag(Obj(source["fields"]), "m_Enabled")
                };
                ApplyRenderer(copy.Renderer, Obj(source["renderer"]));
                foreach (var replacement in Arr(source["replacements"]).Select(Obj))
                    copy.Replacements.Add(sourceMaterials[Int(replacement, "original")],
                        sourceMaterials[Int(replacement, "replacement")]);
                meshCopies.Add(copy);
            }
            RefreshOriginalMeshCopies();
        }

        private void RefreshOriginalMeshCopies()
        {
            // Original CustomRenderExistingMesh.UpdateMaterials shares the exact
            // reference mesh and replaces materials in the same submesh order.
            foreach (var copy in meshCopies)
            {
                if (!copy.Enabled || !copy.Renderer.gameObject.activeInHierarchy) continue;
                copy.Filter.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                copy.Renderer.sharedMaterials = batches.Batches.Select(batch =>
                {
                    var original = MaterialForBatch(batch);
                    return copy.Replacements.TryGetValue(original, out var replacement) ? replacement
                        : throw new InvalidDataException("Original mesh-copy page replacement is unbound.");
                }).ToArray();
            }
        }
    }
}
