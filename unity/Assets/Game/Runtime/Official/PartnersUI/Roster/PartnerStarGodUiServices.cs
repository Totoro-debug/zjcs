using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    [Serializable]
    public sealed class PartnerStarGodNpcDefinition
    {
        public int Id;
        public bool IsStarGodNpc, IsMapExploreBoss;
        public string NameKey, LocationDescKey, StaticFullBodyImagePath;
    }

    // Explicit dependencies consumed by the original star-god page. The host
    // resolves the selected region image and preserves the player's NPC identity.
    public interface IPartnerStarGodUiServices
    {
        bool MainPlayerAvailable { get; }
        bool CanPinNpc { get; }
        bool IsStarGodNpcDisabled { get; }
        IEnumerable<PartnerStarGodNpcDefinition> Definitions { get; }
        IEnumerable<NpcRosterItem> OwnedNpcs { get; }
        event Action<int, bool> NpcCollectedChanged;
        event Action<int, NpcMeetType> NpcAdded;
        event Action<int, int, int> NpcFriendshipLevelUpdated;
        PartnerStarGodNpcDefinition GetDefinition(int npcClassId);
        NpcRosterItem GetNpc(int npcClassId);
        NpcRosterItem CreatePreviewNpc(PartnerStarGodNpcDefinition definition, int level);
        NpcMeetStatus MeetStatusOf(int npcClassId);
        void BindNpcTipDot(TipDotNodeLinker linker, int npcClassId);
        IProfessionAssetOperation<Sprite> LoadSprite(string path);
        void PostSound(AudioClip clip, GameObject owner);
        void RequestMeetNpc(int npcClassId);
        void ShowFriendshipLevelUp(NpcRosterItem npc, long oldFriendship, int oldLevel);
        void ShowStarGodDetails(List<NpcRosterItem> npcs, int index);
        void ShowError(int code);
        List<(int, int)> GetMeetNpcItems(int npcClassId);
        long GetItemAmount(int itemClassId);
        void PrepareItemIcon(ItemIcon icon);
    }
}
