using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerDetailNpcData : IPartnerGiftNpcData
    {
        bool IsCollected { get; }
    }
    public sealed class PartnerDetailNpcInfo
    {
        public int Id, TreasureClassId;
        public bool IsStarGodNpc;
        public PartnerGiftNpcInfo Friendship;
    }
    public sealed class PartnerDetailNpcTarget
    {
        public int NpcClassId, NpcLevel, NpcSubRank, CreatePlanesRank;
    }
    public sealed class PartnerDetailLiteInfo
    {
        public int Level, SubRank;
        public PartnerDetailNpcTarget TargetData;
    }
    public sealed class PartnerDetailCollectedReply { public bool isCollected; }
    public sealed class PartnerDetailNavigateResult { public bool IsSuccess; public string TipMsg; }
    public interface IPartnerDetailPropertyServices : IPartnerFriendshipUiServices
    {
        bool IsPinFunctionLimited { get; }
        IReadOnlyDictionary<ProfessionPropertyType, long> CalcPlayerNpcProps(int npcId);
        bool IsUnlockTravelNpcProp(ProfessionPropertyType property);
        bool IsTreasureActive(int treasureId);
        double GetTreasureToNpcFriendshipPercent(int treasureId);
        bool CanNavigateNpc(int npcId);
        PartnerDetailNpcTarget CreateNpcTarget(int npcId);
        void SetCollected(int npcId, bool collected, Action<PartnerDetailCollectedReply, int> complete);
        void GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback);
        void PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target);
        void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize);
        void NavigateNpcPosition(int npcId, Action<PartnerDetailNavigateResult> complete);
        bool IsNavigateTipValid(string tip);
        void BroadcastNavigateTip(string tip);
        string GetText(string key);
        void SystemMessage(string text);
    }
}
