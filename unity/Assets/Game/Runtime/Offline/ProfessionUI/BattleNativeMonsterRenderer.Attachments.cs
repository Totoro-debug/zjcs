using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class BattleNativeMonsterRenderer
    {
        public PartnerNativeBattleAttachments Attachments { get; private set; }

        public void BindOriginalAttachments(int entityId)
        {
            if (Attachments != null) throw new InvalidOperationException("Original monster attachments are already bound.");
            var asset = Resources.Load<TextAsset>("Combat/NativeMonsters/attachments");
            if (!asset) throw new FileNotFoundException("Original monster attachment graph is not packaged.");
            var catalog = Obj(OfficialEquipmentJson.Parse(asset.text));
            var row = Arr(catalog["partners"]).Select(Obj).Single(value => Int(value, "entityId") == entityId);
            var nodes = Arr(row["nodes"]).Select(Obj).ToArray();
            var originals = new Dictionary<string, Transform>(StringComparer.Ordinal)
                { [(string)nodes[0]["sourceKey"]] = EntityRoot };
            foreach (var node in nodes.Skip(1))
                if (originals.TryGetValue((string)node["parentKey"], out Transform parent))
                {
                    Transform child = parent.Find((string)node["name"]);
                    if (child) originals.Add((string)node["sourceKey"], child);
                }
            string name = EntityRoot.name;
            Attachments = new PartnerNativeBattleAttachments(EntityRoot.parent, EntityRoot, row, originals,
                new[] { new PartnerNativeBattleAttachments.RendererBinding(transform, state, settings, material) },
                () => Advance(0), bindBoneFollowers: true);
            EntityRoot.name = name;
        }
    }
}
