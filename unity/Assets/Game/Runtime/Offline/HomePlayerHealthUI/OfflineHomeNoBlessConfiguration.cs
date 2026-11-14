using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeNoBlessRules;
using Limits = Zjcs.Official.HomeNoBlessRules.SystemLevelLimitInfo;
using LimitsParser = Zjcs.Official.HomeNoBlessRules.SystemLevelLimitInfoParser;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    public sealed class OfflineHomeNoBlessConfiguration : INoBlessConfiguration
    {
        public LimitsParser LevelLimits { get; }
        public EquipResonanceInfoParser Resonances { get; }
        private readonly AstrologicalSeasonConfigParser seasons;
        public int LimitRowCount { get; }
        public int GetSeasonByRank(Rank rank) => seasons.GetSeasonByRank(rank);

        public OfficialCharacterCatalog CreatePropertyCatalog(OfficialCharacterCatalog baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            var asset = Resources.Load<TextAsset>("Home/no-bless-property-chunks");
            var receipt = Resources.Load<TextAsset>("Home/no-bless-property-chunks-hash");
            if (!asset || !receipt) throw new InvalidDataException("The selected home property chunks are unbound.");
            var hash = JObject.Parse(receipt.text);
            if ((string)hash["manifest"] != "884_156692") throw new InvalidDataException("Wrong home property chunk version.");
            return baseline.WithHomePropertyRules(asset.text, (string)hash["sha256"]);
        }

        private OfflineHomeNoBlessConfiguration(JObject source)
        {
            if ((string)source["manifest"] != "884_156692")
                throw new InvalidDataException("Wrong no-blessing configuration version.");
            var rows = source["limits"].ToObject<Limits[]>();
            if (rows == null || rows.Length == 0) throw new InvalidDataException("Missing system level limits.");
            LimitRowCount = rows.Length;
            LevelLimits = new LimitsParser(rows);
            Resonances = new EquipResonanceInfoParser(source["resonances"].ToObject<EquipResonanceInfo[]>());
            seasons = new AstrologicalSeasonConfigParser(source["seasons"].ToObject<AstrologicalSeasonConfig[]>());
        }

        public static OfflineHomeNoBlessConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/no-bless-properties");
            if (!asset) throw new InvalidDataException("The selected no-blessing tables are unbound.");
            return new OfflineHomeNoBlessConfiguration(JObject.Parse(asset.text));
        }
    }
}
