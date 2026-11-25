using System;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.Progression
{
    public static class OfficialPassiveSkillPropertyPacks
    {
        [Serializable] private sealed class Index { public string[] resources; }

        public static void Apply(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (items == null) throw new ArgumentNullException(nameof(items));
            var input = Resources.Load<TextAsset>("Combat/passive-property-packs");
            var index = input ? JsonUtility.FromJson<Index>(input.text) : null;
            if (index?.resources == null) throw new InvalidOperationException("Passive property pack index is missing.");
            foreach (string path in index.resources)
            {
                var pack = Resources.Load<TextAsset>(path);
                var receipt = Resources.Load<TextAsset>(path + "-source-manifest");
                if (!pack || !receipt) throw new InvalidOperationException("Passive property pack is unbound: " + path);
                var source = FightOfficialCombatCatalog.ReadSourceManifest(receipt.text);
                catalog.ApplyPassiveSkillRulePack(pack.text, source.OutputSha256, source.Manifest, items);
            }
        }
    }
}
