using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor
{
    public static class OfficialEmbeddedMonsterSkeletonValidation
    {
        public static void Run()
        {
            try { ProjectTools.Validate(); Validate(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Validate()
        {
            var binding = (Dictionary<string, object>)OfficialEquipmentJson.Parse(
                Resources.Load<TextAsset>("Combat/EmbeddedMonsterSkeletons/bindings").text);
            var rows = ((List<object>)binding["graphs"]).Cast<Dictionary<string, object>>().ToArray();
            int instances = 0, frames = 0;
            var parent = new GameObject("Inactive original graph validation"); parent.SetActive(false);
            try
            {
                foreach (var group in rows.GroupBy(row => Convert.ToInt32(row["skill_id"])))
                {
                    var first = group.First();
                    string resource = "Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/" + group.Key + "/" + first["prefab"];
                    var prefab = Resources.Load<GameObject>(resource);
                    Require(prefab, "Original skill prefab does not load: " + resource);
                    var instance = UnityEngine.Object.Instantiate(prefab, parent.transform, false);
                    try
                    {
                        Require(EmbeddedMonsterNativeSkeleton.BindOriginalPrefab(instance) == group.Count(), "Independent renderer count differs.");
                        foreach (var row in group)
                        {
                            var native = instance.transform.Find((string)row["path"]).GetComponent<EmbeddedMonsterNativeSkeleton>();
                            Require(native && native.State != null, "Original pose is unbound.");
                            var mesh = native.VisibleRenderer.GetComponent<ProfessionNativeSceneMeshPart>();
                            Require(mesh && mesh.transform == instance.transform.Find((string)row["visible_path"]), "Original renderer child differs.");
                            var renderer = (Dictionary<string, object>)row["renderer"];
                            Require(native.AnimationName == (string)renderer["animationName"], "Original animation differs.");
                            int visibleFrames = 0;
                            bool materialOverrideChecked = false;
                            for (int frame = 0; frame < 120; frame++)
                            {
                                native.transform.localPosition += new Vector3(.001f, -.0005f, 0);
                                native.transform.localRotation *= Quaternion.Euler(0, 0, .1f);
                                native.Advance(1f / 60);
                                var vertices = mesh.Mesh.vertices;
                                Require(vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Nonfinite original geometry.");
                                if (vertices.Length > 0) visibleFrames++;
                                if (vertices.Length > 0 && !materialOverrideChecked)
                                {
                                    var originalMaterial = native.SkinMaterials(native.State.Skin).Single();
                                    var replacement = new Material(originalMaterial);
                                    try
                                    {
                                        native.CustomMaterialOverride.Add(originalMaterial, replacement);
                                        float elapsed = native.Elapsed;
                                        native.UpdateAnimationAndMesh();
                                        Require(native.VisibleRenderer.sharedMaterials.All(value => value == replacement), "Original material override was ignored.");
                                        Require(elapsed == native.Elapsed && vertices.SequenceEqual(mesh.Mesh.vertices), "Changing a material advanced the completed pose.");
                                        native.CustomMaterialOverride.Clear(); native.UpdateAnimationAndMesh();
                                        Require(native.VisibleRenderer.sharedMaterials.All(value => value == originalMaterial), "Original material did not restore.");
                                        materialOverrideChecked = true;
                                    }
                                    finally { native.CustomMaterialOverride.Clear(); UnityEngine.Object.DestroyImmediate(replacement); }
                                }
                                if (frame % 20 == 0)
                                {
                                    float elapsed = native.Elapsed;
                                    native.UpdateAnimationAndMesh();
                                    Require(elapsed == native.Elapsed && vertices.SequenceEqual(mesh.Mesh.vertices), "Redrawing changed the completed pose.");
                                    native.gameObject.SetActive(false); native.gameObject.SetActive(true);
                                    Require(elapsed == native.Elapsed, "Pooling reset clearStateOnDisable=false.");
                                }
                                frames++;
                            }
                            Require(visibleFrames > 0 && materialOverrideChecked, "Original animation never generated geometry or material override was not exercised.");
                            Require(native.VisibleRenderer.sharedMaterials.Length > 0, "Original material is unbound.");
                            foreach (var material in native.VisibleRenderer.sharedMaterials)
                            {
                                Require(material && material.shader && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "Original shader failed.");
                                Require(material.mainTexture, "Original atlas is missing.");
                                string high = (string)row["high_texture"];
                                if (high.Length > 0) Require(material.mainTexture == Resources.Load<Texture2D>(high), "Explicit original high atlas was not applied.");
                            }
                            foreach (var follower in instance.GetComponentsInChildren<SkeletonRendererFollower>(true))
                                Require(follower.SkeletonRenderer is EmbeddedMonsterNativeSkeleton, "Original bone follower still references the obsolete renderer.");
                            Debug.Log("Embedded original skeleton passed: " + group.Key + " / " + row["path"] + " animation=" + native.AnimationName + " visibleFrames=" + visibleFrames);
                            instances++;
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
            Require(instances == 8 && frames == 960, "Original embedded coverage differs.");
            Debug.Log("Embedded monster skeleton validation passed: 4 original prefabs, 8 independent skeletons, 960 moving frames; original materials, explicit high atlas, renderer child and follower references.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Embedded original monster: " + message); }
    }
}
