using System;
using System.IO;
using System.Text;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSkeletonData
    {
        // World props share the independent geometry backend, with a separate
        // source identity. They are neither character nor weapon packages.
        public static ProfessionNativeSkeletonData LoadWorld(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "ZJCSWRLD" || reader.ReadUInt32() != 1)
                throw new InvalidDataException("Unknown original world skeleton package format.");
            string manifest = ReadString(reader), model = ReadString(reader);
            float scale = reader.ReadSingle();
            if (manifest != "884_156692" || string.IsNullOrEmpty(model) || model.Contains("/") ||
                model.Contains("\\") || !(scale > 0) || float.IsInfinity(scale))
                throw new InvalidDataException("Original world skeleton source identity or scale differs.");
            string skeleton = ReadBlock(reader, out string skeletonHash);
            string atlas = ReadBlock(reader, out string atlasHash);
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing original world skeleton data.");
            return new ProfessionNativeSkeletonData(manifest, "world:" + model, scale, skeletonHash, atlasHash,
                Object(OfficialEquipmentJson.Parse(skeleton)), Array(OfficialEquipmentJson.Parse(atlas)));
        }
    }
}
