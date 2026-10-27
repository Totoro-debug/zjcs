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
        [SerializeField] private TextAsset monsterHpPlies;
        [SerializeField] private TextAsset monsterHpPliesSourceManifest;
        [SerializeField] private TextAsset reportScoreSettings;
        [SerializeField] private TextAsset reportScoreSettingsSourceManifest;

        public static OfficialCombatAssetCatalog LoadDefault()
        {
            OfficialCombatAssetCatalog catalog = Resources.Load<OfficialCombatAssetCatalog>(DefaultResourcePath);
            if (catalog == null)
                throw new InvalidOperationException("Official combat asset catalog is not installed at Resources/" + DefaultResourcePath + ".asset");
            return catalog;
        }

        public TextAsset Rules => rules;
        public TextAsset RulesSourceManifest => rulesSourceManifest;

        public FightOfficialCombatCatalog Load()
        {
            if (loadedCatalog != null) return loadedCatalog;
            FightOfficialCombatSourceManifest skillSource = ReadManifest(skillSourceManifest, "skill-rules.json");
            FightOfficialCombatSourceManifest battleSource = ReadManifest(battleSourceManifest, "battle-rules.json");
            FightOfficialCombatSourceManifest hpPliesSource = ReadManifest(
                monsterHpPliesSourceManifest, "monster-hp-plies.json");
            if (skillRules == null || battleRules == null)
                throw new InvalidOperationException("Official combat rule TextAssets are not bound.");
            if (monsterHpPlies == null)
                throw new InvalidOperationException("Official monster HP plies TextAsset is not bound.");

            loadedCatalog = FightOfficialCombatCatalog.Load(skillRules.text, skillSource.OutputSha256,
                skillSource.Manifest, battleRules.text, battleSource.OutputSha256, battleSource.Manifest,
                monsterHpPlies.text, hpPliesSource.OutputSha256, hpPliesSource.Manifest);
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

        public FightBattleReportScoreSettings LoadReportScoreSettings()
        {
            if (loadedReportScoreSettings != null) return loadedReportScoreSettings;
            if (reportScoreSettings == null)
                throw new InvalidOperationException("Official report score settings TextAsset is not bound.");
            FightOfficialCombatSourceManifest source = ReadManifest(reportScoreSettingsSourceManifest,
                "report-score-settings.json");
            loadedReportScoreSettings = FightBattleReportScoreSettings.Load(reportScoreSettings.text,
                source.OutputSha256, source.Manifest);
            return loadedReportScoreSettings;
        }

        private FightOfficialCombatCatalog loadedCatalog;
        private FightDamageFormula loadedFormula;
        private FightOfficialSkillItemCatalog loadedSkillItems;
        private FightBattleReportScoreSettings loadedReportScoreSettings;
        private FightOfficialNpcAssistanceCatalog loadedNpcAssistance;

        public FightOfficialNpcAssistanceCatalog LoadNpcAssistance()
        {
            if (loadedNpcAssistance != null) return loadedNpcAssistance;
            TextAsset npcRules = Resources.Load<TextAsset>("DailyChallenges/npc-assistance-rules");
            TextAsset npcSourceManifest = Resources.Load<TextAsset>(
                "DailyChallenges/npc-assistance-rules-source-manifest");
            FightOfficialCombatSourceManifest source = ReadManifest(npcSourceManifest,
                "npc-assistance-rules.json");
            if (npcRules == null)
                throw new InvalidOperationException("Official NPC assistance rules are missing from Resources.");
            loadedNpcAssistance = FightOfficialNpcAssistanceCatalog.Load(npcRules.text,
                source.OutputSha256, source.Manifest, Load(), LoadSkillItems(), LoadDamageFormula());
            return loadedNpcAssistance;
        }

        public FightBattleExecutor BeginBattle(FightBattleCheckpoint checkpoint)
        {
            return FightBattleExecutor.Begin(checkpoint, LoadDamageFormula(), new FightDamageSettlement(),
                Load(), LoadSkillItems());
        }

        public FightBattleExecutor RestoreBattle(FightBattleCheckpoint checkpoint)
        {
            return FightBattleExecutor.Restore(checkpoint, LoadDamageFormula(), new FightDamageSettlement(),
                Load(), LoadSkillItems());
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
