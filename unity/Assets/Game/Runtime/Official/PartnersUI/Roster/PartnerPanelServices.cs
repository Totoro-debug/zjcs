using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerPanelServices
    {
        bool IsFunctionLimit(int functionType);
        void InitDialogueList(GameObject owner);
    }
    internal static class NpcPanelTabs
    {
        public static readonly IReadOnlyDictionary<NpcPanelTabType, int> Tab2FunctionTypeDict =
            new Dictionary<NpcPanelTabType, int> { [NpcPanelTabType.NpcList] = 65, [NpcPanelTabType.Dialogue] = 57, [NpcPanelTabType.StarGod] = 174 };
    }
}
