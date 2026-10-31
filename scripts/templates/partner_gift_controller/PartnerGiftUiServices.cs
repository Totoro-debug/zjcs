using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Projections retain only the official fields consumed by the gift controller.
    public sealed class PartnerGiftNpcInfo
    {
        public int Id, NpcRoleType;
        public bool IsStarGodNpc, CanCustomFriendshipTag;
        public PartnerFriendshipNpcInfo Properties;
        public Dictionary<int, double> GiftPreferenceDict;
    }
    public interface IPartnerGiftNpcData
    {
        int NpcClassId { get; }
        int FriendShipLevel { get; }
        long FriendShip { get; }
        string CustomFriendShipTag { get; }
    }
    public sealed class PartnerGiftInfo
    {
        public int ClassId, Friendship;
    }
    public sealed class PartnerGiftLevelInfo
    {
        public long FriendshipExp;
    }
    public sealed class PartnerGiftTagInfo
    {
        public int ClassId;
        public string TagBg;
    }
    public interface IPartnerGiftPlayer
    {
        int SubRank { get; }
        bool IsAddressListFunctionLimited { get; }
        IEnumerable<KeyValuePair<long, IPartnerGiftNpcData>> Npcs { get; }
        event Action<int, int> OnAddNewNpc;
        event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        event Action<int> OnNpcFriendshipUpdate;
        IPartnerGiftNpcData GetNpcByClassId(int npcId);
        IEnumerable<IProfessionItemData> GetAllCanSendToNpcGift(int npcId);
        long GetItemAmount(int itemId);
        void SetNpcFriendshipTag(int npcId, string tag);
    }
    public interface IPartnerGiftLevels
    {
        int GetMaxLevel(int npcId);
        int GetMaxSubRankLevel(int npcId, int subRank);
        int GetFriendshipLevelByExp(int npcId, long experience, int subRank);
        PartnerGiftLevelInfo GetMaxLevelInfo(int npcId);
        // Enumeration follows the original parser's configured level order.
        IReadOnlyDictionary<int, PartnerGiftLevelInfo> Get(int npcId);
        PartnerGiftLevelInfo Get(int npcId, int level);
    }
    public interface IPartnerGiftExperience
    {
        int GetAllExpByDict(Dictionary<int, long> gifts, Dictionary<int, double> preferences);
    }
    public interface IPartnerGiftInputDialog
    {
        GameObject GameObject { get; }
        void Init(string titleKey, string placeholderKey, Action<string> submit);
        void SetNpcFriendshipTagModerationClickMask();
    }
    public interface IPartnerGiftConfirmDialog
    {
        void Init(string textKey, Action confirm);
    }
    public interface IPartnerGiftFriendshipDialog
    {
        void Init(PartnerGiftNpcInfo npc);
    }
    public interface IPartnerGiftUiServices : IPartnerFriendshipUiServices
    {
        IPartnerGiftPlayer Player { get; }
        IProfessionUpgradeBindings PlayerBindings { get; }
        IPartnerGiftLevels Levels { get; }
        IPartnerGiftExperience Gifts { get; }
        double NpcGiftDefaultPreferenceFactor { get; }
        int CanCustomTagFriendShipLevel { get; }
        PartnerGiftNpcInfo GetNpcInfo(int npcId);
        PartnerGiftInfo GetGiftInfo(IProfessionItemInfo item);
        void ConfigureGiftIcon(ItemIconNpcGift icon);
        bool IsNpcGift(IProfessionItemInfo item);
        bool IsUnlockTravelNpcProp(ProfessionPropertyType property);
        PartnerGiftTagInfo GetTagInfoByRoleTypeAndLevel(int roleType, int level);
        string GetNpcFriendshipTagNameKey(int tagId);
        IProfessionAssetOperation<Sprite> LoadSprite(string path);
        bool IsAllCharacterValid(string input);
        int CheckNpcFriendshipTagLegal(string input);
        void ShowError(int error, object args, bool flag);
        string GetText(string key);
        void SystemMessage(string text);
        void PopupInput(Action<IPartnerGiftInputDialog> initialize);
        void PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize);
        void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize);
        object Compact(long count);
        void Close(GameObject window);
        // Success runs only after the original response has updated local state.
        void SetCustomTag(int npcId, string input, Action<string> success);
        void GiveGift(int npcId, Dictionary<int, long> gifts, Action success);
    }
    public static class PartnerGiftUiServices
    {
        // The original all-NPC blue dot reads the player during Awake. Its scene
        // scope must install this provider before instantiating that prefab.
        public static IPartnerGiftUiServices Current { get; set; }
        public static IPartnerGiftUiServices Required => Current ?? throw new InvalidOperationException("Official partner gift services are unbound.");
    }
}
