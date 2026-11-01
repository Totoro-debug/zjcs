using System;
using System.IO;
using System.Text;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSkeletonData
    {
        // Current partner illustrations have their own identity. They must never
        // be accepted as historical battle NPCs or player appearance packages.
        public static ProfessionNativeSkeletonData LoadPartnerPortrait(byte[] data, int npcId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (npcId <= 0) throw new ArgumentOutOfRangeException(nameof(npcId));
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "ZJCSPNPC" || reader.ReadUInt32() != 1)
                throw new InvalidDataException("Unknown original partner portrait package format.");
            string manifest = ReadString(reader);
            int originalNpc = reader.ReadInt32();
            float scale = reader.ReadSingle();
            if (manifest != "884_156692" || originalNpc != npcId || !(scale > 0) || float.IsInfinity(scale))
                throw new InvalidDataException("Original partner portrait identity or scale differs.");
            string skeleton = ReadBlock(reader, out string skeletonHash);
            string atlas = ReadBlock(reader, out string atlasHash);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing original partner portrait data.");
            return new ProfessionNativeSkeletonData(manifest, "partner-portrait:" + originalNpc, scale, skeletonHash, atlasHash,
                Object(OfficialEquipmentJson.Parse(skeleton)), Array(OfficialEquipmentJson.Parse(atlas)));
        }
    }
}
