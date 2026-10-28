using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent data binding, not an animation SDK. The complete source JSON
    // remains available to subsequent constraint/timeline stages without lossy
    // normalization, pose baking or replacing unsupported fields with defaults.
    public sealed class ProfessionNativeSkeletonData
    {
        public sealed class Constraint
        {
            public readonly string Kind, Name;
            public readonly IReadOnlyDictionary<string, object> Fields;
            internal Constraint(string kind, Dictionary<string, object> fields)
            { Kind = kind; Name = Text(fields, "name"); Fields = fields; }
        }
        private readonly Dictionary<string, object> source, animations;
        private readonly ProfessionNativeBonePose.Bone[] bones;
        private readonly ProfessionNativeSlotPose.Slot[] slots;
        private readonly ProfessionNativeSkin[] skins;
        private readonly Dictionary<string, int> boneIndices, slotIndices;
        public string Manifest { get; }
        public string Sex { get; }
        public float DataScale { get; }
        public string SkeletonSha256 { get; }
        public string AtlasSha256 { get; }
        public int BoneCount => bones.Length;
        public int SlotCount => slots.Length;
        public int SkinCount => skins.Length;
        public int AnimationCount => animations.Count;
        public int AtlasRegionCount { get; }
        public int AttachmentCount { get; }
        public IReadOnlyList<Constraint> Constraints { get; }
        public IEnumerable<string> AnimationNames => animations.Keys;
        public IEnumerable<string> SkinNames => skins.Select(s => s.Name);
        public ProfessionNativeSkinCatalog SkinCatalog { get; }
        public IReadOnlyDictionary<string, object> SourceFields => source;

        public static ProfessionNativeSkeletonData LoadResource(string sex)
        {
            if (sex != "male" && sex != "female") throw new ArgumentException("Unknown official player skeleton sex.", nameof(sex));
            var asset = Resources.Load<TextAsset>("ProfessionUI/NativeSkeletons/" + sex);
            if (!asset) throw new FileNotFoundException("Packaged original player skeleton is unavailable: " + sex);
            var result = Load(asset.bytes);
            if (result.Sex != sex) throw new InvalidDataException("Packaged original skeleton identity differs.");
            return result;
        }

        public static ProfessionNativeSkeletonData Load(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "ZJCSPROF" || reader.ReadUInt32() != 1)
                    throw new InvalidDataException("Unknown native skeleton data format.");
                string manifest = ReadString(reader), sex = ReadString(reader);
                float scale = reader.ReadSingle();
                if (manifest != "871_156598" || sex != "male" && sex != "female" || !(scale > 0) || float.IsInfinity(scale))
                    throw new InvalidDataException("Original skeleton source identity/scale differs.");
                string skeleton = ReadBlock(reader, out string skeletonHash);
                string atlas = ReadBlock(reader, out string atlasHash);
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing native skeleton data.");
                return new ProfessionNativeSkeletonData(manifest, sex, scale, skeletonHash, atlasHash,
                    Object(OfficialEquipmentJson.Parse(skeleton)), Array(OfficialEquipmentJson.Parse(atlas)));
            }
        }

        private ProfessionNativeSkeletonData(string manifest, string sex, float scale, string skeletonHash,
            string atlasHash, Dictionary<string, object> source, List<object> atlas)
        {
            Manifest = manifest; Sex = sex; DataScale = scale; SkeletonSha256 = skeletonHash; AtlasSha256 = atlasHash;
            this.source = source; animations = Object(source["animations"]);
            var boneRows = Array(source["bones"]);
            bones = new ProfessionNativeBonePose.Bone[boneRows.Count];
            boneIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] modes = { "normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection" };
            for (int i = 0; i < bones.Length; i++)
            {
                var row = Object(boneRows[i]); string name = Text(row, "name");
                int mode = System.Array.IndexOf(modes, Text(row, "inherit", "normal"));
                if (mode < 0) throw new InvalidDataException("Unrecognized original inheritance mode.");
                bones[i] = new ProfessionNativeBonePose.Bone { name = name,
                    parent = row.ContainsKey("parent") ? boneIndices[Text(row, "parent")] : -1,
                    inherit = mode, skinRequired = Boolean(row, "skin"), x = Number(row, "x"), y = Number(row, "y"),
                    rotation = Number(row, "rotation"), scaleX = Number(row, "scaleX", 1), scaleY = Number(row, "scaleY", 1),
                    shearX = Number(row, "shearX"), shearY = Number(row, "shearY") };
                boneIndices.Add(name, i);
            }
            var slotRows = Array(source["slots"]);
            slots = new ProfessionNativeSlotPose.Slot[slotRows.Count]; slotIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] blendModes = { "normal", "additive", "multiply", "screen" };
            for (int i = 0; i < slots.Length; i++)
            {
                var row = Object(slotRows[i]); int blend = System.Array.IndexOf(blendModes, Text(row, "blend", "normal"));
                if (blend < 0) throw new InvalidDataException("Unrecognized original slot blend mode.");
                slots[i] = new ProfessionNativeSlotPose.Slot { name = Text(row, "name"), bone = boneIndices[Text(row, "bone")],
                    attachment = Text(row, "attachment", null), blend = blend, color = Color(Text(row, "color", "ffffffff")),
                    hasDarkColor = row.ContainsKey("dark"), darkColor = Color(Text(row, "dark", "00000000")) };
                slotIndices.Add(slots[i].name, i);
            }
            var constraints = new List<Constraint>(); var constraintIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] groups = { "ik", "transform", "path", "physics" };
            foreach (string kind in groups)
                foreach (var value in OptionalArray(source, kind))
                {
                    var constraint = new Constraint(kind, Object(value));
                    constraintIndices.Add(kind + ":" + constraint.Name, constraints.Count); constraints.Add(constraint);
                }
            Constraints = constraints.AsReadOnly();
            var regions = new Dictionary<string, (string page, ProfessionNativeAtlasRegion region)>(StringComparer.Ordinal);
            foreach (var value in atlas)
            {
                var row = Object(value); var size = Array(row["size"]); var bounds = Array(row["bounds"]); var offsets = Array(row["offsets"]);
                regions.Add(Text(row, "name"), (Text(row, "page"), new ProfessionNativeAtlasRegion(Int(size[0]), Int(size[1]),
                    Int(bounds[0]), Int(bounds[1]), Int(bounds[2]), Int(bounds[3]), Int(offsets[0]), Int(offsets[1]),
                    Int(offsets[2]), Int(offsets[3]), (int)Number(row, "rotate"))));
            }
            AtlasRegionCount = regions.Count;
            var skinRows = Array(source["skins"]); skins = new ProfessionNativeSkin[skinRows.Count]; int attachmentCount = 0;
            for (int i = 0; i < skins.Length; i++)
            {
                var row = Object(skinRows[i]); var entries = new List<ProfessionNativeSkin.Entry>();
                foreach (var slot in OptionalObject(row, "attachments"))
                    foreach (var entry in Object(slot.Value))
                    {
                        int slotIndex = slotIndices[slot.Key]; var attachment = Object(entry.Value);
                        string name = Text(attachment, "name", entry.Key), kind = Text(attachment, "type", "region");
                        int bone = slots[slotIndex].bone; ProfessionNativeAttachment result;
                        switch (kind)
                        {
                            case "region":
                            case "mesh":
                                var region = regions[Text(attachment, "path", name)];
                                result = kind == "region" ? new ProfessionNativeRenderAttachment(name, region.page, bone, region.region,
                                    Number(attachment, "width"), Number(attachment, "height"), Number(attachment, "x"), Number(attachment, "y"),
                                    Number(attachment, "scaleX", 1), Number(attachment, "scaleY", 1), Number(attachment, "rotation"), scale)
                                    : new ProfessionNativeRenderAttachment(name, region.page, bone, bones.Length, region.region,
                                        Floats(attachment["vertices"]), Floats(attachment["uvs"]), Array(attachment["triangles"]).Select(Int).ToArray(), scale);
                                break;
                            case "boundingbox":
                                result = new ProfessionNativeBoundingAttachment(name, bone, bones.Length, Floats(attachment["vertices"]), (int)Number(attachment, "vertexCount"), scale); break;
                            case "clipping":
                                result = new ProfessionNativeClippingAttachment(name, bone, bones.Length, Floats(attachment["vertices"]), (int)Number(attachment, "vertexCount"), slotIndices[Text(attachment, "end")], slots.Length, scale); break;
                            case "point":
                                result = new ProfessionNativePointAttachment(name, bone, Number(attachment, "x"), Number(attachment, "y"), scale); break;
                            default: throw new NotSupportedException("Restore additional original attachment type: " + kind);
                        }
                        entries.Add(new ProfessionNativeSkin.Entry(slotIndex, entry.Key, result)); attachmentCount++;
                    }
                var skinConstraints = new List<int>();
                foreach (string kind in groups)
                    foreach (var name in OptionalArray(row, kind)) skinConstraints.Add(constraintIndices[kind + ":" + (string)name]);
                skins[i] = new ProfessionNativeSkin(Text(row, "name"), slots.Length, bones.Length, constraints.Count, entries.ToArray(),
                    OptionalArray(row, "bones").Select(name => boneIndices[(string)name]).ToArray(), skinConstraints.ToArray());
            }
            AttachmentCount = attachmentCount; SkinCatalog = new ProfessionNativeSkinCatalog(skins);
        }

        public ProfessionNativeBonePose CreateBonePose() => new ProfessionNativeBonePose(bones);
        public ProfessionNativeSlotPose CreateSlotPose(ProfessionNativeBonePose pose) => new ProfessionNativeSlotPose(slots, pose);
        public IReadOnlyDictionary<string, object> GetAnimation(string name) => Object(animations[name]);
        public int FindBone(string name) => boneIndices.TryGetValue(name, out int value) ? value : -1;
        public int FindSlot(string name) => slotIndices.TryGetValue(name, out int value) ? value : -1;

        private static string ReadString(BinaryReader reader)
        {
            int length = checked((int)reader.ReadUInt32());
            return new UTF8Encoding(false, true).GetString(ReadBytes(reader, length));
        }
        private static byte[] ReadBytes(BinaryReader reader, int length)
        {
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new EndOfStreamException();
            byte[] bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); return bytes;
        }
        private static string ReadBlock(BinaryReader reader, out string hash)
        {
            int length = checked((int)reader.ReadUInt32()); byte[] expected = ReadBytes(reader, 32);
            byte[] compressed = ReadBytes(reader, checked((int)reader.ReadUInt32()));
            using (var input = new MemoryStream(compressed, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(length))
            {
                gzip.CopyTo(output); byte[] bytes = output.ToArray();
                if (bytes.Length != length) throw new InvalidDataException("Original native data block length differs.");
                using (var sha = SHA256.Create())
                {
                    byte[] actual = sha.ComputeHash(bytes);
                    if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Original native data block hash differs.");
                    hash = BitConverter.ToString(actual).Replace("-", "").ToLowerInvariant();
                }
                return new UTF8Encoding(false, true).GetString(bytes);
            }
        }
        private static Dictionary<string, object> Object(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Expected an original data object.");
        private static List<object> Array(object value) => value as List<object> ?? throw new InvalidDataException("Expected an original data array.");
        private static IEnumerable<object> OptionalArray(Dictionary<string, object> value, string key) => value.TryGetValue(key, out var row) ? Array(row) : Enumerable.Empty<object>();
        private static Dictionary<string, object> OptionalObject(Dictionary<string, object> value, string key) => value.TryGetValue(key, out var row) ? Object(row) : new Dictionary<string, object>();
        private static string Text(Dictionary<string, object> value, string key, string fallback = null) => value.TryGetValue(key, out var row) ? (string)row : fallback;
        private static float Number(Dictionary<string, object> value, string key, float fallback = 0) => value.TryGetValue(key, out var row) ? Convert.ToSingle(row, CultureInfo.InvariantCulture) : fallback;
        private static bool Boolean(Dictionary<string, object> value, string key) => value.TryGetValue(key, out var row) && (bool)row;
        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static float[] Floats(object value) => Array(value).Select(row => Convert.ToSingle(row, CultureInfo.InvariantCulture)).ToArray();
        private static Color32 Color(string value)
        {
            if (value.Length != 6 && value.Length != 8) throw new InvalidDataException("Original RGBA/RGB color differs.");
            byte Channel(int offset) => byte.Parse(value.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32(Channel(0), Channel(2), Channel(4), value.Length == 8 ? Channel(6) : (byte)255);
        }
    }
}
