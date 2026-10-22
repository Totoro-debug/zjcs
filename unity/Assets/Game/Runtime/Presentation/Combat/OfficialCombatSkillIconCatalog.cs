using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class CombatSkillIconBinding
    {
        public int itemId;
        // The official icon PNGs are imported as plain textures (textureType 0); the runtime builds
        // the sprite from the whole texture so the payload does not need a second import pass.
        public Texture2D texture;
        public float pivotX = 0.5f;
        public float pivotY = 0.5f;
        public float pixelsPerUnit = 100f;
    }

    /// <summary>
    /// Official skill-item icons generated from <c>icons.json</c> by
    /// <c>scripts/prepare_unity_combat_skill_icons.py</c>. Loading is optional; without the asset the
    /// battle HUD keeps using its fallback portrait.
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
                    if (binding == null || binding.itemId <= 0 || binding.texture == null
                        || sprites.ContainsKey(binding.itemId)) continue;
                    float ppu = binding.pixelsPerUnit > 0f ? binding.pixelsPerUnit : 100f;
                    Sprite sprite = Sprite.Create(binding.texture,
                        new Rect(0f, 0f, binding.texture.width, binding.texture.height),
                        new Vector2(Mathf.Clamp01(binding.pivotX), Mathf.Clamp01(binding.pivotY)), ppu);
                    sprite.name = "official_skill_icon_" + binding.itemId;
                    sprites.Add(binding.itemId, sprite);
                }
            }
            return sprites.TryGetValue(itemId, out Sprite result) ? result : null;
        }

        public int Count => icons == null ? 0 : icons.Length;
    }
}
