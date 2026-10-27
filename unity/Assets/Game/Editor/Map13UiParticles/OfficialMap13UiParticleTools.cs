using System;
using System.IO;
using System.Linq;
using Coffee.UIExtensions;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Map13UiParticles
{
    public static partial class OfficialMap13UiParticleTools
    {
        private const string SourcePath = "Assets/Official/World/Map/13/UIParticles/bindings.json";
        [Serializable] private sealed class Node { public int[] indices; }
        [Serializable] private sealed class Carrier
        {
            public string panel, path, original_renderer_identity;
            public int[] indices;
        }
        [Serializable] private sealed class Coffee
        {
            public string panel, source_identity, fields_json;
            public bool canvas_cull_transparent;
            public int[] indices;
            public Node[] particles;
        }
        [Serializable] private sealed class Source
        {
            public string manifest;
            public int mapClassId;
            public Carrier[] carriers;
            public Coffee[] coffee;
        }
        [Serializable] private sealed class CoffeeFields
        {
            public int m_Enabled, m_RaycastTarget, m_Maskable, m_MeshSharing, m_GroupId, m_GroupMaxId,
                m_PositionMode, m_AutoScalingMode;
            public bool m_IsTrail, m_IgnoreCanvasScaler, m_AbsoluteMode, m_AutoScaling, m_UseCustomView;
            public Vector3 m_Scale3D;
            public Vector4 m_RaycastPadding;
            public Color m_Color;
            public float m_CustomViewSize;
        }

        public static void ImportAndValidate()
        {
            OfficialMap11UiTools.ImportForMap(13);
            BindAndValidate();
        }

        public static void RunUi()
        {
            BindAndValidate();
            OfficialMap12PlayModeAcceptance.RunUiMap13Imported();
        }

        public static void BindAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            if (data.manifest != "871_156598" || data.mapClassId != 13 || data.carriers.Length != 80
                || data.coffee.Length != 32 || data.coffee.Sum(c => c.particles.Length) != 80)
                throw new InvalidDataException("Original Map13 UI particle closure differs.");
            int particles = 0, coffees = 0;
            foreach (string panel in data.carriers.Select(c => c.panel).Distinct())
            {
                string path = "Assets/Game/Resources/Map13/UI/" + panel + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                bool wasActive = root.activeSelf;
                root.SetActive(false);
                try
                {
                    foreach (Carrier item in data.carriers.Where(c => c.panel == panel))
                    {
                        var target = NodeAt(root, item.indices).gameObject;
                        string sourcePath = item.path.StartsWith("unity/", StringComparison.Ordinal)
                            ? item.path.Substring(6) : throw new InvalidDataException("Particle carrier is outside the Unity project.");
                        var carrier = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                        if (!carrier) throw new InvalidDataException("Original particle carrier failed to import: " + sourcePath);
                        var source = carrier.GetComponent<ParticleSystem>();
                        var sourceRenderer = carrier.GetComponent<ParticleSystemRenderer>();
                        if (!source || !sourceRenderer || sourceRenderer.sharedMaterials.Length != 1
                            || !sourceRenderer.sharedMaterial || !sourceRenderer.sharedMaterial.shader.isSupported)
                            throw new InvalidDataException("Original particle engine components or material are unbound: " + item.original_renderer_identity);
                        var ps = target.GetComponent<ParticleSystem>();
                        if (!ps) ps = target.AddComponent<ParticleSystem>();
                        var renderer = target.GetComponent<ParticleSystemRenderer>();
                        EditorUtility.CopySerialized(source, ps);
                        EditorUtility.CopySerialized(sourceRenderer, renderer);
                        if (ps.gameObject != target || renderer.gameObject != target)
                            throw new InvalidDataException("Native particle component ownership changed.");
                        if (renderer.lightmapIndex != -1 || renderer.realtimeLightmapIndex != -1
                            || renderer.lightmapScaleOffset != new Vector4(1, 1, 0, 0)
                            || renderer.realtimeLightmapScaleOffset != new Vector4(1, 1, 0, 0))
                            throw new InvalidDataException("Original particle lightmap sentinel or scale/offset differs.");
                        particles++;
                    }
                    foreach (Coffee item in data.coffee.Where(c => c.panel == panel))
                    {
                        var target = NodeAt(root, item.indices).gameObject;
                        var ui = target.GetComponent<UIParticle>();
                        if (!ui) ui = target.AddComponent<UIParticle>();
                        var fields = JsonUtility.FromJson<CoffeeFields>(item.fields_json);
                        var serialized = new SerializedObject(ui);
                        void Bool(string name, bool value) => serialized.FindProperty(name).boolValue = value;
                        void Int(string name, int value) => serialized.FindProperty(name).intValue = value;
                        Bool("m_IsTrail", fields.m_IsTrail);
                        Bool("m_IgnoreCanvasScaler", fields.m_IgnoreCanvasScaler);
                        Bool("m_AbsoluteMode", fields.m_AbsoluteMode);
                        Bool("m_AutoScaling", fields.m_AutoScaling);
                        Bool("m_UseCustomView", fields.m_UseCustomView);
                        Bool("m_RaycastTarget", fields.m_RaycastTarget != 0);
                        Bool("m_Maskable", fields.m_Maskable != 0);
                        Int("m_MeshSharing", fields.m_MeshSharing);
                        Int("m_GroupId", fields.m_GroupId);
                        Int("m_GroupMaxId", fields.m_GroupMaxId);
                        Int("m_PositionMode", fields.m_PositionMode);
                        Int("m_AutoScalingMode", fields.m_AutoScalingMode);
                        serialized.FindProperty("m_Material").objectReferenceValue = null;
                        serialized.FindProperty("m_Color").colorValue = fields.m_Color;
                        serialized.FindProperty("m_RaycastPadding").vector4Value = fields.m_RaycastPadding;
                        serialized.FindProperty("m_Scale3D").vector3Value = fields.m_Scale3D;
                        serialized.FindProperty("m_CustomViewSize").floatValue = fields.m_CustomViewSize;
                        serialized.FindProperty("m_AnimatableProperties").arraySize = 0;
                        var list = serialized.FindProperty("m_Particles");
                        list.arraySize = item.particles.Length;
                        for (int i = 0; i < list.arraySize; i++)
                        {
                            var ps = NodeAt(root, item.particles[i].indices).GetComponent<ParticleSystem>();
                            if (!ps) throw new InvalidDataException("Original Coffee particle reference is unbound: " + item.source_identity);
                            list.GetArrayElementAtIndex(i).objectReferenceValue = ps;
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        ui.enabled = fields.m_Enabled != 0;
                        ui.GetComponent<CanvasRenderer>().cullTransparentMesh = item.canvas_cull_transparent;
                        coffees++;
                    }
                    root.SetActive(wasActive);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            if (particles != 80 || coffees != 32) throw new InvalidDataException("Original Map13 UI particle binding is incomplete.");
            foreach (string panel in data.carriers.Select(c => c.panel).Distinct())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/Map13/UI/" + panel + ".prefab");
                foreach (Carrier item in data.carriers.Where(c => c.panel == panel))
                {
                    var renderer = NodeAt(prefab, item.indices).GetComponent<ParticleSystemRenderer>();
                    if (!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.mainTexture)
                        throw new InvalidDataException("Saved original particle dependency is unbound.");
                }
                foreach (Coffee item in data.coffee.Where(c => c.panel == panel))
                {
                    var ui = NodeAt(prefab, item.indices).GetComponent<UIParticle>();
                    if (!ui || ui.particles.Count != item.particles.Length || ui.particles.Any(p => !p))
                        throw new InvalidDataException("Saved original Coffee particle order differs.");
                }
            }
            Debug.Log("Map13 original UI particle binding passed: 80 native systems/renderers, 32 Coffee components and original ordered particle references.");
        }

        private static Transform NodeAt(GameObject root, int[] indices)
        {
            var target = root.transform;
            foreach (int index in indices) target = target.GetChild(index);
            return target;
        }
    }
}
