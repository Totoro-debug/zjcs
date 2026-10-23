using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Official story UI payload: prefab layout trees plus one sprite atlas.</summary>
    public sealed class OpeningUiData
    {
        public string Manifest = string.Empty;
        public string DirectoryPath = string.Empty;
        public readonly List<byte[]> AtlasBytes = new List<byte[]>();
        public readonly List<OpeningUiSprite> AtlasPages = new List<OpeningUiSprite>();
        public readonly Dictionary<string, OpeningUiSprite> Sprites =
            new Dictionary<string, OpeningUiSprite>(StringComparer.Ordinal);
        public readonly Dictionary<string, OpeningUiNode> Prefabs =
            new Dictionary<string, OpeningUiNode>(StringComparer.Ordinal);

        public static OpeningUiData Parse(byte[] bytes, string directory)
        {
            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(bytes), "ui");
            var data = new OpeningUiData
            {
                Manifest = OpeningJson.String(root, "manifest"),
                DirectoryPath = directory,
            };
            if (data.Manifest != OpeningAssets.Manifest)
                throw new FormatException("Opening UI manifest is not pinned.");
            var atlases = new List<Dictionary<string, object>>();
            if (root.TryGetValue("atlases", out object pages) && pages is List<object> pageList)
                foreach (object entry in pageList)
                    atlases.Add(OpeningJson.Object(entry, "atlas page"));
            else
                atlases.Add(OpeningJson.Object(root["atlas"], "atlas"));
            foreach (Dictionary<string, object> atlas in atlases)
            {
                string atlasPath = Path.Combine(directory, OpeningJson.String(atlas, "file"));
                if (!File.Exists(atlasPath)) throw new FileNotFoundException("Opening UI atlas is missing.", atlasPath);
                byte[] pageBytes = File.ReadAllBytes(atlasPath);
                data.AtlasBytes.Add(pageBytes);
                data.AtlasPages.Add(new OpeningUiSprite
                {
                    Width = OpeningJson.Int(atlas, "width"),
                    Height = OpeningJson.Int(atlas, "height"),
                    Name = OpeningJson.String(atlas, "file"),
                });
            }
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(root["sprites"], "sprites"))
            {
                Dictionary<string, object> sprite = OpeningJson.Object(entry.Value, "sprite");
                List<object> pivot = OpeningJson.Array(sprite["pivot"], "pivot");
                List<object> border = null;
                if (sprite.TryGetValue("border", out object borderValue)) border = OpeningJson.Array(borderValue, "border");
                data.Sprites[entry.Key] = new OpeningUiSprite
                {
                    Name = entry.Key,
                    X = OpeningJson.Int(sprite, "x"),
                    Y = OpeningJson.Int(sprite, "y"),
                    Width = OpeningJson.Int(sprite, "w"),
                    Height = OpeningJson.Int(sprite, "h"),
                    Pivot = new Vector2((float)OpeningJson.ToNumber(pivot[0]), (float)OpeningJson.ToNumber(pivot[1])),
                    Border = border == null ? Vector4.zero : new Vector4(
                        (float)OpeningJson.ToNumber(border[0]), (float)OpeningJson.ToNumber(border[1]),
                        (float)OpeningJson.ToNumber(border[2]), (float)OpeningJson.ToNumber(border[3])),
                    PixelsPerUnit = (float)OpeningJson.Number(sprite, "ppu", 100),
                    Atlas = OpeningJson.Int(sprite, "atlas"),
                };
            }
            foreach (object item in OpeningJson.Array(root["prefabs"], "prefabs"))
            {
                Dictionary<string, object> prefab = OpeningJson.Object(item, "prefab");
                data.Prefabs[OpeningJson.String(prefab, "key")] = ParseNode(OpeningJson.Object(prefab["root"], "root"));
            }
            return data;
        }

        private static OpeningUiNode ParseNode(Dictionary<string, object> node)
        {
            var converted = new OpeningUiNode
            {
                Name = OpeningJson.String(node, "name"),
                Path = OpeningJson.String(node, "path"),
                Active = OpeningJson.Bool(node, "active", true),
                Button = OpeningJson.Bool(node, "button"),
                Mask = OpeningJson.Bool(node, "mask"),
                HasImage = OpeningJson.Bool(node, "has_image"),
                HasText = OpeningJson.Bool(node, "has_text"),
                CanvasGroup = OpeningJson.Bool(node, "canvas_group"),
                CanvasGroupInteractable = OpeningJson.Bool(node, "canvas_group_interactable", true),
                CanvasGroupBlocksRaycasts = OpeningJson.Bool(node, "canvas_group_blocks_raycasts", true),
                CanvasGroupIgnoreParentGroups = OpeningJson.Bool(node, "canvas_group_ignore_parent_groups"),
                Sprite = OpeningJson.String(node, "sprite"),
                Alpha = (float)OpeningJson.Number(node, "alpha", 1),
                Type = OpeningJson.Int(node, "image_type"),
                PreserveAspect = OpeningJson.Bool(node, "image_preserve_aspect"),
                TextValue = OpeningJson.String(node, "text_value"),
                FontSize = (float)OpeningJson.Number(node, "text_font_size", 32),
                FontWeight = OpeningJson.Int(node, "text_font_weight", 400),
                FontStyle = OpeningJson.Int(node, "text_font_style"),
                Alignment = OpeningJson.Int(node, "text_alignment"),
                Margin = ReadVector4(node, "text_margin"),
                WordWrap = OpeningJson.Bool(node, "text_word_wrap"),
                Overflow = OpeningJson.Int(node, "text_overflow"),
                AutoSizing = OpeningJson.Bool(node, "text_auto_sizing"),
                HasMesh = OpeningJson.Bool(node, "has_mesh"),
                MeshRaycastTarget = OpeningJson.Bool(node, "mesh_raycast_target"),
                MeshMaskable = OpeningJson.Bool(node, "mesh_maskable", true),
                UiParticle = OpeningJson.Bool(node, "ui_particle"),
                IsRectTransform = OpeningJson.Bool(node, "is_rect_transform", true),
                FontSizeRange = ReadVector(node, "text_font_size_range", Vector2.zero),
                AnchorMin = ReadVector(node, "anchor_min", new Vector2(0.5f, 0.5f)),
                AnchorMax = ReadVector(node, "anchor_max", new Vector2(0.5f, 0.5f)),
                AnchoredPosition = ReadVector(node, "anchored_position", Vector2.zero),
                SizeDelta = ReadVector(node, "size_delta", Vector2.zero),
                Pivot = ReadVector(node, "pivot", new Vector2(0.5f, 0.5f)),
                Scale = ReadVector(node, "scale", ReadVector(node, "local_scale", Vector2.one)),
                LocalPosition = ReadVector3(node, "local_position", Vector3.zero),
                LocalRotation = ReadVector4(node, "local_rotation", new Vector4(0, 0, 0, 1)),
            };
            converted.Window = ParseWindow(node);
            converted.ImageColor = ReadColor(node, "image_color", Color.white);
            converted.TextColor = ReadColor(node, "text_color", Color.white);
            converted.MeshColor = ReadColor(node, "mesh_color", Color.white);
            if (converted.HasMesh)
            {
                Dictionary<string, object> mesh = OpeningJson.Object(node["mesh"], "mesh");
                converted.Mesh = ParseMesh(mesh);
                converted.MeshMaterial = ParseMaterial(OpeningJson.Object(node["mesh_material"], "mesh material"));
            }
            if (node.TryGetValue("particle_system", out object particleSystem))
                converted.ParticleSystem = OpeningJson.Object(particleSystem, "particle system");
            if (node.TryGetValue("particle_renderer", out object particleRenderer))
                converted.ParticleRenderer = OpeningJson.Object(particleRenderer, "particle renderer");
            if (node.TryGetValue("particle_material", out object particleMaterial))
                converted.ParticleMaterial = ParseMaterial(OpeningJson.Object(particleMaterial, "particle material"));
            if (node.TryGetValue("children", out object children) && children is List<object> list)
                foreach (object item in list) converted.Children.Add(ParseNode(OpeningJson.Object(item, "child")));
            return converted;
        }

        private static OpeningUiWindowData ParseWindow(Dictionary<string, object> node)
        {
            if (!node.TryGetValue("window", out object value) || !(value is Dictionary<string, object> window))
                return null;
            var result = new OpeningUiWindowData
            {
                ShowClip = OpeningJson.String(window, "show_clip"),
                HideClip = OpeningJson.String(window, "hide_clip"),
                LoopClip = OpeningJson.String(window, "loop_clip"),
            };
            if (!window.TryGetValue("clips", out object clipsValue)) return result;
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(clipsValue, "window clips"))
            {
                Dictionary<string, object> clip = OpeningJson.Object(entry.Value, "window clip");
                var animation = new OpeningUiAnimationData
                {
                    Duration = (float)OpeningJson.Number(clip, "duration"),
                    Loop = OpeningJson.Bool(clip, "loop"),
                };
                if (clip.TryGetValue("tracks", out object tracksValue))
                    foreach (object trackValue in OpeningJson.Array(tracksValue, "animation tracks"))
                    {
                        Dictionary<string, object> track = OpeningJson.Object(trackValue, "animation track");
                        var keys = new List<Keyframe>();
                        foreach (object keyValue in OpeningJson.Array(track["keys"], "animation keys"))
                        {
                            List<object> key = OpeningJson.Array(keyValue, "animation key");
                            float time = (float)OpeningJson.ToNumber(key[0]);
                            float sample = (float)OpeningJson.ToNumber(key[1]);
                            keys.Add(key.Count >= 4 && key[2] != null && key[3] != null
                                ? new Keyframe(time, sample, (float)OpeningJson.ToNumber(key[2]),
                                    (float)OpeningJson.ToNumber(key[3]))
                                : new Keyframe(time, sample));
                        }
                        if (keys.Count == 0) continue;
                        var curve = new AnimationCurve(keys.ToArray())
                        {
                            preWrapMode = WrapMode.ClampForever,
                            postWrapMode = WrapMode.ClampForever,
                        };
                        animation.Tracks.Add(new OpeningUiAnimationTrack
                        {
                            Path = OpeningJson.String(track, "path"),
                            Property = OpeningJson.String(track, "property"),
                            Curve = curve,
                        });
                    }
                result.Clips[entry.Key] = animation;
            }
            return result;
        }

        private static OpeningUiMeshData ParseMesh(Dictionary<string, object> mesh)
        {
            var result = new OpeningUiMeshData();
            var vertices = new List<Vector3>();
            foreach (object item in OpeningJson.Array(mesh["vertices"], "mesh vertices"))
            {
                List<object> value = OpeningJson.Array(item, "mesh vertex");
                if (value.Count < 3) throw new FormatException("Opening UI mesh vertex is incomplete.");
                vertices.Add(new Vector3((float)OpeningJson.ToNumber(value[0]),
                    (float)OpeningJson.ToNumber(value[1]), (float)OpeningJson.ToNumber(value[2])));
            }
            var uvs = new List<Vector2>();
            foreach (object item in OpeningJson.Array(mesh["uv"], "mesh UVs"))
            {
                List<object> value = OpeningJson.Array(item, "mesh UV");
                if (value.Count < 2) throw new FormatException("Opening UI mesh UV is incomplete.");
                uvs.Add(new Vector2((float)OpeningJson.ToNumber(value[0]), (float)OpeningJson.ToNumber(value[1])));
            }
            var colors = new List<Color>();
            if (mesh.TryGetValue("colors", out object rawColors) && rawColors is List<object> colorList)
                foreach (object item in colorList)
                {
                    List<object> value = OpeningJson.Array(item, "mesh color");
                    if (value.Count < 4) throw new FormatException("Opening UI mesh color is incomplete.");
                    colors.Add(new Color((float)OpeningJson.ToNumber(value[0]),
                        (float)OpeningJson.ToNumber(value[1]), (float)OpeningJson.ToNumber(value[2]),
                        (float)OpeningJson.ToNumber(value[3])));
                }
            var triangles = new List<int>();
            foreach (object item in OpeningJson.Array(mesh["triangles"], "mesh triangles"))
                triangles.Add(Convert.ToInt32(OpeningJson.ToNumber(item)));
            if (vertices.Count == 0 || vertices.Count != uvs.Count || triangles.Count % 3 != 0)
                throw new FormatException("Opening UI mesh has inconsistent geometry arrays.");
            if (colors.Count != 0 && colors.Count != vertices.Count)
                throw new FormatException("Opening UI mesh color count differs from its vertices.");
            result.Vertices = vertices.ToArray();
            result.Uvs = uvs.ToArray();
            result.Colors = colors.ToArray();
            result.Triangles = triangles.ToArray();
            return result;
        }

        private static OpeningUiMaterialData ParseMaterial(Dictionary<string, object> material)
        {
            var result = new OpeningUiMaterialData
            {
                Name = OpeningJson.String(material, "name"),
                ShaderPathId = OpeningJson.String(material, "shader_path_id"),
                RenderQueue = OpeningJson.Int(material, "render_queue", -1),
            };
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(material["floats"], "material floats"))
                result.Floats[entry.Key] = Convert.ToSingle(OpeningJson.ToNumber(entry.Value));
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(material["vectors"], "material vectors"))
                result.Vectors[entry.Key] = ReadColor(
                    new Dictionary<string, object> { ["value"] = entry.Value }, "value", Color.white);
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(material["textures"], "material textures"))
            {
                Dictionary<string, object> texture = OpeningJson.Object(entry.Value, "material texture");
                result.Textures[entry.Key] = new OpeningUiTextureData
                {
                    File = OpeningJson.String(texture, "file"),
                    Scale = ReadVector(texture, "scale", Vector2.one),
                    Offset = ReadVector(texture, "offset", Vector2.zero),
                    FilterMode = OpeningJson.Int(texture, "filter_mode", (int)FilterMode.Bilinear),
                    AnisoLevel = OpeningJson.Int(texture, "aniso_level", 1),
                    WrapU = OpeningJson.Int(texture, "wrap_u", (int)TextureWrapMode.Repeat),
                    WrapV = OpeningJson.Int(texture, "wrap_v", (int)TextureWrapMode.Repeat),
                    WrapW = OpeningJson.Int(texture, "wrap_w", (int)TextureWrapMode.Repeat),
                };
            }
            return result;
        }

        private static Vector2 ReadVector(Dictionary<string, object> node, string key, Vector2 fallback)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 2)
                return fallback;
            return new Vector2((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]));
        }

        private static Vector3 ReadVector3(Dictionary<string, object> node, string key, Vector3 fallback)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 3)
                return fallback;
            return new Vector3((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]),
                (float)OpeningJson.ToNumber(list[2]));
        }

        private static Vector4 ReadVector4(Dictionary<string, object> node, string key, Vector4 fallback = default)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 4)
                return fallback;
            return new Vector4((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]),
                (float)OpeningJson.ToNumber(list[2]), (float)OpeningJson.ToNumber(list[3]));
        }

        private static Color ReadColor(Dictionary<string, object> node, string key, Color fallback)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 4)
                return fallback;
            return new Color((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]),
                (float)OpeningJson.ToNumber(list[2]), (float)OpeningJson.ToNumber(list[3]));
        }
    }

    public sealed class OpeningUiSprite
    {
        public string Name = string.Empty;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        public Vector4 Border;
        public float PixelsPerUnit = 100;
        public int Atlas;
    }

    public sealed class OpeningUiMeshData
    {
        public Vector3[] Vertices = Array.Empty<Vector3>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        public Color[] Colors = Array.Empty<Color>();
        public int[] Triangles = Array.Empty<int>();
    }

    public sealed class OpeningUiMaterialData
    {
        public string Name = string.Empty;
        public string ShaderPathId = string.Empty;
        public int RenderQueue = -1;
        public readonly Dictionary<string, float> Floats = new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly Dictionary<string, Color> Vectors = new Dictionary<string, Color>(StringComparer.Ordinal);
        public readonly Dictionary<string, OpeningUiTextureData> Textures =
            new Dictionary<string, OpeningUiTextureData>(StringComparer.Ordinal);
    }

    public sealed class OpeningUiTextureData
    {
        public string File = string.Empty;
        public Vector2 Scale = Vector2.one;
        public Vector2 Offset = Vector2.zero;
        public int FilterMode = (int)UnityEngine.FilterMode.Bilinear;
        public int AnisoLevel = 1;
        public int WrapU = (int)TextureWrapMode.Repeat;
        public int WrapV = (int)TextureWrapMode.Repeat;
        public int WrapW = (int)TextureWrapMode.Repeat;
    }

    public sealed class OpeningUiNode
    {
        public string Name = string.Empty;
        public string Path = string.Empty;
        public bool Active = true;
        public bool Button;
        public bool Mask;
        public bool HasImage;
        public bool HasText;
        public bool HasMesh;
        public bool MeshRaycastTarget;
        public bool MeshMaskable = true;
        public bool UiParticle;
        public bool IsRectTransform = true;
        public string Sprite = string.Empty;
        public Color ImageColor = Color.white;
        public Color TextColor = Color.white;
        public Color MeshColor = Color.white;
        public OpeningUiMeshData Mesh;
        public OpeningUiMaterialData MeshMaterial;
        public Dictionary<string, object> ParticleSystem;
        public Dictionary<string, object> ParticleRenderer;
        public OpeningUiMaterialData ParticleMaterial;
        public int Type;
        public bool PreserveAspect;
        public string TextValue = string.Empty;
        public float FontSize = 32;
        public int FontWeight = 400;
        public int FontStyle;
        public int Alignment;
        public Vector4 Margin;
        public bool WordWrap;
        public int Overflow;
        public bool AutoSizing;
        public Vector2 FontSizeRange = Vector2.zero;
        public float Alpha = 1;
        public Vector2 AnchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 AnchorMax = new Vector2(0.5f, 0.5f);
        public Vector2 AnchoredPosition = Vector2.zero;
        public Vector2 SizeDelta = Vector2.zero;
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        public Vector2 Scale = Vector2.one;
        public Vector3 LocalPosition = Vector3.zero;
        public Vector4 LocalRotation = new Vector4(0, 0, 0, 1);
        public bool CanvasGroup;
        public bool CanvasGroupInteractable = true;
        public bool CanvasGroupBlocksRaycasts = true;
        public bool CanvasGroupIgnoreParentGroups;
        public OpeningUiWindowData Window;
        public readonly List<OpeningUiNode> Children = new List<OpeningUiNode>();
    }

    public sealed class OpeningUiWindowData
    {
        public string ShowClip = string.Empty;
        public string HideClip = string.Empty;
        public string LoopClip = string.Empty;
        public readonly Dictionary<string, OpeningUiAnimationData> Clips =
            new Dictionary<string, OpeningUiAnimationData>(StringComparer.Ordinal);
    }

    public sealed class OpeningUiAnimationData
    {
        public float Duration;
        public bool Loop;
        public readonly List<OpeningUiAnimationTrack> Tracks = new List<OpeningUiAnimationTrack>();
    }

    public sealed class OpeningUiAnimationTrack
    {
        public string Path = string.Empty;
        public string Property = string.Empty;
        public AnimationCurve Curve;
    }

    /// <summary>Instantiates official story panels from the shipped layout payload.</summary>
    public sealed class OpeningUiView : IDisposable
    {
        private readonly OpeningUiData data;
        private readonly TMP_FontAsset font;
        private readonly List<Texture2D> atlases = new List<Texture2D>();
        private readonly List<Texture2D> materialTextures = new List<Texture2D>();
        private readonly List<Material> meshMaterials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> materialTexturesByKey =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> meshMaterialsByKey =
            new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, Mesh> meshesByPath = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> bindings = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly GameObject root;

        public OpeningUiView(OpeningUiData data, TMP_FontAsset font, Transform parent, int sortingOrder)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.font = font;
            for (int index = 0; index < data.AtlasBytes.Count; index++)
            {
                var page = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!page.LoadImage(data.AtlasBytes[index]))
                    throw new InvalidOperationException("Opening UI atlas could not be decoded: page " + index);
                page.name = "OpeningUiAtlas_" + index;
                atlases.Add(page);
            }
            var canvasObject = new GameObject("OpeningCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            this.root = canvasObject;
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public Transform Root => root.transform;

        public GameObject Create(string key, Transform parent = null)
        {
            if (!data.Prefabs.TryGetValue(key, out OpeningUiNode node))
                throw new InvalidOperationException("Official story UI prefab is missing: " + key);
            bindings.Clear();
            GameObject created = Build(node, parent == null ? root.transform : parent);
            created.name = "Opening_" + key;
            return created;
        }

        public IEnumerator PlayAnimation(GameObject instance, string prefabKey, string clipName, bool instant = false)
        {
            if (instance == null || string.IsNullOrEmpty(clipName)
                || !data.Prefabs.TryGetValue(prefabKey, out OpeningUiNode prefab)
                || prefab.Window == null
                || !prefab.Window.Clips.TryGetValue(clipName, out OpeningUiAnimationData clip))
                yield break;

            var tracks = new List<BoundAnimationTrack>();
            string rootPath = "/" + prefab.Name;
            foreach (OpeningUiAnimationTrack track in clip.Tracks)
            {
                string relativePath = track.Path.StartsWith(rootPath, StringComparison.Ordinal)
                    ? track.Path.Substring(rootPath.Length).TrimStart('/')
                    : track.Path.TrimStart('/');
                Transform target = relativePath.Length == 0 ? instance.transform : instance.transform.Find(relativePath);
                if (target != null) tracks.Add(new BoundAnimationTrack(target, track));
            }

            float duration = Mathf.Max(0, clip.Duration);
            if (instant || duration == 0)
            {
                foreach (BoundAnimationTrack track in tracks) Apply(track, duration);
                yield break;
            }

            float start = Time.realtimeSinceStartup;
            float elapsed = 0;
            while (elapsed < duration)
            {
                foreach (BoundAnimationTrack track in tracks) Apply(track, elapsed);
                yield return null;
                elapsed = Time.realtimeSinceStartup - start;
            }
            foreach (BoundAnimationTrack track in tracks) Apply(track, duration);
        }

        private static void Apply(BoundAnimationTrack bound, float time)
        {
            float value = bound.Track.Curve.Evaluate(time);
            string property = bound.Track.Property;
            if (property == "m_IsActive")
            {
                bound.Transform.gameObject.SetActive(value >= 0.5f);
                return;
            }
            if (property == "m_Alpha" && bound.CanvasGroup != null) { bound.CanvasGroup.alpha = value; return; }
            if (property == "m_BlocksRaycasts" && bound.CanvasGroup != null)
            {
                bound.CanvasGroup.blocksRaycasts = value >= 0.5f;
                return;
            }
            if (property.StartsWith("m_Color.", StringComparison.Ordinal) && bound.Graphic != null)
            {
                bound.Graphic.color = SetColorChannel(bound.Graphic.color, property, value);
                return;
            }
            if (property.StartsWith("m_fontColor.", StringComparison.Ordinal) && bound.Text != null)
            {
                bound.Text.color = SetColorChannel(bound.Text.color, property, value);
                return;
            }
            if (property == "m_fontSize" && bound.Text != null) { bound.Text.fontSize = value; return; }
            if (property == "m_FillAmount" && bound.Image != null) { bound.Image.fillAmount = value; return; }
            if (property == "m_Enabled" && bound.Behaviour != null) { bound.Behaviour.enabled = value >= 0.5f; return; }
            if (property == "m_SortingOrder" && bound.Canvas != null) { bound.Canvas.sortingOrder = Mathf.RoundToInt(value); return; }
            if (property.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal) && bound.Rect != null)
            {
                Vector2 position = bound.Rect.anchoredPosition;
                SetAxis(ref position, property, "m_AnchoredPosition.", value);
                bound.Rect.anchoredPosition = position;
                return;
            }
            if (property.StartsWith("m_SizeDelta.", StringComparison.Ordinal) && bound.Rect != null)
            {
                Vector2 size = bound.Rect.sizeDelta;
                SetAxis(ref size, property, "m_SizeDelta.", value);
                bound.Rect.sizeDelta = size;
                return;
            }
            if (property.StartsWith("position.", StringComparison.Ordinal))
            {
                Vector3 position = bound.Transform.localPosition;
                SetAxis(ref position, property, "position.", value);
                bound.Transform.localPosition = position;
                return;
            }
            if (property.StartsWith("scale.", StringComparison.Ordinal))
            {
                Vector3 scale = bound.Transform.localScale;
                SetAxis(ref scale, property, "scale.", value);
                bound.Transform.localScale = scale;
                return;
            }
            if (property.StartsWith("rotation.", StringComparison.Ordinal)
                || property.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal))
            {
                Vector3 rotation = bound.Transform.localEulerAngles;
                SetAxis(ref rotation, property, property.StartsWith("rotation.", StringComparison.Ordinal)
                    ? "rotation." : "localEulerAnglesRaw.", value);
                bound.Transform.localEulerAngles = rotation;
                return;
            }
            if (property.StartsWith("_", StringComparison.Ordinal) && bound.Mesh != null)
                bound.Mesh.SetAnimatedProperty(property, value);
        }

        private static Color SetColorChannel(Color color, string property, float value)
        {
            switch (property[property.Length - 1])
            {
                case 'r': color.r = value; break;
                case 'g': color.g = value; break;
                case 'b': color.b = value; break;
                case 'a': color.a = value; break;
            }
            return color;
        }

        private static void SetAxis(ref Vector2 vector, string property, string prefix, float value)
        {
            if (property.EndsWith(prefix + "x", StringComparison.Ordinal)) vector.x = value;
            else if (property.EndsWith(prefix + "y", StringComparison.Ordinal)) vector.y = value;
        }

        private static void SetAxis(ref Vector3 vector, string property, string prefix, float value)
        {
            if (property.EndsWith(prefix + "x", StringComparison.Ordinal)) vector.x = value;
            else if (property.EndsWith(prefix + "y", StringComparison.Ordinal)) vector.y = value;
            else if (property.EndsWith(prefix + "z", StringComparison.Ordinal)) vector.z = value;
        }

        private sealed class BoundAnimationTrack
        {
            public readonly Transform Transform;
            public readonly OpeningUiAnimationTrack Track;
            public readonly RectTransform Rect;
            public readonly CanvasGroup CanvasGroup;
            public readonly Graphic Graphic;
            public readonly TMP_Text Text;
            public readonly Image Image;
            public readonly Behaviour Behaviour;
            public readonly Canvas Canvas;
            public readonly OpeningUiMeshGraphic Mesh;

            public BoundAnimationTrack(Transform transform, OpeningUiAnimationTrack track)
            {
                Transform = transform;
                Track = track;
                Rect = transform as RectTransform;
                CanvasGroup = transform.GetComponent<CanvasGroup>();
                Graphic = transform.GetComponent<Graphic>();
                Text = transform.GetComponent<TMP_Text>();
                Image = transform.GetComponent<Image>();
                Behaviour = transform.GetComponent<Behaviour>();
                Canvas = transform.GetComponent<Canvas>();
                Mesh = transform.GetComponent<OpeningUiMeshGraphic>();
            }
        }

        public GameObject Find(string path)
        {
            return bindings.TryGetValue(path, out GameObject value) ? value : null;
        }

        public bool SetText(string path, string value)
        {
            GameObject node = Find(path);
            if (node != null)
            {
                TMP_Text text = node.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(path, StringComparison.Ordinal)) continue;
                TMP_Text text = entry.Value.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            return false;
        }

        public bool SetFirstTextEnding(string suffix, string value)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                TMP_Text text = entry.Value.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            return false;
        }

        public bool SetTextEnding(GameObject scope, string suffix, string value)
        {
            if (scope == null) return false;
            foreach (Transform child in scope.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text == null) continue;
                text.text = value ?? string.Empty;
                return true;
            }
            return false;
        }

        public bool SetImage(GameObject scope, string nodeName, string spriteName, bool enabled, Vector2 offset)
        {
            if (scope == null) return false;
            Transform target = null;
            foreach (Transform child in scope.GetComponentsInChildren<Transform>(true))
                if (child.name == nodeName) { target = child; break; }
            if (target == null) return false;
            Image image = target.GetComponent<Image>();
            if (image == null) return false;
            image.sprite = null;
            image.enabled = false;
            if (enabled && TryGetSprite(spriteName, out Sprite sprite))
            {
                image.sprite = sprite;
                image.enabled = true;
                image.SetNativeSize();
            }
            ((RectTransform)target).anchoredPosition = offset;
            return true;
        }

        public bool SetActive(string path, bool active)
        {
            GameObject node = Find(path);
            if (node == null) return false;
            node.SetActive(active);
            return true;
        }

        public bool SetActiveEnding(string suffix, bool active)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                entry.Value.SetActive(active);
                return true;
            }
            return false;
        }

        public GameObject FindEnding(string suffix)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
                if (entry.Key.EndsWith(suffix, StringComparison.Ordinal)) return entry.Value;
            return null;
        }

        private GameObject Build(OpeningUiNode node, Transform parent)
        {
            var gameObject = new GameObject(node.Name, typeof(RectTransform));
            gameObject.SetActive(false);
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            // A few official containers are point-anchored with a zero size and rely on
            // their panel script to stretch them; without that script their stretched
            // children would collapse, so those containers inherit the parent rect.
            bool container = node.Children.Count > 0
                && node.AnchorMin == node.AnchorMax && node.SizeDelta == Vector2.zero;
            rect.anchorMin = container ? Vector2.zero : node.AnchorMin;
            rect.anchorMax = container ? Vector2.one : node.AnchorMax;
            rect.anchoredPosition = container ? Vector2.zero : node.AnchoredPosition;
            rect.sizeDelta = container ? Vector2.zero : node.SizeDelta;
            rect.pivot = container ? new Vector2(0.5f, 0.5f) : node.Pivot;
            rect.localScale = new Vector3(node.Scale.x, node.Scale.y, 1);
            if (!node.IsRectTransform) rect.localPosition = node.LocalPosition;
            rect.localRotation = new Quaternion(node.LocalRotation.x, node.LocalRotation.y,
                node.LocalRotation.z, node.LocalRotation.w);
            if (node.ParticleSystem != null)
            {
                ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
                OpeningParticleSystemFactory.Configure(system, node.ParticleSystem);
                ParticleSystemRenderer renderer = gameObject.GetComponent<ParticleSystemRenderer>();
                OpeningParticleSystemFactory.ConfigureRenderer(renderer, node.ParticleRenderer);
                renderer.sharedMaterial = GetMeshMaterial(node.ParticleMaterial);
            }
            if (node.HasMesh)
            {
                var graphic = gameObject.AddComponent<OpeningUiMeshGraphic>();
                graphic.raycastTarget = node.MeshRaycastTarget;
                graphic.maskable = node.MeshMaskable;
                graphic.Configure(GetMesh(node), GetMeshMaterial(node.MeshMaterial), node.MeshColor);
            }
            if (node.CanvasGroup || node.Alpha < 0.999f)
            {
                CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = node.Alpha;
                canvasGroup.interactable = node.CanvasGroupInteractable;
                canvasGroup.blocksRaycasts = node.CanvasGroupBlocksRaycasts;
                canvasGroup.ignoreParentGroups = node.CanvasGroupIgnoreParentGroups;
            }
            if (node.HasImage)
            {
                Image image = gameObject.AddComponent<Image>();
                TryGetSprite(node.Sprite, out Sprite sprite);
                image.sprite = sprite;
                image.color = node.ImageColor;
                image.type = (Image.Type)Mathf.Clamp(node.Type, 0, (int)Image.Type.Filled);
                image.preserveAspect = node.PreserveAspect;
                if (node.Type == (int)Image.Type.Sliced && node.Sprite.Length > 0) image.type = Image.Type.Sliced;
            }
            if (node.HasText)
            {
                TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.text = node.TextValue ?? string.Empty;
                text.fontSize = Mathf.Max(1, node.FontSize);
                text.fontWeight = (FontWeight)Mathf.Clamp(node.FontWeight, 100, 900);
                text.fontStyle = (FontStyles)Mathf.Max(0, node.FontStyle);
                text.color = node.TextColor;
                // Alignment, margins, wrapping, overflow and auto-sizing are the
                // official TMP_Text serialized values (0xFFFF alignment resolves
                // through the horizontal/vertical axis fields during extraction).
                text.alignment = (TextAlignmentOptions)node.Alignment;
                text.margin = node.Margin;
                text.enableWordWrapping = node.WordWrap;
                text.overflowMode = (TextOverflowModes)Mathf.Clamp(node.Overflow, 0, 6);
                if (node.AutoSizing && node.FontSizeRange.y > 0f)
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = node.FontSizeRange.x;
                    text.fontSizeMax = node.FontSizeRange.y;
                }
                text.raycastTarget = false;
            }
            if (node.Button)
            {
                if (gameObject.GetComponent<Image>() == null)
                    gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                gameObject.AddComponent<Button>();
            }
            if (node.Mask) gameObject.AddComponent<Mask>().showMaskGraphic = false;
            if (!string.IsNullOrEmpty(node.Path)) bindings[node.Path] = gameObject;
            foreach (OpeningUiNode child in node.Children) Build(child, rect);
            if (node.UiParticle)
            {
                Coffee.UIExtensions.UIParticle particle = gameObject.AddComponent<Coffee.UIExtensions.UIParticle>();
                particle.raycastTarget = false;
            }
            gameObject.SetActive(node.Active);
            return gameObject;
        }

        private Mesh GetMesh(OpeningUiNode node)
        {
            if (meshesByPath.TryGetValue(node.Path, out Mesh existing)) return existing;
            OpeningUiMeshData source = node.Mesh;
            var mesh = new Mesh { name = "Opening " + node.Name };
            var vertices = new Vector3[source.Vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(source.Vertices[i].x * 100f, source.Vertices[i].y * 100f,
                    source.Vertices[i].z);
            mesh.vertices = vertices;
            mesh.uv = source.Uvs;
            if (source.Colors.Length > 0) mesh.colors = source.Colors;
            mesh.triangles = source.Triangles;
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            meshesByPath[node.Path] = mesh;
            return mesh;
        }

        private Material GetMeshMaterial(OpeningUiMaterialData source)
        {
            if (source == null) throw new InvalidOperationException("Official UI mesh has no material data.");
            string key = source.ShaderPathId + ":" + source.Name;
            if (meshMaterialsByKey.TryGetValue(key, out Material existing)) return existing;
            Shader shader = Shader.Find(OpeningUiMeshGraphic.ShaderName(source.ShaderPathId));
            if (shader == null)
                throw new InvalidOperationException("Official UI mesh shader is unavailable: " + source.ShaderPathId);
            var material = new Material(shader) { name = source.Name };
            foreach (KeyValuePair<string, float> value in source.Floats)
                if (material.HasProperty(value.Key)) material.SetFloat(value.Key, value.Value);
            foreach (KeyValuePair<string, Color> value in source.Vectors)
                if (material.HasProperty(value.Key))
                    material.SetVector(value.Key, new Vector4(value.Value.r, value.Value.g,
                        value.Value.b, value.Value.a));
            foreach (KeyValuePair<string, OpeningUiTextureData> entry in source.Textures)
            {
                Texture2D texture = GetMaterialTexture(entry.Value);
                material.SetTexture(entry.Key, texture);
                material.SetTextureScale(entry.Key, entry.Value.Scale);
                material.SetTextureOffset(entry.Key, entry.Value.Offset);
            }
            if (source.RenderQueue >= 0) material.renderQueue = source.RenderQueue;
            meshMaterials.Add(material);
            meshMaterialsByKey[key] = material;
            return material;
        }

        private Texture2D GetMaterialTexture(OpeningUiTextureData source)
        {
            string key = source.File + ":" + source.FilterMode + ":" + source.AnisoLevel + ":"
                + source.WrapU + ":" + source.WrapV + ":" + source.WrapW;
            if (materialTexturesByKey.TryGetValue(key, out Texture2D existing)) return existing;
            string path = Path.Combine(data.DirectoryPath, source.File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Official UI material texture is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Official UI material texture could not be decoded: " + source.File);
            texture.name = Path.GetFileNameWithoutExtension(source.File);
            texture.filterMode = (FilterMode)source.FilterMode;
            texture.anisoLevel = source.AnisoLevel;
            texture.wrapModeU = (TextureWrapMode)source.WrapU;
            texture.wrapModeV = (TextureWrapMode)source.WrapV;
            texture.wrapModeW = (TextureWrapMode)source.WrapW;
            materialTextures.Add(texture);
            materialTexturesByKey[key] = texture;
            return texture;
        }

        private bool TryGetSprite(string name, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(name) || !data.Sprites.TryGetValue(name, out OpeningUiSprite entry)) return false;
            if (sprites.TryGetValue(name, out sprite)) return true;
            if (entry.Atlas < 0 || entry.Atlas >= atlases.Count) return false;
            var rect = new Rect(entry.X, entry.Y, entry.Width, entry.Height);
            sprite = Sprite.Create(atlases[entry.Atlas], rect, entry.Pivot, entry.PixelsPerUnit, 0,
                SpriteMeshType.FullRect, entry.Border);
            sprite.name = name;
            sprites[name] = sprite;
            return true;
        }

        public void Dispose()
        {
            foreach (Sprite sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            sprites.Clear();
            foreach (Mesh mesh in meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            meshes.Clear();
            meshesByPath.Clear();
            foreach (Material material in meshMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            meshMaterials.Clear();
            meshMaterialsByKey.Clear();
            foreach (Texture2D texture in materialTextures)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            materialTextures.Clear();
            materialTexturesByKey.Clear();
            foreach (Texture2D atlas in atlases) if (atlas != null) UnityEngine.Object.Destroy(atlas);
            atlases.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
