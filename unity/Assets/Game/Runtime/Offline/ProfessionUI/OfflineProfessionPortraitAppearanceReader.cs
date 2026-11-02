using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // The local wardrobe has no durable server ModifyTimeStamp. Give the original
    // portrait hash a stable local version of its complete appearance snapshot,
    // including sex/profession, without changing that snapshot or gameplay state.
    public sealed class OfflineProfessionPortraitAppearanceReader : IProfessionPortraitAppearanceReader
    {
        private readonly IProfessionPlayerAppearanceSource player;
        private readonly ProfessionPortraitAppearanceReader fields = new ProfessionPortraitAppearanceReader();
        public OfflineProfessionPortraitAppearanceReader(IProfessionPlayerAppearanceSource player)
            => this.player = player ?? throw new ArgumentNullException(nameof(player));
        public IEnumerable<KeyValuePair<SkinType, object>> GetSkins(object appearance) => fields.GetSkins(appearance);
        public int GetSkinId(object skin) => fields.GetSkinId(skin);
        public long GetModifyTimeStamp(object appearance)
        {
            var data = appearance as AppearanceData ?? throw new ArgumentException(nameof(appearance));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write("Zjcs offline portrait appearance v1");
                writer.Write((int)player.Sex); writer.Write(player.Profession);
                Map(writer, data.SkinMap, value => {
                    writer.Write(value != null);
                    if (value != null) { writer.Write((int)value.Type); writer.Write(value.SkinId); }
                });
                Map(writer, data.DyeMap, value => {
                    writer.Write(value != null);
                    if (value != null) { writer.Write(value.ColorId); writer.Write(value.Hsv.H); writer.Write(value.Hsv.S); writer.Write(value.Hsv.V); }
                });
                Map(writer, data.AppendAppearanceDict, writer.Write);
                Map(writer, data.EquipSkinDict, writer.Write);
                Map(writer, data.HonorItemDict, writer.Write);
                Map(writer, data.AppendAppearanceWearTimeDict, value => writer.Write(value.ToBinary()));
                writer.Write(data.AppearanceLevel); writer.Write(data.FullBodyAppearanceId);
                writer.Write(data.LimitItemExpSet?.Count ?? -1);
                if (data.LimitItemExpSet != null)
                    foreach (int value in data.LimitItemExpSet.OrderBy(value => value)) writer.Write(value);
            }
            using var hash = SHA256.Create();
            return BitConverter.ToInt64(hash.ComputeHash(stream.ToArray()), 0) & long.MaxValue;
        }
        private static void Map<TKey, TValue>(BinaryWriter writer, Dictionary<TKey, TValue> values, Action<TValue> write)
        {
            writer.Write(values?.Count ?? -1);
            if (values == null) return;
            foreach (var pair in values.OrderBy(value => Convert.ToInt32(value.Key)))
            { writer.Write(Convert.ToInt32(pair.Key)); write(pair.Value); }
        }
    }
}
