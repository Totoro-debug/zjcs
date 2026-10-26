using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap10UiTools
    {
        private const string UiRoot = "Assets/Official/World/Map/10/UI";
        private const string PanelDataPath = UiRoot + "/map10-ui.json";
        private const string SourceManifestPath = UiRoot + "/source-manifest.json";
        private const string CatalogPath = "Assets/Game/Resources/World/Map10UiCatalog.asset";

        [MenuItem("Zjcs/World/Map10/Bind Official UI Assets")]
        public static void BindAssets()
        {
            AssetDatabase.Refresh();
            TextAsset panelData = AssetDatabase.LoadAssetAtPath<TextAsset>(PanelDataPath);
            TextAsset sourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceManifestPath);
            if (panelData == null || sourceManifest == null)
                throw new BuildFailedException("Run scripts/prepare_unity_map10_ui.py before binding Map10 UI assets.");

            OfficialMap10UiPayload payload = JsonUtility.FromJson<OfficialMap10UiPayload>(panelData.text);
            if (payload == null || payload.manifest != "854_156383" || payload.mapClassId != 10
                || payload.sprites == null || payload.sprites.Length == 0)
                throw new BuildFailedException("Official Map10 UI payload is malformed.");

            var bindings = new OfficialMap10UiSpriteBinding[payload.sprites.Length];
            for (int i = 0; i < payload.sprites.Length; i++)
            {
                OfficialMap10UiSpriteRecord record = payload.sprites[i];
                string assetPath = record.unityAssetPath;
                string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                if (!File.Exists(absolutePath) || !string.Equals(HashFile(absolutePath), record.sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Map10 UI sprite is missing or differs from its source hash: " + assetPath);

                ConfigureSprite(assetPath, record);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                    throw new BuildFailedException("Official Map10 UI PNG did not import as a Sprite: " + assetPath);
                Vector2 expectedPivot = record.pivot != null && record.pivot.Length >= 2
                    ? new Vector2(record.pivot[0], record.pivot[1]) : new Vector2(record.width * 0.5f, record.height * 0.5f);
                Vector4 expectedBorder = record.border != null && record.border.Length >= 4
                    ? new Vector4(record.border[0], record.border[1], record.border[2], record.border[3]) : Vector4.zero;
                if (Mathf.Abs(sprite.rect.width - record.width) > 0.01f
                    || Mathf.Abs(sprite.rect.height - record.height) > 0.01f
                    || Vector2.Distance(sprite.pivot, expectedPivot) > 0.1f
                    || Vector4.Distance(sprite.border, expectedBorder) > 0.1f)
                    throw new BuildFailedException("Imported Map10 Sprite geometry differs from its official metadata: " + assetPath);
                bindings[i] = new OfficialMap10UiSpriteBinding
                {
                    uri = record.uri,
                    file = record.file,
                    sprite = sprite
                };
            }

            OfficialMap10UiCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialMap10UiCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialMap10UiCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("panelData").objectReferenceValue = panelData;
            serialized.FindProperty("sourceManifest").objectReferenceValue = sourceManifest;
            SerializedProperty rows = serialized.FindProperty("sprites");
            rows.arraySize = bindings.Length;
            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("uri").stringValue = bindings[i].uri;
                row.FindPropertyRelative("file").stringValue = bindings[i].file;
                row.FindPropertyRelative("sprite").objectReferenceValue = bindings[i].sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Official Map10 UI assets bound: " + payload.panels.Length + " panels, " + bindings.Length + " pinned sprites.");
        }

        [MenuItem("Zjcs/World/Map10/Validate Official UI Renderer")]
        public static void ValidateRenderer()
        {
            OfficialMap10UiCatalog catalog = OfficialMap10UiCatalog.LoadDefault();
            int expectedImages = 0, expectedTexts = 0, expectedButtons = 0, missingSprites = 0;
            foreach (OfficialMap10UiPanel panel in catalog.Panels)
                Count(panel.BuildRoot(), catalog, ref expectedImages, ref expectedTexts, ref expectedButtons, ref missingSprites);
            if (missingSprites != 0)
                throw new BuildFailedException("Map10 UI references " + missingSprites + " unbound official sprites.");

            var host = new GameObject("Map10UiValidationHost", typeof(RectTransform));
            try
            {
                var renderer = new OfficialMap10UiRenderer(catalog);
                GameObject canvas = renderer.Mount(host.transform);
                int images = canvas.GetComponentsInChildren<Image>(true).Length;
                int texts = canvas.GetComponentsInChildren<TMP_Text>(true).Length;
                int buttons = canvas.GetComponentsInChildren<Button>(true).Length;
                if (images != expectedImages || texts != expectedTexts || buttons != expectedButtons)
                    throw new BuildFailedException("Map10 UI component counts differ: Image " + images + "/" + expectedImages
                        + ", TMP " + texts + "/" + expectedTexts + ", Button " + buttons + "/" + expectedButtons);
                renderer.Dispose();
                Debug.Log("Official Map10 UI renderer validated: " + catalog.Panels.Count + " panels, "
                    + images + " images, " + texts + " text labels, " + buttons + " buttons.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void ConfigureSprite(string assetPath, OfficialMap10UiSpriteRecord record)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new BuildFailedException("Map10 UI sprite has no TextureImporter: " + assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Vector4 border = Vector4.zero;
            if (record.border != null && record.border.Length >= 4)
                border = new Vector4(record.border[0], record.border[1], record.border[2], record.border[3]);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (record.pivot != null && record.pivot.Length >= 2 && record.width > 0 && record.height > 0)
                pivot = new Vector2(record.pivot[0] / record.width, record.pivot[1] / record.height);

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spritePixelsPerUnit != record.pixelsPerUnit
                || importer.mipmapEnabled || importer.filterMode != FilterMode.Bilinear
                || importer.spriteBorder != border || importer.spritePivot != pivot
                || settings.spriteAlignment != (int)SpriteAlignment.Custom;
            if (!changed) return;
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = record.pixelsPerUnit;
            settings.spriteBorder = border;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = record.pixelsPerUnit;
            importer.spriteBorder = border;
            importer.spritePivot = pivot;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void Count(BattleHudNode node, OfficialMap10UiCatalog catalog,
            ref int images, ref int texts, ref int buttons, ref int missingSprites)
        {
            if (node == null) return;
            if (node.has_image || node.button) images++;
            if (node.has_text) texts++;
            if (node.button) buttons++;
            if (node.has_image_sprite && catalog.FindSprite(node.image_sprite_file) == null) missingSprites++;
            if (node.children != null)
                foreach (BattleHudNode child in node.children)
                    Count(child, catalog, ref images, ref texts, ref buttons, ref missingSprites);
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
