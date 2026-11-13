using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Offline.HomeExperienceUI
{
    [Serializable]
    public sealed class OfflineHomeExperienceConfiguration
    {
        public string manifest;
        public int playerExpItemId;
        public Rank firstBlessRank;
        public Dictionary<string, object> tables;

        public HomeExperienceTables CreateTables()
        {
            if (manifest != "884_156692" || playerExpItemId != 50 || firstBlessRank != Rank.Bronze || tables == null)
                throw new InvalidDataException("The selected original home experience configuration is unbound.");
            return new HomeExperienceTables(Rows<LevelExp>("level_exp"), Rows<LevelBlessInfo>("level_bless"),
                Rows<SystemLevelLimitInfo>("system_level_limit"), Rows<AstrologicalSeasonConfig>("astrological_season_config"), playerExpItemId, firstBlessRank);
        }

        public T[] Rows<T>(string name)
        {
            if (!tables.TryGetValue(name, out var rows)) throw new InvalidDataException("Missing home experience table: " + name);
            return JsonConvert.DeserializeObject<T[]>(rows.ToString());
        }

        public static OfflineHomeExperienceConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/experience-rules");
            if (!asset) throw new InvalidDataException("The selected original home experience tables are missing.");
            return JsonConvert.DeserializeObject<OfflineHomeExperienceConfiguration>(asset.text);
        }
    }
}
