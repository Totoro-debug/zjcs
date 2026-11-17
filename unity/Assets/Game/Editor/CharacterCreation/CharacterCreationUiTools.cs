using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Editor.CharacterCreation
{
    // Restore the serialized UI fields lost by the stripped AssetRipper export.
    // All layout values and artwork come from the pinned official extraction.
    public static class CharacterCreationUiTools
    {
        internal const string Root = "Assets/Official/CharacterCreation/RuntimeUI";
        [Serializable] private sealed class Data { public string[] page_titles; public Node[] nodes; public int creation_root, diy_root, confirm_root; public SpriteInfo[] sprites; public string[] quality_keys; public TrialItem[] trial_items; }
        [Serializable] private sealed class TrialItem { public int id, quality; }
        public static string[] PageTitles { get; private set; }
        [Serializable] private sealed class SpriteInfo { public string key, path; public float[] border; public float ppu; }
        [Serializable] private sealed class Node
        {
            public string name, path; public bool active = true; public string[] scripts;
            public NativeAnimation[] animations;
            public float[] anchor_min, anchor_max, anchored_position, size_delta, pivot, scale, safe_area;
            public bool has_image, has_text, has_layout_element, has_layout, has_grid, has_aspect_fitter, has_size_fitter, has_scroll, has_safe_area;
            public float rotation; public float alpha = 1; public bool has_canvas_group, button, toggle, mask, rectmask2d, show_mask_graphic;
            [NonSerialized] public Node[] children; public int[] child_ids; public ImageInfo image; public TextInfo text;
            public ElementInfo layout_element; public LayoutInfo layout; public GridInfo grid;
            public AspectInfo aspect_fitter; public int[] size_fitter; public ScrollInfo scroll;
        }
        [Serializable] private sealed class ImageInfo { public SpriteRef sprite; public float[] color; public int type; public bool preserve_aspect; }
        [Serializable] private sealed class SpriteRef { public string file; }
        [Serializable] private sealed class TextInfo { public string value; public float[] color; public float font_size; public int horizontal, vertical; }
        [Serializable] private sealed class ElementInfo { public bool ignore; public float[] min, preferred, flexible; }
        [Serializable] private sealed class LayoutInfo { public int axis, alignment; public int[] padding; public float spacing; public bool[] expand, control, child_scale; public bool reverse; }
        [Serializable] private sealed class GridInfo { public int[] padding; public int alignment, corner, axis, constraint, count; public float[] cell, spacing; }
        [Serializable] private sealed class AspectInfo { public int mode; public float ratio; }
        [Serializable] private sealed class ScrollInfo { public bool horizontal, vertical; public float sensitivity; }
        [Serializable] private sealed class NativeAnimation { public string name, owner; public bool loop; public NativeTrack[] tracks; }
        [Serializable] private sealed class NativeTrack { public string path, property; public NativeKey[] keys; }
        [Serializable] private sealed class NativeKey { public float time, value, in_tangent, out_tangent; }
        [Serializable] private sealed class FileRecord { public string path, sha256; }
        [Serializable] private sealed class Receipt { public string operation; public FileRecord[] inputs, outputs; }

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static TMP_FontAsset font;

        public static void RestoreTrialIcons()
        {
            const string source = "Assets/Official/CharacterCreation/TrialUI/trial-ui.json";
            Data data = JsonUtility.FromJson<Data>(File.ReadAllText(source));
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Resources/Combat/FZY4K-Dynamic.asset");
            sprites.Clear();
            foreach (SpriteInfo entry in data.sprites)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.path);
                importer.textureType = TextureImporterType.Sprite;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(V(entry.border,0),V(entry.border,1),V(entry.border,2),V(entry.border,3));
                importer.spritePixelsPerUnit = entry.ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.path);
                if (sprite == null) throw new InvalidOperationException("Trial UI sprite did not import: " + entry.path);
                sprites.Add(entry.key, sprite);
            }
            foreach (Node n in data.nodes) n.children = Array.ConvertAll(n.child_ids, id => data.nodes[id]);
            string destination = "Assets/Game/Resources/CharacterCreation/Trial";
            Directory.CreateDirectory(destination);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (Node node in data.nodes)
            foreach (NativeAnimation sourceAnimation in node.animations ?? Array.Empty<NativeAnimation>())
            {
                var clip = new AnimationClip { name = sourceAnimation.name, legacy = true,
                    wrapMode = sourceAnimation.loop ? WrapMode.Loop : WrapMode.Once };
                foreach (NativeTrack track in sourceAnimation.tracks)
                {
                    var curve = new AnimationCurve(Array.ConvertAll(track.keys,
                        key => new Keyframe(key.time,key.value,key.in_tangent,key.out_tangent)));
                    clip.SetCurve(track.path.Substring(sourceAnimation.owner.Length+1),
                        track.property.StartsWith("m_Color") ? typeof(Image) : typeof(RectTransform),track.property,curve);
                }
                string clipPath = destination + "/" + clip.name + ".anim";
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (existing == null) AssetDatabase.CreateAsset(clip,clipPath);
                else { EditorUtility.CopySerialized(clip,existing); UnityEngine.Object.DestroyImmediate(clip); clip=existing; }
                clips.Add(sourceAnimation.name,clip);
            }
            foreach (TrialItem item in data.trial_items)
            {
                GameObject go = Build(data.nodes[data.creation_root], null);
                try
                {
                    foreach (Node node in data.nodes)
                    foreach (NativeAnimation sourceAnimation in node.animations ?? Array.Empty<NativeAnimation>())
                    {
                        Transform owner = go.transform.Find(sourceAnimation.owner.Substring(data.nodes[data.creation_root].path.Length+1));
                        Animation animation = owner.GetComponent<Animation>();
                        if (animation == null) animation = owner.gameObject.AddComponent<Animation>();
                        animation.AddClip(clips[sourceAnimation.name],sourceAnimation.name);
                        animation.clip = clips[sourceAnimation.name];
                        animation.playAutomatically = true;
                    }
                    go.transform.Find("Content/QualityImg").GetComponent<Image>().sprite = sprites[data.quality_keys[item.quality]];
                    PrefabUtility.SaveAsPrefabAsset(go, destination + "/Item" + item.id + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Zjcs/Character Creation/Restore Official UI Components")]
        public static void Restore()
        {
            Data data = JsonUtility.FromJson<Data>(File.ReadAllText(Root + "/runtime-ui.json"));
            if (data == null || data.nodes == null || data.sprites == null || data.page_titles == null
                || data.creation_root < 0 || data.creation_root >= data.nodes.Length
                || data.diy_root < 0 || data.diy_root >= data.nodes.Length
                || data.confirm_root < 0 || data.confirm_root >= data.nodes.Length)
                throw new InvalidOperationException("Official creation UI data is missing or invalid.");
            PageTitles = data.page_titles;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Resources/Combat/FZY4K-Dynamic.asset");
            if (font == null || font.sourceFontFile == null) throw new InvalidOperationException("Official FZY4K font is missing.");
            sprites.Clear();
            foreach (SpriteInfo entry in data.sprites)
            {
                var importer = AssetImporter.GetAtPath(entry.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Official creation sprite did not import: " + entry.path);
                Vector4 border = new Vector4(V(entry.border, 0), V(entry.border, 1), V(entry.border, 2), V(entry.border, 3));
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spriteBorder != border || importer.spritePixelsPerUnit != entry.ppu || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = border;
                    importer.spritePixelsPerUnit = entry.ppu;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                sprites.Add(entry.key, AssetDatabase.LoadAssetAtPath<Sprite>(entry.path));
            }
            foreach (Node node in data.nodes)
            {
                node.children = new Node[node.child_ids.Length];
                for (int i = 0; i < node.children.Length; i++) node.children[i] = data.nodes[node.child_ids[i]];
            }
            Save(data.nodes[data.creation_root], "CreatePlayerPanel");
            Save(data.nodes[data.diy_root], "CreatePlayerPanelDIYContent");
            Save(data.nodes[data.confirm_root], "ConfirmDialog");
            AssetDatabase.SaveAssets();
            WriteManifest(data);
            Debug.Log("Restored official character creation native UI components and FZY4K font: " + sprites.Count + " sprites.");
        }

        private static void Save(Node source, string name)
        {
            GameObject instance = Build(source, null);
            try { PrefabUtility.SaveAsPrefabAsset(instance, Root + "/" + name + ".prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void WriteManifest(Data data)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string repository = Directory.GetParent(project).FullName;
            var inputs = new List<FileRecord> { Record(project, Root + "/runtime-ui.json"),
                Record(project, "Assets/Official/Commerce/Art/fonts/FZY4K.ttf"),
                Record(project, "Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs") };
            var outputs = new List<FileRecord>();
            foreach (string name in new[] { "CreatePlayerPanel", "CreatePlayerPanelDIYContent", "ConfirmDialog" })
            {
                string path = Root + "/" + name + ".prefab";
                outputs.Add(Record(project, path)); outputs.Add(Record(project, path + ".meta"));
            }
            foreach (SpriteInfo sprite in data.sprites)
            {
                inputs.Add(Record(project, sprite.path));
                outputs.Add(Record(project, sprite.path + ".meta"));
            }
            var receipt = new Receipt {
                operation = "Rebuild original serialized UGUI/TMP fields, layout and sprite imports; bind the original FZY4K TTF through its dynamic TMP asset.",
                inputs = inputs.ToArray(), outputs = outputs.ToArray() };
            string json = JsonUtility.ToJson(receipt, true) + "\n";
            File.WriteAllText(Path.Combine(project, Root, "native-ui-source-manifest.json"), json);
            File.WriteAllText(Path.Combine(repository, "assets/reference/character-creation/native-ui-source-manifest.json"), json);
        }

        private static FileRecord Record(string project, string path)
        {
            using (var stream = File.OpenRead(Path.Combine(project, path)))
            using (var sha = SHA256.Create())
                return new FileRecord { path = "unity/" + path,
                    sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() };
        }

        private static GameObject Build(Node n, Transform parent)
        {
            var go = new GameObject(n.name, typeof(RectTransform));
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vec(n.anchor_min); rect.anchorMax = Vec(n.anchor_max);
            rect.pivot = Vec(n.pivot); rect.sizeDelta = Vec(n.size_delta); rect.anchoredPosition = Vec(n.anchored_position);
            rect.localScale = new Vector3(V(n.scale, 0, 1), V(n.scale, 1, 1), 1);
            rect.localRotation = Quaternion.Euler(0, 0, n.rotation);
            if (n.has_safe_area)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(0, V(n.safe_area, 1)); rect.offsetMax = new Vector2(0, -V(n.safe_area, 0));
            }
            if (n.has_canvas_group) go.AddComponent<CanvasGroup>().alpha = n.alpha;
            if (n.has_image)
            {
                Image image = go.AddComponent<Image>();
                if (n.image.sprite != null && !string.IsNullOrEmpty(n.image.sprite.file)) image.sprite = sprites[n.image.sprite.file];
                image.color = ColorOf(n.image.color); image.type = (Image.Type)n.image.type;
                image.preserveAspect = n.image.preserve_aspect;
                image.raycastTarget = false;
            }
            if (Has(n, "RawImage")) go.AddComponent<RawImage>().raycastTarget = false;
            if (n.has_text)
            {
                TMP_Text text = go.AddComponent<TextMeshProUGUI>();
                text.font = font; text.fontSize = n.text.font_size; text.color = ColorOf(n.text.color);
                text.text = n.text.value; text.raycastTarget = false;
                text.horizontalAlignment = (HorizontalAlignmentOptions)n.text.horizontal;
                text.verticalAlignment = (VerticalAlignmentOptions)n.text.vertical;
            }
            if (n.rectmask2d) go.AddComponent<RectMask2D>();
            if (n.mask) { var mask = go.AddComponent<Mask>(); mask.showMaskGraphic = n.show_mask_graphic; }
            if (n.has_aspect_fitter)
            {
                var aspect = go.AddComponent<AspectRatioFitter>(); aspect.aspectMode = (AspectRatioFitter.AspectMode)n.aspect_fitter.mode; aspect.aspectRatio = n.aspect_fitter.ratio;
            }
            if (n.has_layout_element)
            {
                var e = go.AddComponent<LayoutElement>(); var s = n.layout_element;
                e.ignoreLayout = s.ignore; e.minWidth = V(s.min, 0, -1); e.minHeight = V(s.min, 1, -1);
                e.preferredWidth = V(s.preferred, 0, -1); e.preferredHeight = V(s.preferred, 1, -1);
                e.flexibleWidth = V(s.flexible, 0, -1); e.flexibleHeight = V(s.flexible, 1, -1);
            }
            if (n.has_layout)
            {
                HorizontalOrVerticalLayoutGroup l = n.layout.axis == 0 ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<HorizontalLayoutGroup>() : go.AddComponent<VerticalLayoutGroup>();
                var s = n.layout; l.padding = Padding(s.padding); l.childAlignment = (TextAnchor)s.alignment; l.spacing = s.spacing;
                l.childForceExpandWidth = B(s.expand, 0); l.childForceExpandHeight = B(s.expand, 1);
                l.childControlWidth = B(s.control, 0); l.childControlHeight = B(s.control, 1);
                l.childScaleWidth = B(s.child_scale, 0); l.childScaleHeight = B(s.child_scale, 1); l.reverseArrangement = s.reverse;
            }
            if (n.has_grid)
            {
                var l = go.AddComponent<GridLayoutGroup>(); var s = n.grid;
                l.padding = Padding(s.padding); l.childAlignment = (TextAnchor)s.alignment;
                l.startCorner = (GridLayoutGroup.Corner)s.corner; l.startAxis = (GridLayoutGroup.Axis)s.axis;
                l.cellSize = Vec(s.cell); l.spacing = Vec(s.spacing); l.constraint = (GridLayoutGroup.Constraint)s.constraint; l.constraintCount = Mathf.Max(1, s.count);
            }
            if (n.has_size_fitter)
            {
                var f = go.AddComponent<ContentSizeFitter>(); f.horizontalFit = (ContentSizeFitter.FitMode)n.size_fitter[0]; f.verticalFit = (ContentSizeFitter.FitMode)n.size_fitter[1];
            }
            foreach (Node child in n.children ?? Array.Empty<Node>()) Build(child, rect);
            if (n.button || Has(n, "Button"))
            {
                var button = go.AddComponent<Button>(); button.targetGraphic = HitGraphic(go); button.transition = Selectable.Transition.None;
            }
            if (n.toggle)
            {
                var toggle = go.AddComponent<Toggle>(); toggle.targetGraphic = HitGraphic(go); toggle.transition = Selectable.Transition.None;
                Transform mark = Deep(rect, "Checkmark") ?? Deep(rect, "Select") ?? Deep(rect, "Sel"); if (mark != null) toggle.graphic = mark.GetComponent<Graphic>();
            }
            if (Has(n, "Slider")) go.AddComponent<Slider>();
            if (Has(n, "TMP_InputField"))
            {
                var input = go.AddComponent<TMP_InputField>();
                input.textViewport = Deep(rect, "Text Area") as RectTransform;
                input.textComponent = Deep(input.textViewport, "Text")?.GetComponent<TMP_Text>();
                input.placeholder = Deep(input.textViewport, "Placeholder")?.GetComponent<Graphic>();
                if (input.textComponent == null) throw new InvalidOperationException("Official name input text component is missing.");
                input.fontAsset = font;
                input.targetGraphic = HitGraphic(go);
            }
            if (n.has_scroll)
            {
                var scroll = go.AddComponent<ScrollRect>(); scroll.horizontal = n.scroll.horizontal; scroll.vertical = n.scroll.vertical;
                scroll.scrollSensitivity = n.scroll.sensitivity; scroll.viewport = Deep(rect, "Viewport") as RectTransform;
                scroll.content = Deep(rect, "Content") as RectTransform;
            }
            if (Has(n, "UIMesh")) go.AddComponent<MeshRenderer>();
            go.SetActive(n.active);
            return go;
        }

        private static Graphic HitGraphic(GameObject go)
        {
            Graphic g = go.GetComponent<Graphic>();
            if (g == null) { var image = go.AddComponent<Image>(); image.color = Color.clear; g = image; }
            g.raycastTarget = true; return g;
        }
        private static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root) { if (child.name == name) return child; Transform found = Deep(child, name); if (found != null) return found; }
            return null;
        }
        private static bool Has(Node node, string script) => Array.IndexOf(node.scripts ?? Array.Empty<string>(), script) >= 0;
        private static float V(float[] a, int i, float fallback = 0) => a != null && a.Length > i ? a[i] : fallback;
        private static bool B(bool[] a, int i) => a != null && a.Length > i && a[i];
        private static Vector2 Vec(float[] a) => new Vector2(V(a, 0), V(a, 1));
        private static Color ColorOf(float[] a) => new Color(V(a, 0, 1), V(a, 1, 1), V(a, 2, 1), V(a, 3, 1));
        private static RectOffset Padding(int[] a) => a == null ? new RectOffset() : new RectOffset(a[0], a[1], a[2], a[3]);
    }
}
