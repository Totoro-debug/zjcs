using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class CombatSkillIconBinding
    {
        public int itemId;
        public Sprite sprite;
    }

    /// <summary>
    /// Native skill-item Sprites shared with the original character interfaces.
    /// </summary>
    [CreateAssetMenu(menuName = "Zjcs/Combat/Official Combat Skill Icon Catalog")]
    public sealed class OfficialCombatSkillIconCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Combat/OfficialCombatSkillIconCatalog";

        [SerializeField] private CombatSkillIconBinding[] icons = Array.Empty<CombatSkillIconBinding>();
        private Dictionary<int, Sprite> sprites;

        public static OfficialCombatSkillIconCatalog TryLoadDefault()
        {
            return Resources.Load<OfficialCombatSkillIconCatalog>(DefaultResourcePath);
        }

        public Sprite Find(int itemId)
        {
            if (itemId <= 0) return null;
            if (sprites == null)
            {
                sprites = new Dictionary<int, Sprite>();
                foreach (CombatSkillIconBinding binding in icons ?? Array.Empty<CombatSkillIconBinding>())
                {
                    if (binding == null || binding.itemId <= 0 || binding.sprite == null
                        || sprites.ContainsKey(binding.itemId)) continue;
                    sprites.Add(binding.itemId, binding.sprite);
                }
            }
            return sprites.TryGetValue(itemId, out Sprite result) ? result : null;
        }

        public int Count => icons == null ? 0 : icons.Length;
    }
}
