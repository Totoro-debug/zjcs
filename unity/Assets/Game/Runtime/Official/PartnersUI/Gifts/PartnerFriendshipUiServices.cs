using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Original NpcInfo fields consumed by the property rows. Values come from
    // the selected NPC configuration; this type supplies no defaults or rules.
    public sealed class PartnerFriendshipNpcInfo
    {
        public int Id, LevelPropId;
    }

    public interface IPartnerFriendshipUiServices
    {
        IProfessionPropertyBindings Properties { get; }
        List<ProfessionPropertyType> TravelNpcPropTypeList { get; }
        bool IsTravelFunctionLimited { get; }
        IReadOnlyDictionary<ProfessionPropertyType, long> GetFriendshipLevelPropDiff(
            PartnerFriendshipNpcInfo npc, int fromLevel, int toLevel);
        IReadOnlyDictionary<ProfessionPropertyType, long> GetLevelProps(int levelPropId, int level);
    }
}
