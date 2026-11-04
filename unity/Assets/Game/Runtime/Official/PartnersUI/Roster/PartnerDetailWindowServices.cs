using System;
using UnityEngine;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerDetailWindowNpcData : IPartnerDetailNpcData, IPartnerProfileNpcData
    {
        new int NpcClassId { get; }
    }
    public interface IPartnerDetailWindowServices
    {
        event Action<int> OnRemoveNpc;
        IPartnerDetailWindowNpcData GetNpcByClassId(int npcId);
        PartnerDetailPresentationInfo GetNpcInfo(int npcId);
        void Close(GameObject window);
    }
}
