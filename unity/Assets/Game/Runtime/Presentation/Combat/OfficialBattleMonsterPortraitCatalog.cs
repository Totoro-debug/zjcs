using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleMonsterPortraitBinding
    {
        public int iconId;
        public Sprite sprite;
    }

    /// <summary>
    /// Extracted official Map10/Map11 monster portrait sprites keyed by the battle-rules monster IconId.
    /// Unknown ids return null; callers must report missing official portrait bindings.
    /// </summary>
    [CreateAssetMenu(menuName = "Zjcs/Combat/Official Battle Monster Portrait Catalog")]
    public sealed class OfficialBattleMonsterPortraitCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Combat/OfficialBattleMonsterPortraitCatalog";

        [SerializeField] private OfficialBattleMonsterPortraitBinding[] bindings =
            Array.Empty<OfficialBattleMonsterPortraitBinding>();
        private Dictionary<int, Sprite> byIconId;

        public static OfficialBattleMonsterPortraitCatalog TryLoadDefault()
        {
            return Resources.Load<OfficialBattleMonsterPortraitCatalog>(DefaultResourcePath);
        }

        public Sprite Find(int iconId)
        {
            if (iconId <= 0) return null;
            if (byIconId == null)
            {
                byIconId = new Dictionary<int, Sprite>();
                foreach (OfficialBattleMonsterPortraitBinding binding in bindings
                    ?? Array.Empty<OfficialBattleMonsterPortraitBinding>())
                    if (binding != null && binding.iconId > 0 && binding.sprite != null
                        && !byIconId.ContainsKey(binding.iconId)) byIconId.Add(binding.iconId, binding.sprite);
            }
            if (byIconId.TryGetValue(iconId, out Sprite sprite)) return sprite;
            // Pet and other catalogs use their own id namespace.
            if (this != TryLoadDefault()) return null;
            var additional = Resources.Load<OfficialBattleMonsterPortraitCatalog>("Combat/EmbeddedSourceMonsterPortraitCatalog");
            Sprite supplemental = additional != null && additional != this ? additional.Find(iconId) : null;
            if (supplemental != null) return supplemental;
            var statusGraph = Resources.Load<OfficialBattleMonsterPortraitCatalog>("Combat/StatusGraphMonsterPortraitCatalog");
            return statusGraph != null && statusGraph != this ? statusGraph.Find(iconId) : null;
        }

        public int Count => bindings == null ? 0 : bindings.Length;
    }
}
