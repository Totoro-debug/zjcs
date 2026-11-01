using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerDetailNpcData : IPartnerDetailNpcData
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly IPartnerGiftNpcData gift;
        public OfflinePartnerDetailNpcData(OfflineMainlineProgressSession session, IPartnerGiftNpcData gift)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.gift = gift ?? throw new ArgumentNullException(nameof(gift));
        }
        public int NpcClassId => gift.NpcClassId;
        public int FriendShipLevel => gift.FriendShipLevel;
        public long FriendShip => gift.FriendShip;
        public string CustomFriendShipTag => gift.CustomFriendShipTag;
        public bool IsCollected => session.Save.Player.CharacterProperties.NpcFriendships
            .FirstOrDefault(value => value.NpcClassId == NpcClassId)?.IsCollected == true;
    }
    // Actual saved friendship / talent state supplies the selected official formula.
    public sealed class OfflinePartnerDetailPropertySource : IPartnerNpcPropertySource, IPartnerNpcPropertyPlayer
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialPartnerCatalog catalog;
        private readonly OfflinePartnerFriendshipProperties properties;
        private readonly PartnerNpcPropertyFormula formula;

        public OfflinePartnerDetailPropertySource(OfflineMainlineProgressSession session,
            OfficialPartnerCatalog catalog, OfflinePartnerFriendshipProperties properties)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            formula = new PartnerNpcPropertyFormula(this);
        }
        public IReadOnlyDictionary<ProfessionPropertyType, long> CalcPlayerNpcProps(int npcId)
            => formula.CalcPlayerNpcProps(this, npcId);
        public IReadOnlyDictionary<int, int> GetNpcFriendshipLevelDict()
            => session.Save.Player.CharacterProperties.NpcFriendships.ToDictionary(value => value.NpcClassId, value => value.Level);
        public IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int npcId)
            => session.Save.Player.CharacterProperties.NpcTalentTrees.FirstOrDefault(value => value.NpcClassId == npcId)?.ActiveNodeIds
                ?? (IReadOnlyCollection<int>)Array.Empty<int>();
        public PartnerNpcPropertyInfo GetNpc(int npcId)
        {
            var row = catalog.Rows("npc").FirstOrDefault(value => Convert.ToInt32(value["Id"]) == npcId);
            return row == null ? null : new PartnerNpcPropertyInfo { LevelPropId = Convert.ToInt32(row["LevelPropId"]) };
        }
        public PartnerNpcTalentPropertyInfo GetNodeMapPropInfo(int npcId, int nodeId)
        {
            var row = catalog.Rows("npc_talent_tree_npc_map_prop").FirstOrDefault(value => Convert.ToInt32(value["NpcClassId"]) == npcId
                && Convert.ToInt32(value["NodeId"]) == nodeId);
            return row == null ? null : new PartnerNpcTalentPropertyInfo {
                PropId = Convert.ToInt32(row["PropId"]), PropLevel = Convert.ToInt32(row["PropLevel"]),
                PropTypes = ((List<object>)row["PropTypes"]).Select(value => (ProfessionPropertyType)Convert.ToInt32(value)).ToList() };
        }
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetProps(int propId, int level)
            => properties.GetLevelProps(propId, level);
    }
}
