using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterNativeGeometryValidation
    {
        [MenuItem("Zjcs/Validate/Native Character Attachment Geometry")]
        public static void Run()
        {
            CharacterCreationAssetCatalog assets = CharacterCreationAssetCatalog.LoadDefault();
            int[] expected = { 654, 706 };
            for (int sex = 0; sex < 2; sex++)
            {
                GameObject model = UnityEngine.Object.Instantiate(sex == 0
                    ? assets.MaleNativeModelPrefab : assets.FemaleNativeModelPrefab);
                try
                {
                    Renderer[] originals = model.GetComponentsInChildren<Renderer>(true)
                        .Where(value => value.name.StartsWith("__skin__", StringComparison.Ordinal)).ToArray();
                    Require(originals.Length == expected[sex], "Official native attachment coverage changed.");
                    int missing = originals.Count(value => value is SkinnedMeshRenderer skinned
                        ? skinned.sharedMesh == null : value.GetComponent<MeshFilter>().sharedMesh == null);
                    foreach (Renderer source in originals)
                        for (Transform node = source.transform; node != null && node != model.transform; node = node.parent)
                            node.gameObject.SetActive(true);
                    var geometry = model.AddComponent<CharacterNativeAttachmentGeometry>();
                    geometry.Initialize(sex == 0 ? "male" : "female");
                    MeshFilter[] restored = model.GetComponentsInChildren<MeshFilter>()
                        .Where(value => value.name == "Official native attachment geometry").ToArray();
                    Require(restored.Length == expected[sex], "An official attachment did not create native geometry.");
                    foreach (MeshFilter filter in restored)
                    {
                        Mesh mesh = filter.sharedMesh;
                        Require(mesh.bounds.size.sqrMagnitude > 1e-10f,
                            "An official native attachment still has zero geometry: " + mesh.name);
                        Require(mesh.vertices.All(value => IsFinite(value.x) && IsFinite(value.y)),
                            "An official native attachment has non-finite vertices: " + mesh.name);
                    }
                    MeshFilter hair = restored.First(value => value.transform.parent.name ==
                        (sex == 0 ? "__skin__hair_M_01" : "__skin__hair_F_01")
                        && value.sharedMesh.vertexCount > 4);
                    Vector3[] before = hair.sharedMesh.vertices;
                    Transform[] bones = model.GetComponentsInChildren<Transform>(true);
                    var rotations = bones.Select(value => value.localRotation).ToArray();
                    // Alter each influencing chain differently; rigid parent motion alone
                    // cannot prove that weighted vertices are evaluated from the bones.
                    for (int i = 0; i < bones.Length; i++)
                        if (!bones[i].name.StartsWith("__skin__", StringComparison.Ordinal))
                            bones[i].localRotation *= Quaternion.Euler(0, 0, (i % 7 - 3) * .25f);
                    geometry.SendMessage("LateUpdate");
                    Vector3[] after = hair.sharedMesh.vertices;
                    Require(before.Where((value, i) => (value - after[i]).sqrMagnitude > 1e-10f).Any(),
                        "Native weighted geometry did not respond to bone motion.");
                    for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rotations[i];
                    geometry.SendMessage("LateUpdate");
                    Require(before.Zip(hair.sharedMesh.vertices, (a, b) => (a - b).sqrMagnitude).All(value => value < 1e-10f),
                        "Native geometry did not return to the original bone pose.");
                    Debug.Log("Official native geometry passed: sex=" + sex + " attachments="
                        + restored.Length + " recoveredMissingMeshReferences=" + missing + ".");
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
