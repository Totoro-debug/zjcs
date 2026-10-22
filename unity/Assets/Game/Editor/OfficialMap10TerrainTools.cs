using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
                Debug.Log("Map10 terrain imported: 1404 official mesh batches and all 166 texture bindings.");
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

        private static void RequireEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, "2022.3.57f1c2", StringComparison.Ordinal))
                throw new InvalidOperationException("Map10 terrain must be imported with Unity 2022.3.57f1c2; found " + Application.unityVersion);
        }
    }
}
