using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class BattleNativeMonsterRenderer
    {
        private readonly List<BattleNativeMonsterMeshCopy> meshCopies = new List<BattleNativeMonsterMeshCopy>();

        private void BindOriginalMeshCopies(Dictionary<string, object> row, Dictionary<string, Transform> transforms)
        {
            if (!row.TryGetValue("meshCopies", out object value)) return;
            foreach (var source in Arr(value).Select(Obj))
            {
                if (!transforms.TryGetValue((string)source["transform"], out Transform node))
                    throw new InvalidDataException("Original monster mesh-copy transform is unbound.");
                bool active = node.gameObject.activeSelf;
                node.gameObject.SetActive(false);
                try
                {
                    node.gameObject.AddComponent<MeshFilter>();
                    var target = node.gameObject.AddComponent<MeshRenderer>();
                    ApplyRenderer(target, Obj(source["renderer"]));
                    var copy = node.gameObject.AddComponent<BattleNativeMonsterMeshCopy>();
                    copy.ReferenceRenderer = GetComponent<MeshRenderer>();
                    copy.ReplacementMaterialDict = new MonoDictionary<Material, Material>();
                    copy.enabled = Flag(Obj(source["fields"]), "m_Enabled");
                    foreach (var replacement in Arr(source["replacements"]).Select(Obj))
                        copy.ReplacementMaterialDict.Add(sourceMaterials[Int(replacement, "original")],
                            sourceMaterials[Int(replacement, "replacement")]);
                    meshCopies.Add(copy);
                }
                finally { node.gameObject.SetActive(active); }
            }
            RefreshOriginalMeshCopies();
        }

        private void RefreshOriginalMeshCopies()
        {
            // Original CustomRenderExistingMesh.UpdateMaterials shares the exact
            // reference mesh and replaces materials in the same submesh order.
            foreach (var copy in meshCopies)
            {
                if (copy.isActiveAndEnabled) copy.Refresh();
            }
        }
    }
}
