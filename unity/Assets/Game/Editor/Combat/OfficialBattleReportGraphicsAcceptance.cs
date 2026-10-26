using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportGraphicsAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding[] bindings; public SpriteRecord[] sprites; }
        [Serializable] private sealed class Binding { public string prefab, path, kind; public Fields fields; }
        [Serializable] private sealed class Fields
        {
            public Reference m_Sprite, m_Material, m_fontAsset, m_sharedMaterial;
            public Color m_Color, m_fontColor;
            public string m_text;
            public int m_Type, m_FillMethod, m_FillOrigin;
            public int m_RaycastTarget, m_PreserveAspect, m_FillCenter, m_FillClockwise;
            public float m_FillAmount, m_fontSize, m_fontSizeMin, m_fontSizeMax;
        }
        [Serializable] private sealed class Reference { public long fileID; public string guid; }
        [Serializable] private sealed class SpriteRecord
        {
            public string key; public Vector2 pivot; public Vector4 border;
            public RectData original_rect; public int vertex_count;
        }
        [Serializable] private sealed class RectData { public float width, height; }

        public static IEnumerator Run()
        {
            Require(Application.isPlaying, "Report graphics validation requires Play Mode.");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-graphics-source-manifest.json"));
            int graphics = 0;
            foreach (var panel in receipt.bindings.GroupBy(row => row.prefab))
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(panel.Key);
                var asset = Resources.Load<GameObject>("Combat/BattleReport/" + key);
                Require(asset != null, "Original report prefab missing: " + key);
                var root = UnityEngine.Object.Instantiate(asset);
                root.name = asset.name;
                try
                {
                    // Compare the original serialized seed before MultiLanguageText.Start refreshes it.
                    var nodes = new Dictionary<string, Transform>();
                    Index(root.transform, "", nodes);
                    foreach (Binding binding in panel)
                    {
                        Require(nodes.TryGetValue(binding.path, out Transform node), "Original report hierarchy lost: " + binding.path);
                        var graphic = node.GetComponent<Graphic>();
                        Require(graphic != null && graphic.raycastTarget == (binding.fields.m_RaycastTarget != 0), "Graphic/input flags lost: " + binding.path);
                        var serialized = new SerializedObject(graphic);
                        Require(Close(serialized.FindProperty("m_Color").colorValue, binding.fields.m_Color), "Graphic color differs: " + binding.path);
                        if (graphic is Image image)
                        {
                            Match(image.sprite, binding.fields.m_Sprite, binding.path + " sprite");
                            Match(serialized.FindProperty("m_Material").objectReferenceValue, binding.fields.m_Material, binding.path + " material");
                            Require((int)image.type == binding.fields.m_Type && image.preserveAspect == (binding.fields.m_PreserveAspect != 0)
                                && image.fillCenter == (binding.fields.m_FillCenter != 0) && (int)image.fillMethod == binding.fields.m_FillMethod
                                && image.fillOrigin == binding.fields.m_FillOrigin && image.fillClockwise == (binding.fields.m_FillClockwise != 0)
                                && Mathf.Abs(image.fillAmount - binding.fields.m_FillAmount) < 0.0001f, "Original Image fields differ: " + binding.path);
                            if (binding.kind == "MirrorImage") Require(image is Zjcs.Official.WorldUI.MirrorImage, "Original mirror implementation was not bound.");
                        }
                        else if (graphic is TMP_Text text)
                        {
                            Match(text.font, binding.fields.m_fontAsset, binding.path + " font");
                            Match(text.fontSharedMaterial, binding.fields.m_sharedMaterial, binding.path + " font material");
                            Require(text.text == binding.fields.m_text && Close(text.color, binding.fields.m_fontColor)
                                && Mathf.Abs(text.fontSizeMin - binding.fields.m_fontSizeMin) < 0.0001f
                                && Mathf.Abs(text.fontSizeMax - binding.fields.m_fontSizeMax) < 0.0001f, "Original TMP fields differ: " + binding.path);
                            Require(text.font.sourceFontFile != null && text.fontSharedMaterial.shader.name == "Zjcs/Combat/WorldTextMeshPro",
                                "Original dynamic font/source shader is not available locally.");
                        }
                        else throw new InvalidDataException("Unexpected original graphic: " + binding.path);
                        graphics++;
                    }
                    yield return null;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            foreach (SpriteRecord entry in receipt.sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Combat/BattleReport/Graphics/" + entry.key + ".asset");
                Require(sprite != null && sprite.vertices.Length == entry.vertex_count
                    && Mathf.Abs(sprite.rect.width - entry.original_rect.width) < 0.0001f
                    && Mathf.Abs(sprite.rect.height - entry.original_rect.height) < 0.0001f
                    && Vector2.Distance(sprite.pivot, Vector2.Scale(entry.pivot, sprite.rect.size)) < 0.0001f
                    && Vector4.Distance(sprite.border, entry.border) < 0.0001f, "Native Sprite geometry/padding differs: " + entry.key);
            }
            Require(graphics == 279, "Expected 279 original report graphics; got " + graphics);
            Debug.Log("Original report graphics passed in Play Mode: 279 Image/TMP bindings, 36 native Sprite geometries, original colors/fill/padding, two local dynamic fonts and original shader. Data/custom components remain unfinished.");
        }

        private static void Match(UnityEngine.Object value, Reference expected, string name)
        {
            if (expected == null || expected.fileID == 0) { Require(value == null, "Unexpected native reference: " + name); return; }
            string actualGuid = ""; long actualId = 0;
            Require(value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out actualGuid, out actualId)
                && actualGuid == expected.guid && actualId == expected.fileID, "Original native reference differs: " + name
                + "; value=" + value + "; actual=" + actualGuid + ":" + actualId + "; expected=" + expected.guid + ":" + expected.fileID);
        }

        private static void Index(Transform node, string parent, Dictionary<string, Transform> result)
        {
            string name = node.name;
            if (node.parent != null && parent.Length > 0)
            {
                int repeated = 0;
                for (int i = 0; i < node.GetSiblingIndex(); i++) if (node.parent.GetChild(i).name == name) repeated++;
                if (repeated > 0) name += " (" + repeated + ")";
            }
            string path = parent + "/" + name;
            result.Add(path, node);
            foreach (Transform child in node) Index(child, path, result);
        }

        private static bool Close(Color first, Color second) => Mathf.Abs(first.r - second.r) < 0.0001f
            && Mathf.Abs(first.g - second.g) < 0.0001f && Mathf.Abs(first.b - second.b) < 0.0001f && Mathf.Abs(first.a - second.a) < 0.0001f;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
