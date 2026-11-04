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
            public int sampledPoses;
        }
        [Serializable] private sealed class Probes { public Probe[] animations; }

        public static void Validate() => ValidateCore(false);
        public static void ValidateOrderedConstraints() => ValidateCore(true);

        private static void ValidateCore(bool ordered)
        {
            if (ordered) ValidateOrderedGeometry();
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
                        var times = new SortedSet<float> { 0, channels.Duration * .5f, channels.Duration };
                        if (ordered) CollectTimes(channels.OriginalFields, times);
                        float[] keys = times.ToArray();
                        if (ordered) for (int i = 1; i < keys.Length; i++) times.Add((keys[i - 1] + keys[i]) * .5f);
                        foreach (float time in times)
                        {
                            if (ordered) ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                            else ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, true, allowConcaveClipping: ordered);
                            foreach (var batch in batches.Batches)
                                foreach (Vector3 vertex in batch.Mesh.vertices)
                                    if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x)
                                        || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y))
                                        throw new InvalidDataException("Native battle animation produced a non-finite vertex.");
                            result.sampledPoses++;
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
            File.WriteAllText(Path.Combine(output, ordered ? "native-partner-ordered-animation-probes.json" : "native-partner-animation-probes.json"),
                JsonUtility.ToJson(new Probes { animations = results.ToArray() }, true));
            Debug.Log("Original full-binary partner data loaded: 34 fighters, 35 renderers, " + textures.Count
                + " mapped textures, " + results.Count
                + " original animations; native constraint/mesh probes supported=" + results.Count(value => value.supported)
                + ", unsupported=" + results.Count(value => !value.supported)
                + ", sampled poses=" + results.Sum(value => value.sampledPoses)
                + ". Materials, Animator playback and battle rendering are not validated by this data check.");
        }

        private static void CollectTimes(object value, SortedSet<float> times)
        {
            if (value is IReadOnlyDictionary<string, object> map)
            {
                if (map.TryGetValue("time", out object time)) times.Add(Convert.ToSingle(time, System.Globalization.CultureInfo.InvariantCulture));
                foreach (object child in map.Values) CollectTimes(child, times);
            }
            else if (value is List<object> list) foreach (object child in list) CollectTimes(child, times);
        }

        private static void ValidateOrderedGeometry()
        {
            foreach (float scaleY in new[] { .6f, 1.8f, -.6f, -1.8f, -1.2f })
                foreach (bool bend in new[] { false, true })
                {
                    // Construct a reachable tip independently from FK. The
                    // inverse solution must reach it and retain both lengths.
                    float elbow = bend ? 67 : -67;
                    Matrix4x4 expectedFirst = Matrix4x4.TRS(new Vector3(1, -2, 0),
                        Quaternion.Euler(0, 0, 31), new Vector3(1.2f, scaleY, 1));
                    Vector3 target = (expectedFirst * Matrix4x4.TRS(new Vector3(2, 0, 0),
                        Quaternion.Euler(0, 0, elbow), Vector3.one)).MultiplyPoint3x4(Vector3.right);
                    var elliptic = new ProfessionNativeBonePose(new[] {
                        new ProfessionNativeBonePose.Bone { name = "first", x = 1, y = -2, scaleX = 1.2f, scaleY = scaleY },
                        new ProfessionNativeBonePose.Bone { name = "child", parent = 0, x = 2, y = .03f },
                        new ProfessionNativeBonePose.Bone { name = "target", x = target.x, y = target.y }
                    });
                    ProfessionNativeAffineIk.ApplyPair(elliptic, new ProfessionNativeConstraintAnimation.IkState {
                        Name = "ellipse", Bones = new[] { 0, 1 }, Target = 2, Mix = 1, ChildLength = 1,
                        BendPositive = bend, Stretch = true
                    });
                    if (Vector3.Distance(elliptic.World(1).MultiplyPoint3x4(Vector3.right), target) > .0001f
                        || Math.Abs(elliptic.World(0).MultiplyVector(Vector3.right).magnitude - 1.2f) > .0001f
                        || Math.Abs(elliptic.World(0).MultiplyVector(Vector3.up).magnitude - Math.Abs(scaleY)) > .0001f)
                        throw new InvalidDataException("Nonuniform two-bone IK missed the known FK endpoint or changed original scales.");
                }
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "root" },
                new ProfessionNativeBonePose.Bone { name = "first", parent = 0, x = 1 },
                new ProfessionNativeBonePose.Bone { name = "child", parent = 1, x = 2 },
                new ProfessionNativeBonePose.Bone { name = "target", x = 4, y = 5 },
                new ProfessionNativeBonePose.Bone { name = "unrelated", x = -5 }
            });
            Matrix4x4 root = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 30), new Vector3(2, 2, 1));
            Matrix4x4 unrelated = Matrix4x4.Translate(new Vector3(-3, 6, 0));
            pose.OverrideWorld(0, root);
            pose.OverrideWorld(4, unrelated);
            pose.OverrideWorld(1, root * Matrix4x4.TRS(new Vector3(1, 0, 0), Quaternion.Euler(0, 0, 20), Vector3.one));
            var pair = new ProfessionNativeConstraintAnimation.IkState {
                Name = "ordered pair", Bones = new[] { 1, 2 }, Target = 3, Mix = 1,
                ChildLength = 1, BendPositive = true
            };
            ProfessionNativeAffineIk.ApplyPair(pose, pair);
            Vector3 endpoint = pose.World(2).MultiplyPoint3x4(Vector3.right);
            if (Vector3.Distance(endpoint, new Vector3(4, 5, 0)) > .0001f
                || pose.World(0) != root || pose.World(4) != unrelated)
                throw new InvalidDataException("Ordered affine IK lost a prior constraint or missed the reachable target.");
            pose.Reset();
            root = Matrix4x4.TRS(new Vector3(2, 1, 0), Quaternion.Euler(0, 0, 90), Vector3.one);
            pose.OverrideWorld(0, root);
            pose.OverrideWorld(3, Matrix4x4.Translate(new Vector3(0, 4, 0)));
            var single = new ProfessionNativeConstraintAnimation.IkState {
                Name = "ordered single stretch", Bones = new[] { 1 }, Target = 3, Mix = 1, Stretch = true
            };
            ProfessionNativeAffineIk.ApplySingle(pose, single, 2);
            endpoint = pose.World(1).MultiplyPoint3x4(new Vector3(2, 0, 0));
            if (Vector3.Distance(endpoint, new Vector3(0, 4, 0)) > .0001f || pose.World(0) != root)
                throw new InvalidDataException("Ordered single-bone stretch lost its parent frame or target.");
            pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "reflected parent", scaleX = -2, scaleY = 3 },
                new ProfessionNativeBonePose.Bone { name = "without reflection", parent = 0, x = 1,
                    inherit = 4, scaleX = 1.5f, scaleY = .5f },
                new ProfessionNativeBonePose.Bone { name = "with reflection", parent = 0, x = 1,
                    inherit = 3, scaleX = 1.5f, scaleY = .5f }
            }, allowNoScaleOrReflection: true);
            Matrix4x4 without = pose.World(1), with = pose.World(2);
            if (Vector3.Distance(without.MultiplyPoint3x4(Vector3.zero), new Vector3(-2, 0, 0)) > .0001f
                || Vector3.Distance(without.MultiplyVector(Vector3.right), new Vector3(-1.5f, 0, 0)) > .0001f
                || Vector3.Distance(without.MultiplyVector(Vector3.up), new Vector3(0, -.5f, 0)) > .0001f
                || Vector3.Distance(with.MultiplyVector(Vector3.up), new Vector3(0, .5f, 0)) > .0001f)
                throw new InvalidDataException("Original no-scale/reflection inheritance changed the child axes or position.");
            foreach (bool reverse in new[] { false, true })
            {
                Vector2[] boundary = { Vector2.zero, new Vector2(2, 0), new Vector2(2, 1),
                    Vector2.one, new Vector2(1, 2), new Vector2(0, 2) };
                if (reverse) Array.Reverse(boundary);
                var clipping = new ProfessionNativePolygonClipper(boundary, true);
                var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var indices = new List<int>();
                clipping.AppendTriangle(new Vector2(-10, -10), new Vector2(10, -10), new Vector2(0, 10),
                    Vector2.zero, Vector2.right, new Vector2(.5f, 1), vertices, uvs, indices);
                double area = 0, xMoment = 0, yMoment = 0;
                for (int i = 0; i < indices.Count; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    double portion = ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
                    if (portion <= 0) throw new InvalidDataException("Concave clipping changed source triangle winding.");
                    portion *= .5; area += portion;
                    xMoment += portion * (a.x + b.x + c.x) / 3;
                    yMoment += portion * (a.y + b.y + c.y) / 3;
                }
                if (Math.Abs(area - 3) > .00001 || Math.Abs(xMoment / area - 5.0 / 6) > .00001
                    || Math.Abs(yMoment / area - 5.0 / 6) > .00001)
                    throw new InvalidDataException("Concave clipping changed the L polygon area or centroid.");
                for (int i = 0; i < vertices.Count; i++)
                    if (Vector2.Distance(uvs[i], new Vector2((vertices[i].x + 10) / 20, (vertices[i].y + 10) / 20)) > .00001f)
                        throw new InvalidDataException("Concave clipping changed affine source UVs.");
            }
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
