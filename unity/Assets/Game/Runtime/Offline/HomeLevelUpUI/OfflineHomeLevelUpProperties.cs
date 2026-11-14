using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeLevelUpRules;
using Zjcs.Official.ProfessionUI;
using SubRank = Zjcs.Official.HomeExperienceRules.SubRank;
using RuleProfession = Zjcs.Official.HomeLevelUpRules.Profession;

namespace Zjcs.Offline.HomeLevelUpUI
{
    // Read the current persisted model for both evaluations. Do not apply the
    // full combat-property pipeline: the original notification uses only this formula.
    public sealed class OfflineHomeLevelUpProperties : IDisposable
    {
        private readonly Func<OfflinePlayerProgress> player;
        public HomeCharacterBattleTables Tables { get; }

        public OfflineHomeLevelUpProperties(Func<OfflinePlayerProgress> player, HomeCharacterBattleTables tables)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        public Dictionary<ProfessionPropertyType,long> Evaluate(int level)
        {
            var current = player() ?? throw new InvalidOperationException("The level-up player owner is unavailable.");
            var professions = current.ProfessionLevels.ToDictionary(row => (RuleProfession)row.Profession, row => row.Level);
            return Tables.Formula.CalcCharacterBattleProps((RuleProfession)current.Wardrobe.Profession,
                professions, level, (SubRank)current.SubRank).ToDictionary(row => (ProfessionPropertyType)row.Key, row => row.Value);
        }

        public void Dispose() => Tables.Dispose();

        public static OfflineHomeLevelUpProperties LoadDefault(Func<OfflinePlayerProgress> player)
        {
            var asset = Resources.Load<TextAsset>("Home/level-up-properties");
            if (!asset) throw new InvalidDataException("The selected level-up property configuration is unbound.");
            var source = JObject.Parse(asset.text);
            if ((string)source["manifest"] != "884_156692") throw new InvalidDataException("Wrong level-up property configuration version.");
            string path = Path.Combine(Application.streamingAssetsPath, "PartnerNpcShared", "Config", "Binary", "level_prop.g.bytes");
            byte[] bytes = File.ReadAllBytes(path);
            string actual;
            using (var hash = SHA256.Create()) actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (actual != (string)source["level_prop_sha256"]) throw new InvalidDataException("The selected shared level-property binary differs.");
            var tables = source["tables"];
            return new OfflineHomeLevelUpProperties(player, new HomeCharacterBattleTables(
                tables["game_settings"].ToObject<HomeBattleSettings>(), bytes,
                tables["profession_base"].ToObject<ProfessionBaseInfo[]>(),
                tables["role_prop_group"].ToObject<RolePropGroupInfo[]>(),
                tables["level_prop_default_value"].ToObject<LevelPropDefaultValue[]>()));
        }
    }
}
