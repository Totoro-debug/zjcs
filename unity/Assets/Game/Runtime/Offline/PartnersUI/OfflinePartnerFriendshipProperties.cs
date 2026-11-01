using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Shared current-data provider for the original gift rows and friendship dialog.
    public sealed class OfflinePartnerFriendshipProperties : IPartnerFriendshipUiServices
    {
        private readonly OfficialPartnerCatalog catalog;
        private readonly Func<bool> travelFunctionLimited;
        public IProfessionPropertyBindings Properties { get; }
        public List<ProfessionPropertyType> TravelNpcPropTypeList { get; }
        public bool IsTravelFunctionLimited => travelFunctionLimited();

        public OfflinePartnerFriendshipProperties(OfficialPartnerCatalog catalog,
            IProfessionPropertyBindings properties, Func<bool> travelFunctionLimited)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.travelFunctionLimited = travelFunctionLimited ?? throw new ArgumentNullException(nameof(travelFunctionLimited));
            var settings = (Dictionary<string, object>)catalog.Rows("game_settings").Single()["NpcAdventureSettings"];
            TravelNpcPropTypeList = ((List<object>)settings["TravelNpcPropTypeList"])
                .Select(value => (ProfessionPropertyType)Convert.ToInt32(value)).ToList();
        }

        public IReadOnlyDictionary<ProfessionPropertyType, long> GetFriendshipLevelPropDiff(
            PartnerFriendshipNpcInfo npc, int fromLevel, int toLevel)
        {
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            return ConvertProperties(catalog.GetFriendshipLevelPropDiff(npc.LevelPropId, fromLevel, toLevel));
        }

        public IReadOnlyDictionary<ProfessionPropertyType, long> GetLevelProps(int levelPropId, int level)
            => ConvertProperties(catalog.GetLevelProps(levelPropId, level));

        private static IReadOnlyDictionary<ProfessionPropertyType, long> ConvertProperties(IReadOnlyDictionary<int, long> values)
            => values?.ToDictionary(value => (ProfessionPropertyType)value.Key, value => value.Value);
    }
}
