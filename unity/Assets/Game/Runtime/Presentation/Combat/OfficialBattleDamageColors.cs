using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialDamageColorVariants
    {
        public int element;
        public float[] normal = new[] { 1f, 1f, 1f, 1f };
        public float[] crit = new[] { 1f, 1f, 1f, 1f };
        public float[] block = new[] { 1f, 1f, 1f, 1f };
        public float[] immunity = new[] { 1f, 1f, 1f, 1f };
    }

    [Serializable]
    public sealed class OfficialDamageColorSettings
    {
        public List<OfficialDamageColorVariants> elements = new List<OfficialDamageColorVariants>();
        public float[] cure = new[] { 1f, 1f, 1f, 1f };
        public float[] skillCost = new[] { 1f, 1f, 1f, 1f };
        public float[] statusDamage = new[] { 1f, 1f, 1f, 1f };
        public float[] statusCure = new[] { 1f, 1f, 1f, 1f };
        public float[] defaultColor = new[] { 1f, 1f, 1f, 1f };
        public float[] spacing = new[] { 0f, -0.1f, 0f };
    }

    /// <summary>
    /// Official battle-HUD damage number colours exported by
    /// <c>scripts/prepare_unity_combat_damage_colors.py</c>. Missing data falls back to the default
    /// colour so the scene never invents element colours.
    /// </summary>
    public sealed class OfficialBattleDamageColors
    {
        public const string ResourcePath = "Combat/damage-colors";
        private readonly Dictionary<int, OfficialDamageColorVariants> elements =
            new Dictionary<int, OfficialDamageColorVariants>();

        public OfficialDamageColorSettings Settings { get; private set; }

        public static OfficialBattleDamageColors Load()
        {
            var colors = new OfficialBattleDamageColors();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                colors.Settings = new OfficialDamageColorSettings();
                return colors;
            }
            colors.Settings = JsonUtility.FromJson<OfficialDamageColorSettings>(asset.text)
                ?? new OfficialDamageColorSettings();
            foreach (OfficialDamageColorVariants variant in colors.Settings.elements)
                if (variant != null) colors.elements[variant.element] = variant;
            return colors;
        }

        public Vector3 Spacing
        {
            get
            {
                float[] spacing = Settings == null ? null : Settings.spacing;
                return spacing != null && spacing.Length >= 3
                    ? new Vector3(spacing[0], spacing[1], spacing[2]) : new Vector3(0f, -0.1f, 0f);
            }
        }

        public Color Resolve(FightBattleDamageEvent damage, int element = 0)
        {
            if (damage == null) return ToColor(Settings == null ? null : Settings.defaultColor);
            if (damage.IsCure)
                return ToColor(Settings == null
                    ? null : (damage.SkillEntityId == 0 ? Settings.statusCure : Settings.cure));
            if (damage.Blinding || damage.Immunity || damage.Dodge)
                return ToColor(Settings == null ? null : Settings.defaultColor);
            if (elements.TryGetValue(element, out OfficialDamageColorVariants variant))
            {
                if (damage.Critical) return ToColor(variant.crit);
                if (damage.Block) return ToColor(variant.block);
                return ToColor(variant.normal);
            }
            return ToColor(Settings == null ? null : Settings.defaultColor);
        }

        private static Color ToColor(float[] values)
        {
            if (values == null || values.Length < 3) return Color.white;
            float alpha = values.Length >= 4 ? values[3] : 1f;
            return new Color(values[0], values[1], values[2], alpha);
        }
    }
}
