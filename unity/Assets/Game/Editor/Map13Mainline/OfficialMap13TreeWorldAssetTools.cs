using System.IO;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Map13Mainline
{
    public static class OfficialMap13TreeWorldAssetTools
    {
        public static void ValidateProject()
        {
            Inspect();
            // The root editor assembly is loaded by Unity; keep this optional
            // validation entry from adding a circular editor assembly reference.
            var projectTools = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Zjcs.Editor.ProjectTools"))
                .Single(type => type != null);
            projectTools.GetMethod("Validate").Invoke(null, null);
        }
        public static void Inspect()
        {
            const string root = "Assets/Game/Resources/Map13/Native/tree-world-assets.asset";
            AssetDatabase.ImportAsset(root, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(root);
            if (!asset) throw new InvalidDataException("Original tree world asset did not import.");
            var serialized = new SerializedObject(asset);
            foreach (string name in new[] { "animationBinary", "animationCatalog", "dissolveBinary", "dissolveCatalog" })
            {
                var property = serialized.FindProperty(name);
                var value = property.objectReferenceValue;
                Debug.Log("Tree world reference " + name + " = " + (value ? value.name : "null") + " path=" + AssetDatabase.GetAssetPath(value));
                if (value) continue;
                const string path = "Assets/Game/Resources/Map13/Native/entity-sprite-catalog.asset";
                var direct = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                Debug.Log("Direct sprite catalog=" + direct + " type=" + direct?.GetType());
                throw new InvalidDataException("Original tree world reference is unresolved: " + name);
            }
            string resource = serialized.FindProperty("spriteCatalogResource").stringValue;
            if (resource != "Map13/Native/entity-sprite-catalog" || !Resources.Load(resource))
                throw new InvalidDataException("Original tree sprite catalog resource is not explicitly bound.");
            var displays = serialized.FindProperty("displays");
            for (int i = 0; i < displays.arraySize; i++)
            {
                var prefab = displays.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                if (!prefab || prefab.transform.parent || !prefab.GetComponent<Zjcs.Offline.World.NativeMap13TreeEntities.NativeMap13TreeEntityBinding>())
                    throw new InvalidDataException("Original complete tree reference must select its root entity, not a model child.");
            }
            Debug.Log("Original tree world serialized resource references passed.");
        }
    }
}
