using System;
using System.IO;
using System.Text;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSkeletonData
    {
        // A separate format keeps NPC identity explicit and leaves the existing
        // player/weapon package format and its validation unchanged.
        public static ProfessionNativeSkeletonData LoadNpc(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "ZJCSNPCS" || reader.ReadUInt32() != 1)
                throw new InvalidDataException("Unknown original NPC skeleton package format.");
            string manifest = ReadString(reader), model = ReadString(reader);
            float scale = reader.ReadSingle();
            if (manifest != "871_156598" || !model.StartsWith("npc_", StringComparison.Ordinal) ||
                !model.EndsWith("_SkeletonData", StringComparison.Ordinal) || model.Contains("/") ||
                model.Contains("\\") || !(scale > 0) || float.IsInfinity(scale))
                throw new InvalidDataException("Original NPC skeleton source identity or scale differs.");
            string skeleton = ReadBlock(reader, out string skeletonHash);
            string atlas = ReadBlock(reader, out string atlasHash);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing original NPC skeleton data.");
            return new ProfessionNativeSkeletonData(manifest, "npc:" + model, scale, skeletonHash, atlasHash,
                Object(OfficialEquipmentJson.Parse(skeleton)), Array(OfficialEquipmentJson.Parse(atlas)));
        }
    }
}
