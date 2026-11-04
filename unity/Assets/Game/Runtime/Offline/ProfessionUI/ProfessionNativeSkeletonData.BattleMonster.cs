using System;
using System.IO;
using System.Text;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSkeletonData
    {
        public static ProfessionNativeSkeletonData LoadBattleMonster(byte[] data, int entityId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "ZJCSMNST" || reader.ReadUInt32() != 1)
                throw new InvalidDataException("Unknown original battle monster package format.");
            string manifest = ReadString(reader);
            int originalEntity = reader.ReadInt32();
            float scale = reader.ReadSingle();
            if (manifest != "884_156692" || originalEntity != entityId || !float.IsFinite(scale) || scale <= 0)
                throw new InvalidDataException("Original battle monster identity or scale differs.");
            string skeleton = ReadBlock(reader, out string skeletonHash);
            string atlas = ReadBlock(reader, out string atlasHash);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing original battle monster data.");
            return new ProfessionNativeSkeletonData(manifest, "battle-monster:" + originalEntity, scale, skeletonHash, atlasHash,
                Object(OfficialEquipmentJson.Parse(skeleton)), Array(OfficialEquipmentJson.Parse(atlas)));
        }
    }
}
