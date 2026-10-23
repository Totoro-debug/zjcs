using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap10TerrainTools
    {
        private const string PayloadRoot = "Assets/Official/World/Map/10";
        private const string GeometryAsset = PayloadRoot + "/map10.geom.bytes";
        private const string PrefabAsset = PayloadRoot + "/Map10Terrain.prefab";
        private static readonly int[] MonsterIds = { 3204, 3206, 3207, 3254, 3255, 3281, 3282, 3283, 3401, 3411, 3603 };

        [MenuItem("Zjcs/World/Build and Validate Map10 Terrain")]
        public static void BuildAndValidate()
        {
            RequireEditorVersion();
            var geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryAsset);
            if (geometry == null)
                throw new InvalidOperationException("Official Map10 geometry TextAsset is not imported: " + GeometryAsset);

            var temporary = new GameObject("Map10 terrain prefab source");
            try
            {
                OfficialMap10TerrainView view = temporary.AddComponent<OfficialMap10TerrainView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("geometry").objectReferenceValue = geometry;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PrefabAsset);
                if (prefab == null) throw new InvalidOperationException("Unity could not create the Map10 terrain prefab.");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabAsset, ImportAssetOptions.ForceUpdate);

                view.Initialize(geometry);
                if (view.BatchCount != 1404)
                    throw new InvalidOperationException("Map10 terrain parser created " + view.BatchCount + " batches instead of 1404.");
                MeshRenderer[] renderers = temporary.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 1404 || renderers.Any(renderer => renderer.sharedMaterial == null))
                    throw new InvalidOperationException("Map10 terrain mesh/material bindings are incomplete.");
                if (renderers.Any(renderer => renderer.sharedMaterial.mainTexture == null))
                    throw new InvalidOperationException("A Map10 official terrain texture did not resolve in Resources.");

                GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAsset);
                OfficialMap10TerrainView savedView = saved != null ? saved.GetComponent<OfficialMap10TerrainView>() : null;
                var savedSerialized = savedView != null ? new SerializedObject(savedView) : null;
                if (savedSerialized == null || savedSerialized.FindProperty("geometry").objectReferenceValue != geometry)
                    throw new InvalidOperationException("Map10 terrain prefab lost its geometry binding.");

                ValidateMonsterModels();
                ValidateEntityModels();
                ValidateEnvironment();
                Debug.Log("Map10 terrain, entity displays, water, butterflies, official Spine payloads, and texture bindings imported.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        [MenuItem("Zjcs/World/Validate Map10 Monster Models")]
        public static void ValidateMonsterModels()
        {
            int totalFrames = 0;
            foreach (int monsterId in MonsterIds)
            {
                string resourcePath = PayloadRoot + "/Resources/Map10/Monsters/" + monsterId + ".json";
                TextAsset payload = AssetDatabase.LoadAssetAtPath<TextAsset>(resourcePath);
                int frameCount = OfficialMap10MonsterView.ValidatePayload(payload, monsterId, out string textureResource);
                string texturePath = PayloadRoot + "/Resources/" + textureResource + ".png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
                    throw new InvalidOperationException("Official Map10 monster atlas did not import: " + texturePath);
                totalFrames += frameCount;
            }
            Debug.Log("Map10 official monster animations imported: " + MonsterIds.Length + " models, " + totalFrames + " idle frames.");
        }

        [MenuItem("Zjcs/World/Validate Map10 Entity Models")]
        public static void ValidateEntityModels()
        {
            string catalogPath = PayloadRoot + "/Resources/Map10/Entities/catalog.json";
            TextAsset catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
            int placementCount = OfficialMap10EntityView.ValidateCatalog(catalog, out int modelCount,
                out int geometryBatches, out int spineCount, out int textureCount);
            if (placementCount != 82 || modelCount != 26 || geometryBatches <= 0 || spineCount != 8 || textureCount != 41)
                throw new InvalidOperationException("Map10 entity payload counts differ from the fixed source: "
                    + placementCount + " placements, " + modelCount + " models, " + geometryBatches + " geometry batches, "
                    + spineCount + " Spine views, " + textureCount + " textures.");

            TextAsset mapDefinition = AssetDatabase.LoadAssetAtPath<TextAsset>(PayloadRoot + "/map10.json");
            OfficialMapDefinition definition = mapDefinition != null
                ? JsonUtility.FromJson<OfficialMapDefinition>(mapDefinition.text)
                : null;
            if (definition == null || definition.Navigation == null)
                throw new InvalidOperationException("Map10 entity placement requires official map navigation origin.");

            var temporary = new GameObject("Map10 entity display validation");
            try
            {
                OfficialMap10EntityView view = temporary.AddComponent<OfficialMap10EntityView>();
                view.Initialize(catalog, temporary.transform,
                    new Vector2(definition.Navigation.Origin.X, definition.Navigation.Origin.Y));
                if (view.EntityCount != 82)
                    throw new InvalidOperationException("Map10 entity runtime created " + view.EntityCount + " of 82 placements.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
            Debug.Log("Map10 entity displays imported: " + placementCount + " placements, " + modelCount + " models, " + geometryBatches
                + " geometry batches, " + spineCount + " Spine views, and " + textureCount + " textures.");
        }

        [MenuItem("Zjcs/World/Validate Map10 Environment")]
        public static void ValidateEnvironment()
        {
            string environmentRoot = PayloadRoot + "/Resources/Map10/Environment/";
            TextAsset water = AssetDatabase.LoadAssetAtPath<TextAsset>(environmentRoot + "map10-water-runtime.json");
            TextAsset butterflies = AssetDatabase.LoadAssetAtPath<TextAsset>(environmentRoot + "map10-butterflies-runtime.json");
            OfficialMap10EnvironmentView.ValidatePayloads(water, butterflies,
                out int waterCount, out int butterflyCount, out int animationCount);
            if (waterCount != 4 || butterflyCount != 15 || animationCount != 2)
                throw new InvalidOperationException("Map10 environment payload counts differ from the fixed source: "
                    + waterCount + " water meshes, " + butterflyCount + " butterflies, " + animationCount + " animations.");

            Shader waterShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Resources/World/Map10Water.shader");
            Shader butterflyShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Resources/World/Map10Butterfly.shader");
            if (waterShader == null || !waterShader.isSupported || butterflyShader == null || !butterflyShader.isSupported)
                throw new InvalidOperationException("Map10 Windows environment shader did not compile or import.");

            var temporary = new GameObject("Map10 environment validation");
            try
            {
                OfficialMap10EnvironmentView view = temporary.AddComponent<OfficialMap10EnvironmentView>();
                view.Initialize(water, butterflies);
                MeshRenderer[] renderers = temporary.GetComponentsInChildren<MeshRenderer>(true);
                MeshFilter[] meshes = temporary.GetComponentsInChildren<MeshFilter>(true);
                OfficialMap10MonsterView[] spines = temporary.GetComponentsInChildren<OfficialMap10MonsterView>(true);
                if (view.WaterMeshCount != 4 || view.ButterflyCount != 15 || renderers.Length != 19
                    || meshes.Length != 19 || spines.Length != 15)
                    throw new InvalidOperationException("Map10 environment runtime created an incomplete renderer set.");
                foreach (MeshFilter filter in meshes)
                    if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                        throw new InvalidOperationException("Map10 environment runtime generated an empty mesh: " + filter.name);
                foreach (OfficialMap10MonsterView spine in spines)
                    if (string.IsNullOrEmpty(spine.Animation))
                        throw new InvalidOperationException("Map10 butterfly animation did not initialize.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
            Debug.Log("Map10 environment imported: " + waterCount + " water meshes, " + butterflyCount
                + " animated butterflies, and " + animationCount + " official animation loops.");
        }

        private static void RequireEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, "2022.3.57f1c2", StringComparison.Ordinal))
                throw new InvalidOperationException("Map10 terrain must be imported with Unity 2022.3.57f1c2; found " + Application.unityVersion);
        }
    }
}
