using System;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Zjcs/Combat/Official Combat Asset Catalog")]
    public sealed class OfficialCombatAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Combat/OfficialCombatAssetCatalog";

        [SerializeField] private TextAsset skillRules;
        [SerializeField] private TextAsset skillSourceManifest;
        [SerializeField] private TextAsset battleRules;
        [SerializeField] private TextAsset battleSourceManifest;
        [SerializeField] private TextAsset rules;
        [SerializeField] private TextAsset rulesSourceManifest;

        public static OfficialCombatAssetCatalog LoadDefault()
        {
            OfficialCombatAssetCatalog catalog = Resources.Load<OfficialCombatAssetCatalog>(DefaultResourcePath);
            if (catalog == null)
                throw new InvalidOperationException("Official combat asset catalog is not installed at Resources/" + DefaultResourcePath + ".asset");
            return catalog;
        }

        public FightOfficialCombatCatalog Load()
        {
            if (loadedCatalog != null) return loadedCatalog;
            FightOfficialCombatSourceManifest skillSource = ReadManifest(skillSourceManifest, "skill-rules.json");
            FightOfficialCombatSourceManifest battleSource = ReadManifest(battleSourceManifest, "battle-rules.json");
            if (skillRules == null || battleRules == null)
                throw new InvalidOperationException("Official combat rule TextAssets are not bound.");

            loadedCatalog = FightOfficialCombatCatalog.Load(skillRules.text, skillSource.OutputSha256,
                skillSource.Manifest, battleRules.text, battleSource.OutputSha256, battleSource.Manifest);
            return loadedCatalog;
        }

        public FightDamageFormula LoadDamageFormula()
        {
            if (loadedFormula != null) return loadedFormula;
            if (rules == null) throw new InvalidOperationException("Official rules TextAsset is not bound.");
            FightOfficialCombatSourceManifest source = ReadManifest(rulesSourceManifest, "rules.json");
            loadedFormula = Load().CreateDamageFormula(rules.text, source.OutputSha256, source.Manifest);
            return loadedFormula;
        }

        public FightOfficialSkillItemCatalog LoadSkillItems()
        {
            if (loadedSkillItems != null) return loadedSkillItems;
            if (rules == null) throw new InvalidOperationException("Official rules TextAsset is not bound.");
            FightOfficialCombatSourceManifest source = ReadManifest(rulesSourceManifest, "rules.json");
            loadedSkillItems = FightOfficialSkillItemCatalog.Load(rules.text, source.OutputSha256,
                source.Manifest);
            return loadedSkillItems;
        }

        private FightOfficialCombatCatalog loadedCatalog;
        private FightDamageFormula loadedFormula;
        private FightOfficialSkillItemCatalog loadedSkillItems;

        public FightBattleExecutor BeginBattle(FightBattleCheckpoint checkpoint)
        {
            return FightBattleExecutor.Begin(checkpoint, LoadDamageFormula(), new FightDamageSettlement(),
                Load());
        }

        public FightBattleExecutor RestoreBattle(FightBattleCheckpoint checkpoint)
        {
            return FightBattleExecutor.Restore(checkpoint, LoadDamageFormula(), new FightDamageSettlement(),
                Load());
        }

        private FightStageStatusCatalog loadedStageStatus;

        public FightStageStatusCatalog LoadStageStatusCatalog()
        {
            if (loadedStageStatus == null)
            {
                if (rules == null) throw new InvalidOperationException("Official rules TextAsset is not bound.");
                loadedStageStatus = FightStageStatusCatalog.Parse(rules.text);
            }
            return loadedStageStatus;
        }

        private static FightOfficialCombatSourceManifest ReadManifest(TextAsset asset, string name)
        {
            if (asset == null) throw new InvalidOperationException("Official source manifest is not bound for " + name);
            FightOfficialCombatSourceManifest manifest = FightOfficialCombatCatalog.ReadSourceManifest(asset.text);
            if (string.IsNullOrEmpty(manifest.Manifest) || string.IsNullOrEmpty(manifest.OutputSha256))
                throw new InvalidOperationException("Official source manifest is invalid for " + name);
            return manifest;
        }
    }
}
