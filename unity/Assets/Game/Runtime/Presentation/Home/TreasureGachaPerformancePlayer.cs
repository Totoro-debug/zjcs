using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class TreasureGachaPerformanceGeometry
    {
        public float[] vertices;
        public int[] triangles;
    }

    [Serializable]
    public sealed class TreasureGachaPerformanceUvSet
    {
        public float[] values;
    }

    [Serializable]
    public sealed class TreasureGachaPerformancePart
    {
        public int mesh;
        public int uv;
        public float[] color;
        public int blend;
    }

    [Serializable]
    public sealed class TreasureGachaPerformanceFrame
    {
        public TreasureGachaPerformancePart[] parts;
    }

    [Serializable]
    public sealed class TreasureGachaPerformanceAnimation
    {
        public string key;
        public string name;
        public string texture;
        public float duration;
        public float fps;
        public float scale;
        public TreasureGachaPerformanceFrame[] frames;
    }

    [Serializable]
    public sealed class TreasureGachaPerformanceTransform
    {
        public float[] anchor_min;
        public float[] anchor_max;
        public float[] anchored_position;
        public float[] size_delta;
        public float[] pivot;
        public float[] scale;
        public float rotation;
    }

    [Serializable]
    public sealed class TreasureGachaPerformanceData
    {
        public string version;
        public TreasureGachaPerformanceTransform capsule_transform;
        public TreasureGachaPerformanceGeometry[] geometry;
        public TreasureGachaPerformanceUvSet[] uvs;
        public TreasureGachaPerformanceAnimation[] animations;
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TreasureGachaMeshGraphic : MaskableGraphic
    {
        private TreasureGachaPerformanceData data;
        private TreasureGachaPerformanceAnimation animationData;
        private Texture2D atlas;
        private Action completed;
        private float elapsed;
        private int currentFrame = -1;
        private bool playing;
        private bool loop;

        public override Texture mainTexture => atlas != null ? atlas : s_WhiteTexture;

        public void Play(TreasureGachaPerformanceData source,
            TreasureGachaPerformanceAnimation animation, Texture2D texture, bool repeat,
            Action onCompleted = null)
        {
            if (source == null || animation == null || texture == null)
                throw new ArgumentNullException("Treasure gacha mesh animation binding is incomplete.");
            data = source;
            animationData = animation;
            atlas = texture;
            loop = repeat;
            completed = onCompleted;
            elapsed = 0f;
            playing = true;
            SetFrame(0);
            SetMaterialDirty();
        }

        private void Update()
        {
            if (!playing || animationData == null || animationData.frames.Length == 0) return;
            elapsed += Time.deltaTime;
            float duration = Mathf.Max(0.0001f, animationData.duration);
            if (loop && elapsed >= duration) elapsed %= duration;
            bool finished = !loop && elapsed >= duration;
            float position = finished ? 1f : Mathf.Clamp01(elapsed / duration);
            int frame = Mathf.Min(animationData.frames.Length - 1,
                Mathf.FloorToInt(position * animationData.frames.Length));
            SetFrame(frame);
            if (!finished) return;
            playing = false;
            Action action = completed;
            completed = null;
            action?.Invoke();
        }

        private void SetFrame(int index)
        {
            if (currentFrame == index) return;
            currentFrame = index;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (data == null || animationData == null || currentFrame < 0
                || currentFrame >= animationData.frames.Length) return;

            foreach (TreasureGachaPerformancePart part in animationData.frames[currentFrame].parts)
            {
                TreasureGachaPerformanceGeometry mesh = data.geometry[part.mesh];
                float[] uvValues = data.uvs[part.uv].values;
                Color tint = part.color == null || part.color.Length < 4
                    ? Color.white : new Color(part.color[0], part.color[1], part.color[2], part.color[3]);
                int vertexStart = vertexHelper.currentVertCount;
                UIVertex vertex = UIVertex.simpleVert;
                for (int i = 0; i < mesh.vertices.Length / 2; i++)
                {
                    vertex.position = new Vector3(mesh.vertices[i * 2], mesh.vertices[i * 2 + 1], 0f);
                    vertex.uv0 = new Vector2(uvValues[i * 2], uvValues[i * 2 + 1]);
                    vertex.color = (Color32)(color * tint);
                    vertexHelper.AddVert(vertex);
                }
                for (int i = 0; i + 2 < mesh.triangles.Length; i += 3)
                    vertexHelper.AddTriangle(vertexStart + mesh.triangles[i],
                        vertexStart + mesh.triangles[i + 1], vertexStart + mesh.triangles[i + 2]);
            }
        }
    }

    public sealed class TreasureGachaPerformancePlayer : MonoBehaviour
    {
        private const string SkeletonPrefix = "CAB-f8b8d4723f8c600b96a5c17edc5cc61c:";
        private const string CapsuleSkeleton = SkeletonPrefix + "-3592130628675380995";
        private const string BodyBackSkeleton = SkeletonPrefix + "-2360317434235585549:loop";
        private const string BodyFrontSkeleton = SkeletonPrefix + "-1126455669465147305:loop";
        private const string MachinePath = "/ChestGachaPanel/BgPanel1/Content/Mask/GachaponMachine/";
        private readonly Dictionary<string, TreasureGachaPerformanceAnimation> animations =
            new Dictionary<string, TreasureGachaPerformanceAnimation>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private TreasureGachaPerformanceData data;
        private TreasureGachaRuntimeCatalog catalog;
        private HomeUiRenderer uiRenderer;
        private TreasureGachaMeshGraphic[] capsules;
        private int[] displayQualities;
        private int selectedChestId;
        private int selectedGachaType;
        private int appearanceVersion;

        public void Initialize(HomeUiRenderer renderer, TreasureGachaRuntimeCatalog runtimeCatalog,
            int gachaType, int chestId)
        {
            if (renderer == null || runtimeCatalog == null) throw new ArgumentNullException();
            uiRenderer = renderer;
            catalog = runtimeCatalog;
            LoadPerformanceData();

            AttachBodyGraphic(renderer.Find(MachinePath + "MachineGo/BodySpine2"), BodyBackSkeleton);
            AttachBodyGraphic(renderer.Find(MachinePath + "MachineGo/BodySpine1"), BodyFrontSkeleton);
            CreateCapsuleGraphics(renderer.Find(MachinePath + "MachineGo/GachaponInsideSlot"));
            Configure(chestId, gachaType);
        }

        public void Configure(int chestId, int gachaType)
        {
            if (chestId == selectedChestId && gachaType == selectedGachaType && displayQualities != null) return;
            int[] qualities = catalog.QualityDistribution(chestId);
            int elementId = catalog.ElementIdForGachaType(gachaType);
            if (qualities == null || qualities.Length != 10 || elementId < 0)
                throw new InvalidOperationException("The official treasure gacha capsule configuration is missing.");

            selectedChestId = chestId;
            selectedGachaType = gachaType;
            displayQualities = new int[10];
            int[] order = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var random = new System.Random(unchecked((int)DateTime.Now.Ticks));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                int value = order[i];
                order[i] = order[swap];
                order[swap] = value;
            }
            for (int i = 0; i < qualities.Length; i++)
                displayQualities[order[i]] = qualities[i];

            appearanceVersion++;
            for (int index = 0; index < capsules.Length; index++)
            {
                int position = capsules.Length - index;
                int quality = displayQualities[index];
                string shockKey = MachineKey(quality, elementId, "shock_" + position.ToString("00"));
                capsules[index].Play(data, FindAnimation(shockKey), LoadTexture(shockKey), false);
            }
            uiRenderer.ApplyTreasureGachaMachineScale(false);
        }

        public void PlayDraw()
        {
            if (displayQualities == null) return;
            int elementId = catalog.ElementIdForGachaType(selectedGachaType);
            uiRenderer.ApplyTreasureGachaMachineScale(true);
            int version = ++appearanceVersion;
            int remaining = capsules.Length;
            for (int index = 0; index < capsules.Length; index++)
                PlayCapsule(index, "qiu", false, elementId, () =>
                {
                    if (version != appearanceVersion || --remaining > 0) return;
                    uiRenderer.ApplyTreasureGachaMachineScale(false);
                });
        }

        private void AttachBodyGraphic(GameObject owner, string key)
        {
            if (owner == null) throw new InvalidOperationException("The official treasure gacha machine body node is missing.");
            TreasureGachaMeshGraphic graphic = owner.GetComponent<TreasureGachaMeshGraphic>();
            if (graphic == null) graphic = owner.AddComponent<TreasureGachaMeshGraphic>();
            graphic.raycastTarget = false;
            graphic.Play(data, FindAnimation(key), LoadTexture(key), true);
        }

        private void CreateCapsuleGraphics(GameObject owner)
        {
            if (owner == null) throw new InvalidOperationException("The official treasure gacha capsule slot is missing.");
            capsules = new TreasureGachaMeshGraphic[10];
            for (int index = 0; index < capsules.Length; index++)
            {
                var capsule = new GameObject("OfficialGachapon_" + (index + 1).ToString("00"), typeof(RectTransform));
                RectTransform rect = (RectTransform)capsule.transform;
                rect.SetParent(owner.transform, false);
                rect.anchorMin = Pair(data.capsule_transform.anchor_min, new Vector2(0.5f, 0.5f));
                rect.anchorMax = Pair(data.capsule_transform.anchor_max, new Vector2(0.5f, 0.5f));
                rect.anchoredPosition = Pair(data.capsule_transform.anchored_position, Vector2.zero);
                rect.sizeDelta = Pair(data.capsule_transform.size_delta, new Vector2(711.5112f, 338f));
                rect.pivot = Pair(data.capsule_transform.pivot, new Vector2(0.20379166f, 0.06252816f));
                float[] scale = data.capsule_transform.scale;
                rect.localScale = scale == null || scale.Length < 2
                    ? Vector3.one : new Vector3(scale[0], scale[1], scale.Length > 2 ? scale[2] : 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, data.capsule_transform.rotation);
                TreasureGachaMeshGraphic graphic = capsule.AddComponent<TreasureGachaMeshGraphic>();
                graphic.raycastTarget = false;
                capsules[index] = graphic;
            }
        }

        private void PlayCapsule(int index, string animationName, bool loop, int? elementOverride = null,
            Action completed = null)
        {
            if (displayQualities == null || index < 0 || index >= displayQualities.Length) return;
            int elementId = elementOverride ?? catalog.ElementIdForGachaType(selectedGachaType);
            string key = MachineKey(displayQualities[index], elementId,
                animationName + "_" + (capsules.Length - index).ToString("00"));
            capsules[index].Play(data, FindAnimation(key), LoadTexture(key), loop, completed);
        }

        private static string MachineKey(int quality, int elementId, string animation)
        {
            return CapsuleSkeleton + ":machine:" + quality + ":" + elementId + ":" + animation;
        }

        private TreasureGachaPerformanceAnimation FindAnimation(string key)
        {
            if (!animations.TryGetValue(key, out TreasureGachaPerformanceAnimation animation))
                throw new InvalidOperationException("The official treasure gacha animation is missing: " + key);
            return animation;
        }

        private Texture2D LoadTexture(string animationKey)
        {
            TreasureGachaPerformanceAnimation animation = FindAnimation(animationKey);
            if (textures.TryGetValue(animation.texture, out Texture2D texture)) return texture;
            string path = Path.Combine(Application.streamingAssetsPath, "TreasureGacha",
                "performance-sprites", animation.texture);
            byte[] bytes = File.ReadAllBytes(path);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, bytes, false))
                throw new InvalidDataException("The official treasure gacha atlas could not be decoded: " + animation.texture);
            texture.name = animation.texture;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            textures.Add(animation.texture, texture);
            return texture;
        }

        private void LoadPerformanceData()
        {
            data = LoadDefaultData();
            foreach (TreasureGachaPerformanceAnimation animation in data.animations)
                animations.Add(animation.key, animation);
        }

        public static TreasureGachaPerformanceData LoadDefaultData()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "TreasureGacha", "performance-data.json.gz");
            byte[] compressed = File.ReadAllBytes(path);
            byte[] json;
            using (var input = new MemoryStream(compressed))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                json = output.ToArray();
            }
            TreasureGachaPerformanceData data = JsonUtility.FromJson<TreasureGachaPerformanceData>(Encoding.UTF8.GetString(json));
            if (data == null || data.version != "854_156383" || data.geometry == null || data.geometry.Length != 24170
                || data.uvs == null || data.uvs.Length != 29 || data.animations == null || data.animations.Length != 632)
                throw new FormatException("The official treasure gacha performance package is incomplete.");
            foreach (TreasureGachaPerformanceGeometry mesh in data.geometry)
                if (mesh == null || mesh.vertices == null || mesh.vertices.Length % 2 != 0
                    || mesh.triangles == null || mesh.triangles.Length % 3 != 0)
                    throw new FormatException("The official treasure gacha mesh catalog contains an invalid mesh.");
            foreach (TreasureGachaPerformanceUvSet uv in data.uvs)
                if (uv == null || uv.values == null || uv.values.Length % 2 != 0)
                    throw new FormatException("The official treasure gacha mesh catalog contains invalid UV coordinates.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (TreasureGachaPerformanceAnimation animation in data.animations)
            {
                if (animation == null || string.IsNullOrEmpty(animation.key) || animation.frames == null
                    || animation.frames.Length == 0 || !keys.Add(animation.key)
                    || string.IsNullOrEmpty(animation.texture) || animation.duration <= 0f || animation.fps <= 0f)
                    throw new FormatException("The official treasure gacha animation catalog contains an invalid binding.");
                foreach (TreasureGachaPerformanceFrame frame in animation.frames)
                {
                    if (frame == null || frame.parts == null)
                        throw new FormatException("The official treasure gacha animation has a missing frame.");
                    foreach (TreasureGachaPerformancePart part in frame.parts)
                    {
                        if (part == null || part.mesh < 0 || part.mesh >= data.geometry.Length
                            || part.uv < 0 || part.uv >= data.uvs.Length || part.blend != 0
                            || data.uvs[part.uv].values.Length != data.geometry[part.mesh].vertices.Length)
                            throw new FormatException("The official treasure gacha animation references an invalid mesh part.");
                    }
                }
                string atlasPath = Path.Combine(Application.streamingAssetsPath, "TreasureGacha",
                    "performance-sprites", animation.texture);
                if (!File.Exists(atlasPath))
                    throw new FileNotFoundException("An official treasure gacha performance atlas is missing.", atlasPath);
            }
            return data;
        }

        private static Vector2 Pair(float[] value, Vector2 fallback)
        {
            return value == null || value.Length < 2 ? fallback : new Vector2(value[0], value[1]);
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in textures.Values)
                if (texture != null) Destroy(texture);
            textures.Clear();
        }
    }
}
