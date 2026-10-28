using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.StoryBubbles;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14BubbleTools
    {
        public const string Destination = "Assets/Game/Resources/Map14/BubbleUI";
        public const string Source = "Assets/Official/World/Map/14/BubbleUI/bubble-ui.json";
        public const string FontPath = "Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset";
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            ["BubbleDialog"] = typeof(BubbleDialog), ["AttachToObjUI"] = typeof(AttachToObjUI),
            ["TMP_TypeWriterEffect"] = typeof(TMP_TypeWriterEffect), ["UIBloomGroup"] = typeof(UIBloomGroup),
            ["ButtonSound"] = typeof(ButtonSound), ["PopupWindow"] = typeof(WorldPopupWindow),
            ["MultiLanguageText"] = typeof(WorldMultiLanguageText), ["TextMeshProUGUI"] = typeof(TextMeshProUGUI),
            ["WorldMultiLanguageText"] = typeof(WorldMultiLanguageText),
            ["NoDrawingRayCast"] = typeof(Zjcs.Official.WorldUI.NoDrawingRayCast),
            ["Button"] = typeof(Button), ["Image"] = typeof(Image), ["CanvasGroup"] = typeof(CanvasGroup),
            ["Graphic"] = typeof(Graphic),
            ["GraphicRaycaster"] = typeof(GraphicRaycaster), ["HorizontalLayoutGroup"] = typeof(HorizontalLayoutGroup),
            ["VerticalLayoutGroup"] = typeof(VerticalLayoutGroup), ["ContentSizeFitter"] = typeof(ContentSizeFitter),
            ["Canvas"] = typeof(Canvas), ["Animator"] = typeof(Animator), ["CanvasRenderer"] = typeof(CanvasRenderer)
        };

        public static void Import()
        {
            AssetDatabase.Refresh();
            string raw = File.ReadAllText(Source);
            var data = JsonUtility.FromJson<OfficialMap11UiData>(raw);
            if (data.manifest != "871_156598" || data.panels.Length != 1 || data.panels[0].nodes.Length != 10 || data.sprites.Length != 2)
                throw new InvalidDataException("Original BubbleDialog source is incomplete.");
            var document = (Dictionary<string, object>)OfficialEquipmentJson.Parse(raw, true);
            var records = (List<object>)document["nativeComponents"];
            if (records.Count != 43) throw new InvalidDataException("Original bubble component count changed.");
            var sprites = new Dictionary<string, Sprite>();
            var externalObjects = new Dictionary<string, UnityEngine.Object>();
            foreach (var s in data.sprites)
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(s.unityAssetPath))).Replace("-", "").ToLowerInvariant() != s.sha256)
                        throw new InvalidDataException("Original bubble sprite differs: " + s.uri);
                var importer = (TextureImporter)AssetImporter.GetAtPath(s.unityAssetPath);
                importer.textureType = TextureImporterType.Sprite; importer.textureShape = TextureImporterShape.Texture2D;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = s.pixelsPerUnit;
                importer.spriteBorder = new Vector4(s.border[0], s.border[1], s.border[2], s.border[3]);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(s.pivot[0] / s.width, s.pivot[1] / s.height); importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(s.unityAssetPath, ImportAssetOptions.ForceSynchronousImport);
                var imported = AssetDatabase.LoadAssetAtPath<Sprite>(s.unityAssetPath);
                if (!imported) throw new InvalidDataException("Original bubble sprite failed native import: " + s.unityAssetPath + "; subassets=" + string.Join(",", AssetDatabase.LoadAllAssetsAtPath(s.unityAssetPath).Select(a=>a.GetType().Name)));
                sprites.Add(s.uri, imported);
            }
            foreach (Dictionary<string, object> s in (List<object>)document["sprites"])
                externalObjects.Add((string)((Dictionary<string, object>)s["source"])["path_id"], sprites[(string)s["uri"]]);
            var host = new GameObject("Original bubble prefab restoration", typeof(RectTransform)); host.SetActive(false);
            try
            {
                var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault(), uri => sprites[uri]);
                GameObject root = renderer.CreateSubtree(data.panels[0].BuildRoot(), host.transform);
                var windowSettings = root.AddComponent<BubbleWindowSettings>();
                root.AddComponent<BubbleFontBinding>().Source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var bindings = new Dictionary<Dictionary<string, object>, Component>();
                foreach (Dictionary<string, object> record in records)
                {
                    string kind = (string)record["type"]; if (kind == "RectTransform") continue;
                    if (!Types.TryGetValue(kind, out Type type)) throw new InvalidDataException("Unbound original bubble component: " + kind);
                    GameObject node = renderer.Find((string)record["path"]);
                    var component = node.GetComponent(type);
                    if (component == null) component = node.AddComponent(type);
                    if (!component) throw new InvalidDataException("Could not attach original bubble component: " + kind + " at " + record["path"]);
                    bindings.Add(record, component);
                }
                foreach (var pair in bindings)
                {
                    if (!pair.Value) throw new InvalidDataException("Original component disappeared while restoring fields: " + pair.Key["type"] + " at " + pair.Key["path"]);
                    var fields = (Dictionary<string, object>)pair.Key["fields"];
                    var serialized = new SerializedObject(pair.Value);
                    foreach (var field in fields)
                    {
                        if (field.Key == "m_Script" || field.Key == "m_GameObject" || field.Key == "m_Name") continue;
                        // Native engine fields are restored below through their public APIs.
                        if (pair.Value is Canvas || pair.Value is CanvasRenderer || pair.Value is Animator) continue;
                        var property = serialized.FindProperty(field.Key);
                        if (property == null && pair.Value is WorldPopupWindow)
                        {
                            var settings = new SerializedObject(windowSettings);
                            property = settings.FindProperty(field.Key);
                            if (property != null) { Apply(property, field.Value, renderer, externalObjects); settings.ApplyModifiedPropertiesWithoutUndo(); continue; }
                        }
                        if (property == null) throw new InvalidDataException("Original bubble field has no Windows binding: " + pair.Value.GetType().Name + "." + field.Key);
                        Apply(property, field.Value, renderer, externalObjects);
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (pair.Value is Canvas canvas)
                    {
                        canvas.renderMode = (RenderMode)Convert.ToInt32(fields["m_RenderMode"]);
                        canvas.planeDistance = Convert.ToSingle(fields["m_PlaneDistance"]);
                        canvas.overrideSorting = (bool)fields["m_OverrideSorting"];
                        canvas.sortingLayerID = Convert.ToInt32(fields["m_SortingLayerID"]);
                        canvas.sortingOrder = Convert.ToInt32(fields["m_SortingOrder"]);
                        canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)Convert.ToInt32(fields["m_AdditionalShaderChannelsFlag"]);
                    }
                    if (pair.Value is Animator animator)
                    {
                        if (fields["m_Controller"] != null || fields["m_Avatar"] != null) throw new InvalidDataException("Unexpected original bubble animator dependency.");
                        animator.cullingMode = (AnimatorCullingMode)Convert.ToInt32(fields["m_CullingMode"]);
                        animator.updateMode = (AnimatorUpdateMode)Convert.ToInt32(fields["m_UpdateMode"]);
                        animator.applyRootMotion = (bool)fields["m_ApplyRootMotion"];
                    }
                }
                Directory.CreateDirectory(Destination);
                if (!PrefabUtility.SaveAsPrefabAsset(root, Destination + "/BubbleDialog.prefab")) throw new InvalidDataException("Could not save original bubble prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log("Original BubbleDialog restored: 10 native nodes, 43 components and original sprite/font/material bindings.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Apply(SerializedProperty property, object value, OfficialBattleHudRenderer renderer, Dictionary<string, UnityEngine.Object> externalObjects)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                UnityEngine.Object target = null;
                if (value is string path)
                {
                    var node = renderer.Find(path) ?? throw new InvalidDataException("Missing original bubble reference: " + path);
                    string name = property.type;
                    if (name == "PPtr<$GameObject>") target = node;
                    else
                    {
                        string kind = name.Substring(name.IndexOf('$') + 1).TrimEnd('>');
                        target = kind == "RectTransform" ? node.transform : node.GetComponent(Types[kind]);
                    }
                }
                else if (value is Dictionary<string, object> external)
                {
                    var source = (Dictionary<string, object>)external["source"]; string id = (string)source["path_id"];
                    if (id == "-1054575886599457342") target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                    else if (id == "8668477128756491714") target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath).material;
                    else if (externalObjects.TryGetValue(id, out var sprite)) target = sprite;
                    else throw new InvalidDataException("Unresolved original bubble external reference: " + id);
                    if (target == null) throw new InvalidDataException("Unbound original bubble external: " + id + "; field=" + property.propertyPath + "; source type=" + external["type"] + "; font type=" + AssetDatabase.GetMainAssetTypeAtPath(FontPath));
                }
                property.objectReferenceValue = target; return;
            }
            if (property.isArray && value is List<object> array)
            {
                property.arraySize = array.Count;
                for (int i = 0; i < array.Count; i++) Apply(property.GetArrayElementAtIndex(i), array[i], renderer, externalObjects);
                return;
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = Convert.ToBoolean(value); return;
                case SerializedPropertyType.Integer: property.longValue = Convert.ToInt64(value); return;
                case SerializedPropertyType.Enum: property.intValue = Convert.ToInt32(value); return;
                case SerializedPropertyType.Float: property.doubleValue = Convert.ToDouble(value); return;
                case SerializedPropertyType.String: property.stringValue = (string)value; return;
                case SerializedPropertyType.LayerMask: property.intValue = unchecked((int)Convert.ToInt64(((Dictionary<string, object>)value)["m_Bits"])); return;
                case SerializedPropertyType.Color:
                    var color = (Dictionary<string, object>)value;
                    if (color.TryGetValue("rgba", out var packed))
                    {
                        uint rgba=Convert.ToUInt32(packed);
                        property.colorValue=(Color)new Color32((byte)rgba,(byte)(rgba>>8),(byte)(rgba>>16),(byte)(rgba>>24));
                    }
                    else property.colorValue=new Color(Convert.ToSingle(color["r"]),Convert.ToSingle(color["g"]),Convert.ToSingle(color["b"]),Convert.ToSingle(color["a"]));
                    return;
            }
            if (value is Dictionary<string, object> fields)
            {
                foreach (var field in fields)
                {
                    var child = property.FindPropertyRelative(field.Key);
                    if (child == null) throw new InvalidDataException("Unbound original bubble nested field: " + property.propertyPath + "." + field.Key);
                    Apply(child, field.Value, renderer, externalObjects);
                }
                return;
            }
            throw new InvalidDataException("Unbound original bubble serialized field: " + property.propertyPath + " (" + property.propertyType + ")");
        }
    }
}
