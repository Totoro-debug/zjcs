using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Consumed original parser/player/pool/window services. No profession or
    // skill rules are implemented at this boundary.
    public interface IProfessionInfoSkill : IProfessionItemInfo
    {
        int ItemType { get; }
        int Rank { get; }
    }
    public sealed class ProfessionSkillRankDefinition
    {
        public int RankAddition;
        public Quality Quality;
    }
    public interface IProfessionSkillParameter
    {
        int GetRank();
        int SkillSkinId { get; }
    }
    public interface IProfessionSkillIconPlayer
    {
        bool HasItem(int id, long amount);
        IProfessionItemData GetItemById(int id);
        bool ContainsItemAtIndex(IProfessionItemData item);
        bool IsSkillAvailableForCurProfession(int id);
        event Action<IProfessionItemData, int, int> OnSkillRankUp;
        event Action<IProfessionItemData, int, int> OnSkillRankDown;
        event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
        event Action<int, int> OnSkillSkillEquiped;
    }
    public interface IProfessionSkillIconBindings
    {
        IProfessionSkillIconPlayer MainPlayer { get; }
        IProfessionSkillParameter GetItemParamSkill(IProfessionItemData item);
        ProfessionSkillRankDefinition GetRank(int rank);
        int GetMaxRankByQuality(Quality quality);
        int GetDebugProfessionRank(int itemId);
        bool DebugShowSkillRank { get; }
        event Action DebugShowSkillRankChanged;
        bool IsSkillAvailableForProfession(int itemId, int profession);
        bool IsPetSkill(IProfessionItemInfo item);
        bool IsPlayerTarget(int targetType);
        bool IsSkillGMDisabled(int itemId);
        bool HasSkillSkin(int skinId);
        IProfessionItemInfo GetItemInfo(int itemId);
        void PopupSkillDetail(IProfessionItemData data, object propDataProvider);
        void PopupSkillDetail(int itemId, long count);
        ItemIconSkill GetIcon(int itemId, Transform slot);
        ItemIconSkill GetIcon(IProfessionItemData item, Transform slot);
        void RecycleIcon(ItemIconSkill icon);
    }
    public interface IProfessionInfoBindings
    {
        IProfessionImageResourceLoader ResourceLoader { get; }
        ProfessionDetailDefinition GetProfession(int profession);
        string GetProfessionDescKey(int profession);
        List<int> GetSupportWeaponTypes(int profession);
        List<int> GetSupportSecondWeaponTypes(int profession);
        string GetEquipTypeIcon(int itemType);
        string GetEquipTypeNameKey(int itemType);
        IProfessionInfoSkill GetItemInfo(int itemId);
        IReadOnlyDictionary<int, long> GetFeatureProps(int profession);
        Color GetRadarColor(int profession);
        void InitPropItem(ProfessionPlayerPropItem item, int propType, long value);
    }
    public static class ProfessionInfoBindings
    {
        public static IProfessionInfoBindings Current { get; set; }
        public static IProfessionInfoBindings Required => Current ??
            throw new InvalidOperationException("Original profession information services are unbound.");
    }
    public static class ProfessionSkillIconBindings
    {
        public static IProfessionSkillIconBindings Current { get; set; }
        public static IProfessionSkillIconBindings Required => Current ??
            throw new InvalidOperationException("Original profession skill-icon services are unbound.");
    }
}
