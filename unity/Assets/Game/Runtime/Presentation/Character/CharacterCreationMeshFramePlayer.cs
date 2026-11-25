using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    // Independent native Mesh playback of the pinned, already converted official frames.
    public sealed class CharacterCreationMeshFramePlayer : MonoBehaviour
    {
        [Serializable] private sealed class Pose { public float fps, duration; public Part[] parts; }
        [Serializable] private sealed class Part
        {
            public string texture, skin;
            public int skin_type, order;
            public float[] vertices, uv, color;
            public int[] triangles;
            public Frame[] frames;
        }
        [Serializable] private sealed class Frame { public float[] vertices; public int order; public bool has_order; }
        [Serializable] private sealed class Textures { public TextureEntry[] textures; }
        [Serializable] private sealed class TextureEntry { public string name, texture, mask; }
        [Serializable] private sealed class PoseIndex { public float fps, duration; public SkinEntry[] skins; }
        [Serializable] private sealed class SkinEntry { public string name; public string[] entries; }
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string name, sha256; }
        private sealed class Instance
        {
            public GameObject Node;
            public Mesh Mesh;
            public Vector3[][] Frames;
            public MeshRenderer Renderer;
            public CharacterCreationDyeMapBinding DyeBinding;
            public Frame[] SourceFrames;
            public int InitialOrder;
            public bool Frozen;
        }
        private readonly Dictionary<string, Dictionary<string, SkinEntry>> poses =
            new Dictionary<string, Dictionary<string, SkinEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoseIndex> poseTimings = new Dictionary<string, PoseIndex>(StringComparer.Ordinal);
        private readonly Dictionary<string, TextureEntry> textureEntries = new Dictionary<string, TextureEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> masks = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Instance> instances = new List<Instance>();
        private readonly Dictionary<string, List<Instance>> skinInstances =
            new Dictionary<string, List<Instance>>(StringComparer.Ordinal);
        private MaterialPropertyBlock dyeProperties;
        private HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> availableSkinNames = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, string> hashes;
        private ZipArchive archive;
        private Shader nativeShader;
        private IReadOnlyDictionary<int, Vector4[]> dye;
        private float elapsed;
        private string currentPose;
        private float poseDuration;
        private float poseFps;
        private string oneShotReturnPose;
        private bool oneShot;
        public int CurrentFrame { get; private set; }
        public int VisiblePartCount => instances.Count(value => value.Node.activeSelf);

        public void Initialize(string sex)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "CharacterCreation/creation-meshes.zip");
            archive = ZipFile.OpenRead(path);
            try
            {
                Manifest manifest;
                using (var reader = new StreamReader(archive.GetEntry("manifest.json").Open()))
                    manifest = JsonUtility.FromJson<Manifest>(reader.ReadToEnd());
                if (manifest == null || manifest.entries == null)
                    throw new InvalidDataException("Character creation mesh package manifest is invalid.");
                hashes = manifest.entries.ToDictionary(entry => entry.name, entry => entry.sha256, StringComparer.Ordinal);
                nativeShader = Zjcs.Offline.CharacterCreation.CharacterCreationAssetCatalog.LoadDefault().NativeCreationShader;
                foreach (string name in new[] { "home/idle_01", "map/run_01", "map/portal_01" })
                {
                    string indexName = sex + "-" + name.Replace('/', '-') + "-index.json";
                    PoseIndex index = JsonUtility.FromJson<PoseIndex>(Read(indexName));
                    if (index == null || index.skins == null || index.fps <= 0 || index.duration <= 0)
                        throw new InvalidDataException("Character creation pose index is invalid: " + name);
                    poseTimings.Add(name, index);
                    poses.Add(name, index.skins.ToDictionary(entry => entry.name, entry => entry, StringComparer.Ordinal));
                }
                foreach (TextureEntry entry in JsonUtility.FromJson<Textures>(Read("textures.json")).textures)
                    textureEntries.Add(entry.name, entry);
            }
            catch
            {
                archive.Dispose();
                archive = null;
                throw;
            }
            Play("home/idle_01");
        }

        public void Play(string name)
        {
            if (currentPose == null || !string.Equals(currentPose, name, StringComparison.Ordinal))
            {
                RebuildPose(name);
                return;
            }

            ResetPlayback();
            foreach (Instance value in instances)
            {
                ApplyFrame(value, 0);
                value.Renderer.sortingOrder = value.InitialOrder;
            }
        }

        private void RebuildPose(string name)
        {
            if (!poses.TryGetValue(name, out Dictionary<string, SkinEntry> skinEntries))
                throw new InvalidOperationException("Missing official pose: " + name);
            RemoveParts(instances);
            instances.Clear();
            skinInstances.Clear();
            currentPose = name;
            PoseIndex timing = poseTimings[name];
            poseFps = timing.fps;
            poseDuration = timing.duration;
            availableSkinNames = new HashSet<string>(skinEntries.Keys, StringComparer.Ordinal);
            ResetPlayback();
            foreach (string skin in selected.OrderBy(value => value, StringComparer.Ordinal))
            {
                List<Instance> parts = BuildSkin(skin, skinEntries);
                skinInstances.Add(skin, parts);
                instances.AddRange(parts);
            }
        }

        private List<Instance> BuildSkin(string skin, Dictionary<string, SkinEntry> skinEntries)
        {
            bool freeze = !skinEntries.TryGetValue(skin, out SkinEntry skinEntry);
            if (freeze && !poses["home/idle_01"].TryGetValue(skin, out skinEntry))
                throw new InvalidOperationException("Official character pose has no skin attachment: " + skin);
            var parts = new List<Instance>();
            try
            {
                foreach (string entry in skinEntry.entries)
                {
                    Pose skinPose = JsonUtility.FromJson<Pose>(Read(entry));
                    if (skinPose == null || skinPose.parts == null
                        || Mathf.Abs(skinPose.fps - poseFps) > .001f
                        || Mathf.Abs(skinPose.duration - poseDuration) > .001f)
                        throw new InvalidDataException("Character skin frame data is invalid: " + entry);
                    foreach (Part part in skinPose.parts)
                    {
                        if (part.triangles.Length == 0) continue;
                        Instance instance = CreatePart(part, freeze);
                        parts.Add(instance);
                        if (CurrentFrame != 0) ApplyFrame(instance, CurrentFrame);
                        ApplyPartDye(instance);
                    }
                }
                return parts;
            }
            catch
            {
                RemoveParts(parts);
                throw;
            }
        }

        private void RemoveParts(List<Instance> parts)
        {
            foreach (Instance part in parts)
            {
                part.Node.SetActive(false);
                Destroy(part.Node);
                Destroy(part.Mesh);
            }
        }

        private void ResetPlayback()
        {
            oneShot = false;
            oneShotReturnPose = null;
            elapsed = 0;
            CurrentFrame = 0;
        }

        public void PlayOnce(string name, string returnPose)
        {
            if (string.IsNullOrEmpty(returnPose) || !poses.ContainsKey(returnPose))
                throw new InvalidOperationException("Official return pose is unavailable: " + returnPose);
            Play(name);
            oneShotReturnPose = returnPose;
            oneShot = true;
        }

        private Instance CreatePart(Part part, bool frozen)
        {
            if (!materials.TryGetValue(part.texture, out Material material))
            {
                Texture2D texture = LoadTexture(part.texture);
                material = new Material(nativeShader);
                material.mainTexture = texture;
                material.SetFloat("_StraightAlphaInput", 1f);
                materials.Add(part.texture, material);
            }
            var node = new GameObject("__skin__" + part.skin, typeof(MeshFilter), typeof(MeshRenderer));
            node.transform.SetParent(transform, false);
            node.layer = gameObject.layer;
            var mesh = new Mesh { name = part.skin + "_" + part.order };
            mesh.MarkDynamic();
            Vector3[] Points(float[] values)
            {
                var points = new Vector3[values.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector3(values[i*2] * .01f, -values[i*2+1] * .01f, 0);
                return points;
            }
            Vector3[][] frames = part.frames.Select(frame => Points(frame.vertices)).ToArray();
            mesh.vertices = frames.Length > 0 && frames[0].Length > 0 ? frames[0] : Points(part.vertices);
            var uv = new Vector2[part.uv.Length / 2];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(part.uv[i*2], 1f - part.uv[i*2+1]);
            mesh.uv = uv;
            Color color = part.color.Length == 4 ? new Color(part.color[0], part.color[1], part.color[2], part.color[3]) : Color.white;
            mesh.colors = Enumerable.Repeat(color, uv.Length).ToArray();
            mesh.triangles = part.triangles;
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = part.order;
            renderer.enabled = frames.Length == 0 || frames[0].Length > 0;
            var binding = node.AddComponent<CharacterCreationDyeMapBinding>();
            binding.Bind(part.skin_type,
                masks.TryGetValue(part.texture, out Texture2D mask) ? mask : null);
            return new Instance
            {
                Node = node, Mesh = mesh, Frames = frames, SourceFrames = part.frames,
                Renderer = renderer, InitialOrder = part.order, DyeBinding = binding, Frozen = frozen
            };
        }

        private Texture2D LoadTexture(string name)
        {
            if (!textureEntries.TryGetValue(name, out TextureEntry entry))
                throw new InvalidDataException("Character skin texture is missing: " + name);
            if (!textures.TryGetValue(name, out Texture2D texture))
            {
                texture = LoadTextureFile(entry.texture);
                if (!string.IsNullOrEmpty(entry.mask)) masks.Add(name, LoadTextureFile(entry.mask));
                textures.Add(name, texture);
            }
            return texture;
        }

        private Texture2D LoadTextureFile(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, ReadBytes(path), true)) throw new InvalidDataException(path);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private string Read(string name)
        {
            return System.Text.Encoding.UTF8.GetString(ReadBytes(name));
        }

        private byte[] ReadBytes(string name)
        {
            ZipArchiveEntry entry = archive.GetEntry(name);
            if (entry == null) throw new FileNotFoundException("Character mesh archive entry is missing.", name);
            using (Stream input = entry.Open())
            using (var buffer = new MemoryStream())
            using (var sha = SHA256.Create())
            {
                input.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (!hashes.TryGetValue(name, out string expected) || expected != hash)
                    throw new InvalidDataException("Character creation mesh hash differs: " + name);
                return bytes;
            }
        }

        public void ApplySkins(IEnumerable<string> names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            var next = new HashSet<string>(names, StringComparer.Ordinal);
            if (selected.SetEquals(next)) return;
            string missing = next.FirstOrDefault(name => !availableSkinNames.Contains(name));
            if (missing != null) throw new InvalidOperationException("Missing official character mesh skin: " + missing);

            var additions = new Dictionary<string, List<Instance>>(StringComparer.Ordinal);
            try
            {
                foreach (string skin in next.OrderBy(value => value, StringComparer.Ordinal))
                    if (!skinInstances.ContainsKey(skin)) additions.Add(skin, BuildSkin(skin, poses[currentPose]));
            }
            catch
            {
                foreach (List<Instance> parts in additions.Values) RemoveParts(parts);
                throw;
            }
            foreach (string skin in selected)
            {
                if (next.Contains(skin)) continue;
                RemoveParts(skinInstances[skin]);
                skinInstances.Remove(skin);
            }
            foreach (var addition in additions) skinInstances.Add(addition.Key, addition.Value);
            selected = next;
            instances.Clear();
            foreach (string skin in selected.OrderBy(value => value, StringComparer.Ordinal))
                instances.AddRange(skinInstances[skin]);
            for (int index = 0; index < instances.Count; index++)
                instances[index].Node.transform.SetSiblingIndex(index);
        }

        public void ApplyDye(IReadOnlyDictionary<int, Vector4[]> changes)
        {
            dye = changes;
            foreach (Instance value in instances) ApplyPartDye(value);
        }

        private void ApplyPartDye(Instance instance)
        {
            dyeProperties ??= new MaterialPropertyBlock();
            CharacterCreationDyeMapBinding binding = instance.DyeBinding;
            Vector4[] channels = dye != null && dye.TryGetValue(binding.SkinType, out Vector4[] values) ? values : null;
            dyeProperties.SetTexture("_DyeTex", binding.DyeMask != null ? binding.DyeMask : Texture2D.blackTexture);
            dyeProperties.SetVector("_DyeHsv1", channels != null && channels.Length > 0 ? channels[0] : Vector4.zero);
            dyeProperties.SetVector("_DyeHsv2", channels != null && channels.Length > 1 ? channels[1] : Vector4.zero);
            dyeProperties.SetVector("_DyeHsv3", channels != null && channels.Length > 2 ? channels[2] : Vector4.zero);
            instance.Renderer.SetPropertyBlock(dyeProperties);
        }

        private void Update()
        {
            if (currentPose == null) return;
            elapsed += Time.unscaledDeltaTime;
            if (oneShot && elapsed >= poseDuration)
            {
                string returnPose = oneShotReturnPose;
                oneShot = false;
                Play(returnPose);
                return;
            }
            int frame = Mathf.FloorToInt((elapsed % Mathf.Max(.01f, poseDuration)) * poseFps);
            if (frame == CurrentFrame) return;
            CurrentFrame = frame;
            foreach (Instance value in instances)
                ApplyFrame(value, frame);
        }

        private static void ApplyFrame(Instance value, int frame)
        {
            if (!value.Node.activeSelf || value.Frozen || value.Frames.Length == 0) return;
            int frameIndex = frame % value.Frames.Length;
            Vector3[] points = value.Frames[frameIndex];
            value.Renderer.enabled = points.Length > 0;
            if (value.SourceFrames[frameIndex].has_order)
                value.Renderer.sortingOrder = value.SourceFrames[frameIndex].order;
            if (points.Length == 0) return; // Official attachment visibility, including eye blinks.
            value.Mesh.vertices = points;
            value.Mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            foreach (Instance value in instances) Destroy(value.Mesh);
            foreach (Material value in materials.Values) Destroy(value);
            foreach (Texture2D value in textures.Values) Destroy(value);
            foreach (Texture2D value in masks.Values) Destroy(value);
            archive?.Dispose();
        }
    }
}
