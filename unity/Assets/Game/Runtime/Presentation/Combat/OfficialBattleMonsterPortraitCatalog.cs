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
    /// Extracted official Map10 monster portrait sprites keyed by the battle-rules monster IconId.
    /// Unknown ids return null so timeline cards retain the official prefab default portrait.
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
            return byIconId.TryGetValue(iconId, out Sprite sprite) ? sprite : null;
        }

        public int Count => bindings == null ? 0 : bindings.Length;
    }
}
