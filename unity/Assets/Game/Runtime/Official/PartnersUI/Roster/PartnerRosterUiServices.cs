using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerNpcDefinition
    {
        public int Id, ExclusiveGiftId;
        public NpcRoleType NpcRoleType;
        public string NameKey, FigureImage, SmallBgImage;
    }

    public readonly struct PartnerMeetProgress
    {
        public readonly long Current, Target;
        public PartnerMeetProgress(long current, long target) { Current = current; Target = target; }
    }

    // Consumed calls from the original player, parser, resource loader and window
    // services. No UI hierarchy or layout is produced by this boundary.
    public interface IPartnerRosterUiServices
    {
        bool MainPlayerAvailable { get; }
        bool IsNormalNpcDisabled { get; }
        bool CanPinNpc { get; }
        int MetNpcCount { get; }
        event Action<int, NpcMeetType> NpcAdded;
        event Action<int, bool> NpcCollectedChanged;
        event Action<int> NpcRemoved;
        event Action<int> NpcFriendshipUpdated;
        IReadOnlyList<NpcRosterItem> NpcRoster(bool showAll);
        NpcRosterItem GetNpc(int npcClassId);
        PartnerNpcDefinition GetDefinition(int npcClassId);
        NpcMeetStatus MeetStatusOf(int npcClassId);
        PartnerMeetProgress? MeetProgress(int npcClassId);
        bool HasItemDefinition(int itemId);
        int FriendshipMaxLevel(int npcClassId);
        long FriendshipExp(int npcClassId, int level);
        IProfessionAssetOperation<Sprite> LoadSprite(string path);
        void BindNpcTipDot(TipDotNodeLinker linker, int npcClassId);
        void PostSound(AudioClip sound, GameObject owner);
        Task<bool> MeetNpc(int npcClassId);
        void ShowFriendshipLevelUp(NpcRosterItem npc, long oldFriendship, int oldLevel);
        void ShowExclusiveGift(int itemId, Component owner);
        void ShowNpcDetails(List<NpcRosterItem> npcs, int index);
        void ShowError(int code);
    }
}
