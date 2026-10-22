using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Editor.Inventory
{
    public static class OfficialInventoryAssetTools
    {
        private const string Payload = "Assets/Official/Inventory/";
        private const string ResourcesRoot = "Assets/Game/Resources/Inventory";
        private const string CatalogPath = ResourcesRoot + "/OfficialInventoryAssetCatalog.asset";

        [MenuItem("Zjcs/Inventory/Import and Validate Official Assets")]
        public static void ImportAndValidate()
        {
            TextAsset data = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "bag-items.json");
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "source-manifest.json");
            if (data == null || manifest == null)
                throw new BuildFailedException("Run scripts/prepare_unity_inventory_assets.py and import the official inventory payload first.");

            string spritesRoot = Payload + "Art/sprites";
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spritesRoot });
            if (guids.Length < 1500)
                throw new BuildFailedException("The restored official inventory sprite set is incomplete: " + guids.Length);

            EnsureFolders();
            OfficialInventoryAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialInventoryAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialInventoryAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("uiData").objectReferenceValue = data;
            serialized.FindProperty("sourceManifest").objectReferenceValue = manifest;
            SerializedProperty sprites = serialized.FindProperty("sprites");
            sprites.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new BuildFailedException("Inventory sprite did not import: " + path);
                SerializedProperty entry = sprites.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sourcePath").stringValue = Path.GetFileName(path);
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.ValidateBindings();
            if (catalog.LoadTemplate("bag") == null
                || catalog.LoadRowTemplate("bag_scroll_row_0") == null
                || catalog.LoadRowTemplate("bag", "/PlayerBagPanel/BgPanel1/Content/MiddleGo/ScrollRect ") == null)
                throw new BuildFailedException("Official inventory panel or scroll row metadata is missing.");
            Debug.Log("Official inventory UI, row templates and sprite bindings imported and validated.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Inventory");
        }
    }
}
