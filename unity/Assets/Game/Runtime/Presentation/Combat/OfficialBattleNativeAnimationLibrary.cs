using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleNativeModelCatalogData
    {
        public string manifest;
        public OfficialBattleNativeModelRecord[] models;
    }

    [Serializable]
    public sealed class OfficialBattleNativeModelRecord
    {
        public string key;
        public int monsterId;
        public float scale;
        public OfficialBattleNativeAnimationRecord[] animations;
    }

    [Serializable]
    public sealed class OfficialBattleNativeAnimationRecord
    {
        public string name;
        public string entry;
    }

    [Serializable]
    public sealed class OfficialBattleNativeAnimationData
    {
        public string animation;
        public float duration;
        public float fps;
        public float scale;
        public OfficialBattleNativeFrameData[] frames;
    }

    [Serializable]
    public sealed class OfficialBattleNativeFrameData
    {
        public OfficialBattleNativeDrawData[] batches;
    }

    [Serializable]
    public sealed class OfficialBattleNativeDrawData
    {
        public int order;
        public string slot;
        public string page;
        public float[] vertices;
        public float[] uv;
        public int[] triangles;
        public float[] color;
        public int blend;
    }

    public sealed class OfficialBattleNativeAnimationLibrary : IDisposable
    {
        private const string ArchiveRelativePath = "Combat/native-battle-meshes.zip";
        private const string CatalogEntry = "catalog.json";
        private const string ShaderResourcePath = "Combat/OfficialBattleNativeMesh";

        private readonly FileStream archiveStream;
        private readonly ZipArchive archive;
        private readonly Dictionary<string, OfficialBattleNativeModelRecord> models =
            new Dictionary<string, OfficialBattleNativeModelRecord>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> monsterKeys = new Dictionary<int, string>();
        private readonly Dictionary<string, OfficialBattleNativeAnimationData> animations =
            new Dictionary<string, OfficialBattleNativeAnimationData>(StringComparer.Ordinal);
        private readonly LinkedList<string> animationLru = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> animationLruNodes =
            new Dictionary<string, LinkedListNode<string>>(StringComparer.Ordinal);
        private long cachedAnimationBytes;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Shader shader;
        private bool disposed;

        private OfficialBattleNativeAnimationLibrary(string archivePath)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Official native battle mesh pack is missing.", archivePath);
            archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, false);
                ZipArchiveEntry entry = archive.GetEntry(CatalogEntry);
                if (entry == null)
                    throw new InvalidDataException("Official native battle pack has no catalog entry.");
                using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                {
                    OfficialBattleNativeModelCatalogData catalog =
                        JsonUtility.FromJson<OfficialBattleNativeModelCatalogData>(reader.ReadToEnd());
                    if (catalog == null || catalog.manifest != "854_156383" || catalog.models == null
                        || catalog.models.Length == 0)
                        throw new InvalidDataException("Official native battle model catalog is malformed.");
                    BuildIndex(catalog.models);
                }
                shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                    throw new InvalidOperationException("Native battle mesh shader is unavailable: " + ShaderResourcePath);
            }
            catch
            {
                archive?.Dispose();
                archiveStream.Dispose();
                throw;
            }
        }

        public int ModelCount => models.Count;
        public int AnimationCount => animations.Count;

        public static OfficialBattleNativeAnimationLibrary LoadDefault()
        {
            string path = Path.Combine(Application.streamingAssetsPath, ArchiveRelativePath);
            return new OfficialBattleNativeAnimationLibrary(path);
        }

        public string ResolveModelKey(FightBattleUnitState unit)
        {
            if (unit == null || unit.MonsterId <= 0) return null;
            return monsterKeys.TryGetValue(unit.MonsterId, out string key) ? key : null;
        }

        public OfficialBattleNativeModelRecord FindModel(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return models.TryGetValue(key, out OfficialBattleNativeModelRecord model) ? model : null;
        }

        public bool HasAnimation(string modelKey, string animation)
        {
            OfficialBattleNativeModelRecord model = FindModel(modelKey);
            if (model == null || model.animations == null || string.IsNullOrEmpty(animation)) return false;
            return ResolveAnimation(model, animation) != null;
        }

        internal OfficialBattleNativeAnimationData LoadAnimation(string modelKey, string animation)
        {
            ThrowIfDisposed();
            OfficialBattleNativeModelRecord model = FindModel(modelKey);
            if (model == null || model.animations == null)
                throw new KeyNotFoundException("Official native battle model is not bound: " + modelKey);
            OfficialBattleNativeAnimationRecord selected = ResolveAnimation(model, animation);
            if (selected == null || string.IsNullOrEmpty(selected.entry))
                throw new KeyNotFoundException("Official native battle animation is not bound: " + modelKey + "/" + animation);
            if (animations.TryGetValue(selected.entry, out OfficialBattleNativeAnimationData cached))
            {
                TouchAnimation(selected.entry);
                return cached;
            }
            ZipArchiveEntry entry = archive.GetEntry(selected.entry);
            if (entry == null) throw new InvalidDataException("Native battle animation data is missing: " + selected.entry);
            OfficialBattleNativeAnimationData value;
            using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                value = JsonUtility.FromJson<OfficialBattleNativeAnimationData>(reader.ReadToEnd());
            ValidateAnimation(value, modelKey, animation);
            if (entry.Length <= 32L * 1024L * 1024L)
            {
                animations.Add(selected.entry, value);
                animationLruNodes.Add(selected.entry, animationLru.AddLast(selected.entry));
                cachedAnimationBytes += entry.Length;
                TrimAnimationCache();
            }
            return value;
        }

        internal Texture2D LoadTexture(string page)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(page)) throw new InvalidDataException("Native battle draw has no atlas page.");
            if (textures.TryGetValue(page, out Texture2D cached)) return cached;
            ZipArchiveEntry entry = archive.GetEntry("textures/" + page);
            if (entry == null) throw new InvalidDataException("Native battle atlas page is missing: " + page);
            byte[] bytes;
            using (Stream source = entry.Open())
            using (var buffer = new MemoryStream())
            {
                source.CopyTo(buffer);
                bytes = buffer.ToArray();
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = "Official Battle " + page,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!texture.LoadImage(bytes, true))
            {
                OfficialBattleSceneBindings.DestroyCreatedObject(texture);
                throw new InvalidDataException("Native battle atlas page is not a readable PNG: " + page);
            }
            textures.Add(page, texture);
            return texture;
        }

        internal Material LoadMaterial(string page, int blend)
        {
            ThrowIfDisposed();
            string key = page + "\n" + blend.ToString();
            if (materials.TryGetValue(key, out Material cached)) return cached;
            var material = new Material(shader)
            {
                name = "Official Battle " + page + " Blend " + blend,
                mainTexture = LoadTexture(page),
                renderQueue = 3000
            };
            switch (blend)
            {
                case 0:
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case 1:
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    break;
                case 2:
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case 3:
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcColor);
                    break;
                default:
                    UnityEngine.Object.Destroy(material);
                    throw new InvalidDataException("Native battle draw has an unknown blend mode: " + blend);
            }
            materials.Add(key, material);
            return material;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Material material in materials.Values)
                OfficialBattleSceneBindings.DestroyCreatedObject(material);
            foreach (Texture2D texture in textures.Values)
                OfficialBattleSceneBindings.DestroyCreatedObject(texture);
            materials.Clear();
            textures.Clear();
            animations.Clear();
            animationLru.Clear();
            animationLruNodes.Clear();
            cachedAnimationBytes = 0;
            models.Clear();
            monsterKeys.Clear();
            archive.Dispose();
            archiveStream.Dispose();
        }

        private void BuildIndex(OfficialBattleNativeModelRecord[] records)
        {
            foreach (OfficialBattleNativeModelRecord model in records)
            {
                if (model == null || string.IsNullOrEmpty(model.key) || model.scale <= 0f
                    || model.animations == null || model.animations.Length == 0 || models.ContainsKey(model.key))
                    throw new InvalidDataException("Official native battle model binding is malformed.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (OfficialBattleNativeAnimationRecord animation in model.animations)
                    if (animation == null || string.IsNullOrEmpty(animation.name) || string.IsNullOrEmpty(animation.entry)
                        || !names.Add(animation.name))
                        throw new InvalidDataException("Official native battle animation binding is invalid: " + model.key);
                models.Add(model.key, model);
                if (model.monsterId > 0)
                {
                    if (monsterKeys.ContainsKey(model.monsterId))
                        throw new InvalidDataException("Official native battle monster model ID is duplicated: " + model.monsterId);
                    monsterKeys.Add(model.monsterId, model.key);
                }
            }
        }

        private static OfficialBattleNativeAnimationRecord ResolveAnimation(
            OfficialBattleNativeModelRecord model, string requested)
        {
            if (model == null || model.animations == null || string.IsNullOrEmpty(requested)) return null;
            foreach (OfficialBattleNativeAnimationRecord binding in model.animations)
                if (binding != null && string.Equals(binding.name, requested, StringComparison.Ordinal)) return binding;

            string suffix = "/" + requested;
            OfficialBattleNativeAnimationRecord match = null;
            foreach (OfficialBattleNativeAnimationRecord binding in model.animations)
            {
                if (binding == null || !binding.name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if (match != null) return null;
                match = binding;
            }
            return match;
        }

        private void TouchAnimation(string key)
        {
            if (!animationLruNodes.TryGetValue(key, out LinkedListNode<string> node)) return;
            animationLru.Remove(node);
            animationLruNodes[key] = animationLru.AddLast(key);
        }

        private void TrimAnimationCache()
        {
            const long cacheLimitBytes = 32L * 1024L * 1024L;
            while (cachedAnimationBytes > cacheLimitBytes && animationLru.First != null)
            {
                string key = animationLru.First.Value;
                animationLru.RemoveFirst();
                animationLruNodes.Remove(key);
                ZipArchiveEntry entry = archive.GetEntry(key);
                cachedAnimationBytes -= entry == null ? 0 : entry.Length;
                animations.Remove(key);
            }
        }

        private static void ValidateAnimation(OfficialBattleNativeAnimationData value, string modelKey, string animation)
        {
            if (value == null || value.frames == null || value.frames.Length == 0 || value.fps <= 0f
                || value.duration < 0f || value.scale <= 0f || value.animation != animation)
                throw new InvalidDataException("Official native battle animation is malformed: " + modelKey + "/" + animation);
            foreach (OfficialBattleNativeFrameData frame in value.frames)
            {
                if (frame == null || frame.batches == null)
                    throw new InvalidDataException("Official native battle animation frame is empty: " + modelKey + "/" + animation);
                foreach (OfficialBattleNativeDrawData draw in frame.batches)
                {
                    if (draw == null || string.IsNullOrEmpty(draw.page) || draw.vertices == null
                        || draw.vertices.Length == 0 || draw.vertices.Length % 2 != 0 || draw.uv == null
                        || draw.uv.Length != draw.vertices.Length || draw.triangles == null
                        || draw.triangles.Length == 0 || draw.triangles.Length % 3 != 0
                        || draw.color == null || draw.color.Length != 4 || draw.blend < 0 || draw.blend > 3)
                        throw new InvalidDataException("Official native battle draw is malformed: " + modelKey + "/" + animation);
                    int vertexCount = draw.vertices.Length / 2;
                    foreach (int index in draw.triangles)
                        if (index < 0 || index >= vertexCount)
                            throw new InvalidDataException("Official native battle draw index is outside its mesh: " + modelKey + "/" + animation);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleNativeAnimationLibrary));
        }
    }
}
