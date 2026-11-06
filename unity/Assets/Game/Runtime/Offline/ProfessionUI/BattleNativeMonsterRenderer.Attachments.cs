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
            BindOriginalMeshCopies(BattleNativeMonsterAssets.Load().Find(entityId), originals);
            BindOriginalBodyEffects(entityId, originals);
            EntityRoot.name = name;
        }

        private void BindOriginalBodyEffects(int entityId, Dictionary<string, Transform> originals)
        {
            if (entityId != 3803) return;
            var asset = Resources.Load<TextAsset>("Combat/monster-body-effects");
            if (!asset) throw new FileNotFoundException("Original monster body effects are not packaged.");
            var catalog = Obj(OfficialEquipmentJson.Parse(asset.text));
            if (!Equals(catalog["manifest"], "884_156692")) throw new InvalidDataException("Original body-effect version differs.");
            foreach (var effect in Arr(catalog["graphs"]).Select(Obj))
            {
                var prefab = Resources.Load<GameObject>((string)effect["resource"]);
                if (!prefab) throw new FileNotFoundException("Original monster body-effect prefab is unbound: " + effect["id"]);
                if (effect.TryGetValue("embeddedRootPaths", out object paths))
                {
                    var container = UnityEngine.Object.Instantiate(prefab, EntityRoot, false);
                    container.SetActive(false);
                    try
                    {
                        foreach (string path in Arr(paths))
                        {
                            Transform previous = EntityRoot.Find(path);
                            Transform replacement = container.transform.Find(path);
                            if (!previous || !replacement)
                                throw new InvalidDataException("Original embedded particle path is unbound: " + path);
                            replacement.SetParent(previous.parent, false);
                            ReplaceOriginalBodyEffect(previous, replacement);
                        }
                    }
                    finally
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(container);
                        else UnityEngine.Object.DestroyImmediate(container);
                    }
                }
                else
                {
                    Transform previous = originals.Values.Single(value => value.name == (string)effect["id"]);
                    ReplaceOriginalBodyEffect(previous,
                        UnityEngine.Object.Instantiate(prefab, previous.parent, false).transform);
                }
            }
        }

        private void ReplaceOriginalBodyEffect(Transform previous, Transform replacement)
        {
            replacement.name = previous.name;
            replacement.localPosition = previous.localPosition;
            replacement.localRotation = previous.localRotation;
            replacement.localScale = previous.localScale;
            replacement.SetSiblingIndex(previous.GetSiblingIndex());
            replacement.gameObject.SetActive(previous.gameObject.activeSelf);
            Attachments.RebindOriginalSubtree(replacement);
        }
    }
}
