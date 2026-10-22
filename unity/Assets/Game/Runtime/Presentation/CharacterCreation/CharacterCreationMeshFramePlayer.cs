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
        [Serializable] private sealed class Frame { public float[] vertices; }
        [Serializable] private sealed class Textures { public TextureEntry[] textures; }
        [Serializable] private sealed class TextureEntry { public string name, texture, mask; }
        [Serializable] private sealed class Manifest { public string manifest; public Entry[] entries; }
        [Serializable] private sealed class Entry { public string name, sha256; }
        private sealed class Instance
        {
            public GameObject Node;
            public Mesh Mesh;
            public Vector3[][] Frames;
            public MeshRenderer Renderer;
        }
        private readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> masks = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Instance> instances = new List<Instance>();
        private HashSet<string> selected = new HashSet<string>();
        private IReadOnlyDictionary<int, Vector4[]> dye;
        private float elapsed;
        private Pose pose;
        public int CurrentFrame { get; private set; }
        public int VisiblePartCount => instances.Count(value => value.Node.activeSelf);

        public void Initialize(string sex)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "CharacterCreation/creation-meshes.zip");
            using (var archive = ZipFile.OpenRead(path))
            {
                Manifest manifest;
                using (var reader = new StreamReader(archive.GetEntry("manifest.json").Open()))
                    manifest = JsonUtility.FromJson<Manifest>(reader.ReadToEnd());
                if (manifest.manifest != "854_156383" || manifest.entries == null)
                    throw new InvalidDataException("Character creation mesh package is not pinned.");
                var hashes = manifest.entries.ToDictionary(entry => entry.name, entry => entry.sha256, StringComparer.Ordinal);
                byte[] ReadBytes(string name)
                {
                    using (Stream input = archive.GetEntry(name).Open())
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
                string Read(string name)
                {
                    return System.Text.Encoding.UTF8.GetString(ReadBytes(name));
                }
                Texture2D Load(string name)
                {
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(texture, ReadBytes(name), true)) throw new InvalidDataException(name);
                        texture.wrapMode = TextureWrapMode.Clamp;
                        return texture;
                }
                foreach (string name in new[] { "home/idle_01", "map/run_01" })
                    poses.Add(name, JsonUtility.FromJson<Pose>(Read(sex + "-" + name.Replace('/', '-') + ".json")));
                foreach (TextureEntry entry in JsonUtility.FromJson<Textures>(Read("textures.json")).textures)
                {
                    textures.Add(entry.name, Load(entry.texture));
                    if (!string.IsNullOrEmpty(entry.mask)) masks.Add(entry.name, Load(entry.mask));
                }
            }
            Play("home/idle_01");
        }

        public void Play(string name)
        {
            if (!poses.TryGetValue(name, out Pose next)) throw new InvalidOperationException("Missing official pose: " + name);
            foreach (Instance value in instances) { value.Node.SetActive(false); Destroy(value.Node); Destroy(value.Mesh); }
            instances.Clear();
            pose = next;
            elapsed = 0;
            CurrentFrame = 0;
            foreach (Part part in pose.parts)
            {
                if (part.triangles.Length == 0) continue;
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
                if (!materials.TryGetValue(part.texture, out Material material))
                {
                    material = new Material(Zjcs.Offline.CharacterCreation.CharacterCreationAssetCatalog.LoadDefault().NativeCreationShader);
                    material.mainTexture = textures[part.texture];
                    material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                    materials.Add(part.texture, material);
                }
                renderer.sharedMaterial = material;
                renderer.sortingOrder = part.order;
                renderer.enabled = frames.Length == 0 || frames[0].Length > 0;
                node.AddComponent<CharacterCreationDyeMapBinding>().Bind(part.skin_type,
                    masks.TryGetValue(part.texture, out Texture2D mask) ? mask : null);
                instances.Add(new Instance { Node = node, Mesh = mesh, Frames = frames, Renderer = renderer });
            }
            ApplySkins(selected);
            ApplyDye(dye);
        }

        public void ApplySkins(IEnumerable<string> names)
        {
            selected = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (Instance value in instances) value.Node.SetActive(selected.Contains(value.Node.name.Substring(8)));
        }

        public void ApplyDye(IReadOnlyDictionary<int, Vector4[]> changes)
        {
            dye = changes;
            var properties = new MaterialPropertyBlock();
            foreach (Instance value in instances)
            {
                var binding = value.Node.GetComponent<CharacterCreationDyeMapBinding>();
                Vector4[] channels = changes != null && changes.TryGetValue(binding.SkinType, out Vector4[] values) ? values : null;
                properties.Clear();
                properties.SetTexture("_DyeTex", binding.DyeMask != null ? binding.DyeMask : Texture2D.blackTexture);
                for (int i = 0; i < 3; i++) properties.SetVector("_DyeHsv" + (i+1), channels != null && channels.Length > i ? channels[i] : Vector4.zero);
                value.Node.GetComponent<Renderer>().SetPropertyBlock(properties);
            }
        }

        private void Update()
        {
            if (pose == null) return;
            elapsed += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt((elapsed % Mathf.Max(.01f, pose.duration)) * pose.fps);
            if (frame == CurrentFrame) return;
            CurrentFrame = frame;
            foreach (Instance value in instances)
            {
                if (!value.Node.activeSelf || value.Frames.Length == 0) continue;
                Vector3[] points = value.Frames[CurrentFrame % value.Frames.Length];
                value.Renderer.enabled = points.Length > 0;
                if (points.Length == 0) continue; // Official attachment visibility, including eye blinks.
                value.Mesh.vertices = points;
                value.Mesh.RecalculateBounds();
            }
        }

        private void OnDestroy()
        {
            foreach (Instance value in instances) Destroy(value.Mesh);
            foreach (Material value in materials.Values) Destroy(value);
            foreach (Texture2D value in textures.Values) Destroy(value);
            foreach (Texture2D value in masks.Values) Destroy(value);
        }
    }
}
