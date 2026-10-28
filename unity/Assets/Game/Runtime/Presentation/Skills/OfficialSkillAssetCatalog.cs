using System;
using Zjcs.Offline.Skills;
using UnityEngine;
using TMPro;

namespace Zjcs.Presentation.Skills
{
    [Serializable]
    public sealed class OfficialSkillSpriteBinding
    {
        public string sourcePath;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Zjcs/Skills/Official Skill Asset Catalog")]
    public sealed class OfficialSkillAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Skills/OfficialSkillAssetCatalog";

        [SerializeField] private TextAsset skillRecommendations;
        [SerializeField] private TextAsset skillRules;
        [SerializeField] private TextAsset runtimeRules;
        [SerializeField] private TextAsset cultivationRules;
        [SerializeField] private TextAsset uiLayouts;
        [SerializeField] private TextAsset fidelityTexts;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private TextAsset qualityGradient;
        [SerializeField] private TMP_FontAsset nativeUiFont;
        [SerializeField] private Shader nativeCharacterShader;
        [SerializeField] private Shader nativeCharacterUrpShader;
        [SerializeField] private OfficialSkillSpriteBinding[] sprites = Array.Empty<OfficialSkillSpriteBinding>();

        public TextAsset SkillRecommendations => skillRecommendations;
        public TextAsset SkillRules => skillRules;
        public TextAsset RuntimeRules => runtimeRules;
        public TextAsset CultivationRules => cultivationRules;
        public TextAsset UiLayouts => uiLayouts;
        public TextAsset FidelityTexts => fidelityTexts;
        public TextAsset SourceManifest => sourceManifest;
        public TextAsset QualityGradient => qualityGradient;
        public TMP_FontAsset NativeUiFont => nativeUiFont;
        public Shader NativePortraitShader => nativeCharacterShader;
        public Shader NativeCharacterShader => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? nativeCharacterUrpShader : nativeCharacterShader;

        public static OfficialSkillAssetCatalog LoadDefault()
        {
            OfficialSkillAssetCatalog catalog = Resources.Load<OfficialSkillAssetCatalog>(DefaultResourcePath);
            if (catalog == null)
                throw new InvalidOperationException("Official skill assets are not installed at Resources/" + DefaultResourcePath + ".asset");
            catalog.ValidateBindings();
            return catalog;
        }

        public OfflineSkillRules LoadProgressionRules()
        {
            ValidateBindings();
            OfflineSkillRules rules = JsonUtility.FromJson<OfflineSkillRules>(runtimeRules.text);
            if (rules == null || rules.Skills == null || rules.Skills.Length == 0)
                throw new InvalidOperationException("Official skill progression rules are invalid.");
            return rules;
        }

        public Sprite FindSprite(string sourceUri)
        {
            string normalized = NormalizeSpritePath(sourceUri);
            foreach (OfficialSkillSpriteBinding binding in sprites ?? Array.Empty<OfficialSkillSpriteBinding>())
                if (binding != null && binding.sourcePath == normalized) return binding.sprite;
            return null;
        }

        public void ValidateBindings()
        {
            if (skillRecommendations == null || skillRules == null || runtimeRules == null || cultivationRules == null
                || uiLayouts == null || fidelityTexts == null || sourceManifest == null || qualityGradient == null)
                throw new InvalidOperationException("Official skill TextAssets are not bound.");
            if (sprites == null || sprites.Length == 0)
                throw new InvalidOperationException("Official skill sprite bindings are empty.");
        }

        public OfficialSkillQualityGradientData LoadQualityGradient()
        {
            ValidateBindings();
            return OfficialSkillQualityGradientData.Parse(qualityGradient.text);
        }

        public static string NormalizeSpritePath(string sourceUri)
        {
            const string prefix = "res://assets/official/";
            if (string.IsNullOrEmpty(sourceUri) || !sourceUri.StartsWith(prefix, StringComparison.Ordinal))
                return string.Empty;
            return sourceUri.Substring(prefix.Length).Replace('\\', '/');
        }
    }
}
