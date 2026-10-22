using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    internal sealed class CharacterModelPackage : IDisposable
    {
        private const string UriPrefix = "res://assets/characters/";
        private readonly FileStream stream;
        private readonly ZipArchive archive;
        private readonly Dictionary<string, ZipArchiveEntry> entries;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> fileHashes;
        private readonly Dictionary<string, object> catalog;

        public Dictionary<string, object> Catalog => catalog;

        private CharacterModelPackage()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Character/character-models.zip");
            if (!File.Exists(path))
                throw new FileNotFoundException("The pinned offline character model package is missing.", path);
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            entries = archive.Entries.ToDictionary(value => value.FullName, StringComparer.Ordinal);
            Dictionary<string, object> manifest = AsObject(OfficialEquipmentJson.Parse(
                Encoding.UTF8.GetString(ReadUnverifiedBytes("character-model-manifest.json"))), "character model manifest");
            if (!manifest.TryGetValue("manifest", out object manifestVersion)
                || Convert.ToString(manifestVersion, CultureInfo.InvariantCulture) != "854_156383"
                || !manifest.TryGetValue("files", out object rawFiles))
                throw new FormatException("Character model package is not pinned to 854_156383.");
            fileHashes = AsObject(rawFiles, "character model package hashes");
            string catalogText = ReadText(UriPrefix + "catalog.json");
            catalog = AsObject(OfficialEquipmentJson.Parse(catalogText), "character model catalog");
        }

        public static CharacterModelPackage OpenDefault() => new CharacterModelPackage();

        public string ReadText(string name)
        {
            return Encoding.UTF8.GetString(ReadBytes(name));
        }

        public byte[] ReadBytes(string name)
        {
            string entryName = name.StartsWith(UriPrefix, StringComparison.Ordinal)
                ? "assets/characters/" + name.Substring(UriPrefix.Length) : name;
            byte[] bytes = ReadUnverifiedBytes(entryName);
            if (!fileHashes.TryGetValue(entryName, out object expected)
                || !string.Equals(Convert.ToString(expected, CultureInfo.InvariantCulture), Hash(bytes),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Character model package entry hash mismatch: " + entryName);
            return bytes;
        }

        private byte[] ReadUnverifiedBytes(string entryName)
        {
            if (!entries.TryGetValue(entryName, out ZipArchiveEntry entry))
                throw new FileNotFoundException("Character model package entry is missing: " + entryName);
            using (Stream input = entry.Open())
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }

        public Texture2D LoadTexture(string name)
        {
            if (textures.TryGetValue(name, out Texture2D texture)) return texture;
            Dictionary<string, object> paths = AsObject(catalog["textures"], "character textures");
            if (!paths.TryGetValue(name, out object rawPath))
                throw new FileNotFoundException("Character mesh texture is not indexed: " + name);
            byte[] bytes = ReadBytes(Convert.ToString(rawPath, CultureInfo.InvariantCulture));
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!UnityEngine.ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Character texture could not be decoded: " + name);
            }
            textures.Add(name, texture);
            return texture;
        }

        public static Dictionary<string, object> AsObject(object value, string label)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(label + " is not a JSON object.");
        }

        public static List<object> AsArray(object value, string label)
        {
            if (value is List<object> result) return result;
            throw new FormatException(label + " is not a JSON array.");
        }

        public static float[] Floats(object value, string label)
        {
            return AsArray(value, label).Select(item => Convert.ToSingle(item, CultureInfo.InvariantCulture)).ToArray();
        }

        public void Dispose()
        {
            archive.Dispose();
            stream.Dispose();
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    [DisallowMultipleComponent]
    public sealed class CharacterModelPreview : MonoBehaviour
    {
        private sealed class ModelMeshPart
        {
            public Texture2D Texture;
            public Vector2[] Uv;
            public int[] Triangles;
            public Color Color;
            public int Order;
            public Vector2[][] Frames;
        }

        private static CharacterModelPackage sharedPackage;
        private OfflineCharacterProgressionService progression;
        private RectTransform modelRect;
        private Vector2 renderedSize;
        private float modelScale;
        private Vector2 modelCenter;
        private readonly List<CharacterModelMeshGraphic> graphics = new List<CharacterModelMeshGraphic>();

        public void Initialize(OfflineCharacterProgressionService characterProgression)
        {
            progression = characterProgression != null ? characterProgression
                : throw new ArgumentNullException(nameof(characterProgression));
            modelRect = transform as RectTransform;
            if (modelRect == null) throw new InvalidOperationException("The character preview must be mounted to a RectTransform.");
            if (sharedPackage == null) sharedPackage = CharacterModelPackage.OpenDefault();
            Refresh();
        }

        public void Refresh()
        {
            if (progression == null || modelRect == null) return;
            foreach (CharacterModelMeshGraphic graphic in graphics)
                if (graphic != null) Destroy(graphic.gameObject);
            graphics.Clear();

            CharacterModelPackage package = sharedPackage ?? (sharedPackage = CharacterModelPackage.OpenDefault());
            Dictionary<string, object> catalog = package.Catalog;
            string sex = progression.Player.Wardrobe.Sex == 1 ? "female" : "male";
            string profession = progression.Player.Wardrobe.Profession.ToString(CultureInfo.InvariantCulture);
            Dictionary<string, object> profiles = CharacterModelPackage.AsObject(catalog["profiles"], "character profiles");
            Dictionary<string, object> sexProfiles = CharacterModelPackage.AsObject(profiles[sex], sex + " profiles");
            if (!sexProfiles.TryGetValue(profession, out object profileRaw))
                throw new InvalidOperationException("The character profession has no official model profile: " + profession);
            Dictionary<string, object> profile = CharacterModelPackage.AsObject(profileRaw, "character model profile");
            string pose = Convert.ToString(profile["pose"], CultureInfo.InvariantCulture);
            Dictionary<string, object> poses = CharacterModelPackage.AsObject(catalog["poses"], "character poses");
            Dictionary<string, object> sexPoses = CharacterModelPackage.AsObject(poses[sex], sex + " poses");
            string poseUri = Convert.ToString(sexPoses[pose], CultureInfo.InvariantCulture);
            Dictionary<string, object> poseData = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(package.ReadText(poseUri)), "character idle pose");
            Dictionary<string, object> poseParts = CharacterModelPackage.AsObject(poseData["parts"], "character pose parts");
            Dictionary<string, object> partIndex = CharacterModelPackage.AsObject(
                CharacterModelPackage.AsObject(catalog["parts"], "character parts")[sex], sex + " parts");
            WardrobeResolution appearance = progression.ResolveAppearance();

            var meshUris = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<int, int> skin in appearance.SkinByType.OrderBy(pair => pair.Key))
            {
                string key;
                if (skin.Key == 7 || skin.Key == 8)
                    key = "profession_weapon_" + skin.Value.ToString(CultureInfo.InvariantCulture);
                else if (partIndex.TryGetValue(skin.Value.ToString(CultureInfo.InvariantCulture), out object partName))
                    key = Convert.ToString(partName, CultureInfo.InvariantCulture);
                else
                    key = "skin_" + skin.Value.ToString(CultureInfo.InvariantCulture);
                if (poseParts.TryGetValue(key, out object uri))
                    meshUris[key] = Convert.ToString(uri, CultureInfo.InvariantCulture);
            }

            var meshParts = new List<ModelMeshPart>();
            foreach (KeyValuePair<string, string> mesh in meshUris)
            {
                byte[] compressed = package.ReadBytes(mesh.Value);
                string json;
                using (var input = new MemoryStream(compressed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    json = Encoding.UTF8.GetString(output.ToArray());
                }
                foreach (object rawPart in CharacterModelPackage.AsArray(
                    OfficialEquipmentJson.Parse(json), "character mesh parts"))
                    meshParts.Add(ParsePart(package, CharacterModelPackage.AsObject(rawPart, "character mesh part")));
            }

            if (meshParts.Count == 0) throw new InvalidOperationException("The selected character has no official idle mesh parts.");
            CalculateBounds(meshParts);
            foreach (ModelMeshPart part in meshParts.OrderBy(value => value.Order))
            {
                var instance = new GameObject("CharacterMesh_" + part.Order, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(CharacterModelMeshGraphic));
                instance.transform.SetParent(transform, false);
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                CharacterModelMeshGraphic graphic = instance.GetComponent<CharacterModelMeshGraphic>();
                graphic.Initialize(part.Texture, part.Uv, part.Triangles, part.Color, part.Frames,
                    modelScale, modelCenter, poseData.TryGetValue("fps", out object fps)
                        ? Convert.ToSingle(fps, CultureInfo.InvariantCulture) : 24f);
                graphic.raycastTarget = false;
                graphics.Add(graphic);
            }
            renderedSize = modelRect.rect.size;
        }

        private void LateUpdate()
        {
            if (progression != null && modelRect != null && modelRect.rect.size != renderedSize)
                Refresh();
        }

        private ModelMeshPart ParsePart(CharacterModelPackage package, Dictionary<string, object> data)
        {
            string textureName = Convert.ToString(data["texture"], CultureInfo.InvariantCulture);
            Vector2[] uv = Pairs(CharacterModelPackage.Floats(data["uv"], "character mesh UVs"));
            int[] triangles = CharacterModelPackage.AsArray(data["triangles"], "character mesh triangles")
                .Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
            Color color = Color.white;
            if (data.TryGetValue("color", out object colorRaw))
            {
                float[] values = CharacterModelPackage.Floats(colorRaw, "character mesh color");
                if (values.Length >= 4) color = new Color(values[0], values[1], values[2], values[3]);
            }
            var frames = new List<Vector2[]>();
            if (data.TryGetValue("idle", out object idleRaw))
            {
                foreach (object rawFrame in CharacterModelPackage.AsArray(idleRaw, "character idle frames"))
                {
                    Dictionary<string, object> frame = CharacterModelPackage.AsObject(rawFrame, "character idle frame");
                    if (frame.TryGetValue("vertices", out object vertices))
                        frames.Add(Pairs(CharacterModelPackage.Floats(vertices, "character frame vertices")));
                }
            }
            if (frames.Count == 0)
                frames.Add(Pairs(CharacterModelPackage.Floats(data["vertices"], "character mesh vertices")));
            return new ModelMeshPart
            {
                Texture = package.LoadTexture(textureName),
                Uv = uv,
                Triangles = triangles,
                Color = color,
                Order = Convert.ToInt32(data["order"], CultureInfo.InvariantCulture),
                Frames = frames.ToArray()
            };
        }

        private void CalculateBounds(IEnumerable<ModelMeshPart> parts)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector2[] frame in parts.SelectMany(part => part.Frames))
            foreach (Vector2 point in frame)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
            Vector2 size = modelRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(207f, 337f);
            float width = Mathf.Max(1f, maxX - minX);
            float height = Mathf.Max(1f, maxY - minY);
            modelScale = Mathf.Min(size.x / width, size.y / height) * 0.96f;
            modelCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        private static Vector2[] Pairs(float[] values)
        {
            if (values.Length % 2 != 0) throw new FormatException("Character mesh data must contain x/y pairs.");
            var result = new Vector2[values.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            return result;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CharacterModelMeshGraphic : MaskableGraphic
    {
        private Texture texture;
        private Vector2[] uv = Array.Empty<Vector2>();
        private int[] triangles = Array.Empty<int>();
        private Color meshColor = Color.white;
        private Vector2[][] frames = Array.Empty<Vector2[]>();
        private float scale = 1f;
        private Vector2 center;
        private float framesPerSecond = 24f;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public void Initialize(Texture source, Vector2[] meshUv, int[] meshTriangles, Color color,
            Vector2[][] animationFrames, float sourceScale, Vector2 sourceCenter, float fps)
        {
            texture = source;
            uv = meshUv ?? Array.Empty<Vector2>();
            triangles = meshTriangles ?? Array.Empty<int>();
            meshColor = color;
            frames = animationFrames ?? Array.Empty<Vector2[]>();
            scale = sourceScale;
            center = sourceCenter;
            framesPerSecond = Mathf.Max(1f, fps);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (frames.Length > 1) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (frames.Length == 0 || uv.Length == 0 || triangles.Length < 3) return;
            Vector2[] positions = frames[Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % frames.Length];
            if (positions.Length != uv.Length) return;
            Color vertexColor = color * meshColor;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 position = positions[i];
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3((position.x - center.x) * scale,
                    (center.y - position.y) * scale, 0f);
                vertex.color = vertexColor;
                vertex.uv0 = uv[i];
                helper.AddVert(vertex);
            }
            for (int i = 0; i + 2 < triangles.Length; i += 3)
                helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
