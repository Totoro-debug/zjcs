// Original Spine.Extension.RenderExistingMesh runtime behavior, recovered from
// the selected APK native methods. See the machine-readable source receipt.
using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Official.HomeFurniture
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class RenderExistingMesh : MonoBehaviour
    {
        [Serializable] public struct MaterialReplacement { public Material originalMaterial, replacementMaterial; }
        public MeshRenderer referenceRenderer;
        private bool updateViaSkeletonCallback;
        private MeshFilter referenceMeshFilter;
        private MeshRenderer ownRenderer;
        private MeshFilter ownMeshFilter;
        public MaterialReplacement[] replacementMaterials = Array.Empty<MaterialReplacement>();
        private readonly Dictionary<Material, Material> replacementMaterialDict = new Dictionary<Material, Material>();
        private Material[] sharedMaterials = Array.Empty<Material>();

        private void Awake()
        {
            if (referenceRenderer == null) referenceRenderer = transform.parent.GetComponentInParent<MeshRenderer>();
            SkeletonAnimation skeleton = referenceRenderer.GetComponent<SkeletonAnimation>();
            if (skeleton)
            {
                skeleton.OnMeshAndMaterialsUpdated -= UpdateOnCallback;
                skeleton.OnMeshAndMaterialsUpdated += UpdateOnCallback;
                updateViaSkeletonCallback = true;
            }
            referenceMeshFilter = referenceRenderer.GetComponent<MeshFilter>();
            ownRenderer = GetComponent<MeshRenderer>();
            ownMeshFilter = GetComponent<MeshFilter>();
            InitializeDict();
        }

        private void LateUpdate() { if (!updateViaSkeletonCallback) UpdateMaterials(); }
        private void UpdateOnCallback(SkeletonRenderer renderer) => UpdateMaterials();
        private void UpdateMaterials()
        {
            ownMeshFilter.sharedMesh = referenceMeshFilter.sharedMesh;
            Material[] parentMaterials = referenceRenderer.sharedMaterials;
            if (sharedMaterials.Length != parentMaterials.Length) sharedMaterials = new Material[parentMaterials.Length];
            for (int i = 0; i < parentMaterials.Length; i++)
                if (replacementMaterialDict.ContainsKey(parentMaterials[i]))
                    sharedMaterials[i] = replacementMaterialDict[parentMaterials[i]];
            ownRenderer.sharedMaterials = sharedMaterials;
        }

        private void InitializeDict()
        {
            replacementMaterialDict.Clear();
            foreach (MaterialReplacement entry in replacementMaterials)
                replacementMaterialDict[entry.originalMaterial] = entry.replacementMaterial;
        }
    }
}
