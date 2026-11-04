using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class OfficialPartnerNativeBattleDataValidation
    {
        private const string Root = "Assets/Official/Combat/NativePartners/";
        [Serializable] private sealed class Catalog { public string manifest; public Fighter[] fighters; }
        [Serializable] private class Renderer
        {
            public int npcId;
            public string package, materials;
            public string[] skins, animationNames;
        }
        [Serializable] private sealed class Fighter : Renderer { public Renderer[] parts; }
        [Serializable] private sealed class Materials { public MaterialRow[] rows; }
        [Serializable] private sealed class MaterialRow { public TextureRow[] runtimeTextures; }
        [Serializable] private sealed class TextureRow
        {
            public string file, rgba_sha256;
            public int width, height, mip_count, color_space, filter_mode, aniso, wrap_u, wrap_v, wrap_w;
            public float mip_bias;
            public bool is_readable;
        }
        [Serializable] private sealed class Probe
        {
            public int npcId;
            public string animation, error;
            public bool supported;
        }
        [Serializable] private sealed class Probes { public Probe[] animations; }

        public static void Validate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Catalog catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Root + "catalog.json"));
            if (catalog?.manifest != "884_156692" || catalog.fighters?.Length != 34)
                throw new InvalidDataException("Full-binary partner battle source identity differs.");
            var results = new List<Probe>();
            Renderer[] renderers = catalog.fighters.Cast<Renderer>().Concat(catalog.fighters.SelectMany(value => value.parts)).ToArray();
            if (renderers.Length != 35) throw new InvalidDataException("Original partner renderer inventory differs.");
            var textures = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Renderer fighter in renderers)
            {
                foreach (MaterialRow material in JsonUtility.FromJson<Materials>("{\"rows\":"
                             + File.ReadAllText(Root + fighter.materials) + "}").rows)
                    foreach (TextureRow texture in material.runtimeTextures)
                    {
                        string settings = JsonUtility.ToJson(texture);
                        if (textures.TryGetValue(texture.file, out string existing))
                        {
                            if (existing != settings)
                                throw new InvalidDataException("Original partner textures share pixels but require distinct import settings.");
                        }
                        else { ImportTexture(texture); textures.Add(texture.file, settings); }
                    }
                byte[] bytes = File.ReadAllBytes(Root + fighter.package);
                ProfessionNativeSkeletonData data = ProfessionNativeSkeletonData.LoadPartnerBattle(bytes, fighter.npcId);
                if (!data.AnimationNames.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                        fighter.animationNames.OrderBy(value => value, StringComparer.Ordinal)))
                    throw new InvalidDataException("Native partner loader changed original animation names: " + fighter.npcId);
                bool rejected = false;
                try { ProfessionNativeSkeletonData.LoadPartnerBattle(bytes, fighter.npcId + 100000); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new InvalidDataException("Native partner loader accepted another NPC identity.");
                var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                skeleton.SetSkin(data.CreateCombinedNpcSkin(fighter.skins));
                using var batches = new ProfessionNativeMeshBatches(data.SlotCount);
                foreach (string name in data.AnimationNames)
                {
                    var result = new Probe { npcId = fighter.npcId, animation = name };
                    try
                    {
                        var channels = new ProfessionNativeAnimationChannels(data, name);
                        foreach (float time in new[] { 0f, channels.Duration * .5f, channels.Duration })
                        {
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, true);
                            foreach (var batch in batches.Batches)
                                foreach (Vector3 vertex in batch.Mesh.vertices)
                                    if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x)
                                        || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y))
                                        throw new InvalidDataException("Native battle animation produced a non-finite vertex.");
                        }
                        result.supported = true;
                    }
                    catch (NotSupportedException error) { result.error = error.Message; }
                    catch (Exception error)
                    {
                        throw new InvalidDataException("Original partner battle probe failed: "
                            + fighter.npcId + "/" + name, error);
                    }
                    results.Add(result);
                }
            }
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output))
                throw new InvalidDataException("Native battle capability probe requires an isolated output directory.");
            File.WriteAllText(Path.Combine(output, "native-partner-animation-probes.json"),
                JsonUtility.ToJson(new Probes { animations = results.ToArray() }, true));
            Debug.Log("Original full-binary partner data loaded: 34 fighters, 35 renderers, " + textures.Count
                + " mapped textures, " + results.Count
                + " original animations; native constraint/mesh probes supported=" + results.Count(value => value.supported)
                + ", unsupported=" + results.Count(value => !value.supported)
                + ". Materials, Animator playback and battle rendering are not validated by this data check.");
        }

        private static void ImportTexture(TextureRow source)
        {
            string path = Root + source.file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidDataException("Original mapped partner texture is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = source.color_space == 1;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = source.mip_count > 1;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Math.Max(source.width, source.height));
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = (FilterMode)source.filter_mode;
            importer.anisoLevel = source.aniso;
            importer.mipMapBias = source.mip_bias;
            importer.wrapModeU = (TextureWrapMode)source.wrap_u;
            importer.wrapModeV = (TextureWrapMode)source.wrap_v;
            importer.wrapModeW = (TextureWrapMode)source.wrap_w;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || texture.width != source.width || texture.height != source.height
                || texture.mipmapCount != source.mip_count || texture.filterMode != (FilterMode)source.filter_mode
                || texture.anisoLevel != source.aniso || texture.mipMapBias != source.mip_bias
                || texture.wrapModeU != (TextureWrapMode)source.wrap_u || texture.wrapModeV != (TextureWrapMode)source.wrap_v
                || texture.wrapModeW != (TextureWrapMode)source.wrap_w
                || UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) != (source.color_space == 1))
                throw new InvalidDataException("Original mapped partner texture import differs: " + path
                    + "; expected=" + JsonUtility.ToJson(source) + "; actual=" + (texture
                        ? string.Join(",", texture.width, texture.height, texture.mipmapCount, texture.filterMode,
                            texture.anisoLevel, texture.mipMapBias, texture.wrapModeU, texture.wrapModeV,
                            texture.wrapModeW, texture.graphicsFormat) : "null"));
            Color32[] pixels = texture.GetPixels32();
            byte[] rgba = new byte[pixels.Length * 4];
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    Color32 color = pixels[(texture.height - 1 - y) * texture.width + x];
                    int index = (y * texture.width + x) * 4;
                    rgba[index] = color.r; rgba[index + 1] = color.g; rgba[index + 2] = color.b; rgba[index + 3] = color.a;
                }
            using var hash = SHA256.Create();
            if (BitConverter.ToString(hash.ComputeHash(rgba)).Replace("-", "").ToLowerInvariant() != source.rgba_sha256)
                throw new InvalidDataException("Original mapped partner texture pixels differ after Unity import: " + path);
            importer.isReadable = source.is_readable;
            importer.SaveAndReimport();
        }
    }
}
