using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionUpgradeCostDefinition
    {
        public Dictionary<int, long> UpgradeCost;
    }

    public interface IProfessionUpgradePlayer
    {
        int Profession { get; }
        bool IsProfessionFunctionLimit { get; }
        bool IsProfessionUnlocked(int profession);
        int GetProfessionLevel(int profession);
        long GetItemAmount(int itemId);
        event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        event Action<int, int> OnProfessionUpgrade;
        event Action<int, int> OnSkillRankRewardUpdate;
        event Action<IProfessionItemData, int, int> OnSkillRankUp;
        event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
    }

    // Consumed original application calls only. Transactions, formulas, typed
    // property text and production player state are supplied by the host.
    public interface IProfessionUpgradeBindings
    {
        IProfessionUpgradePlayer MainPlayer { get; }
        bool ManagerAvailable { get; }
        SafeAction OnPlayerUpdate { get; }
        IProfessionTipDotSystem TipDotSystem { get; }
        ProfessionDetailDefinition GetProfession(int profession);
        int GetLevelPropId(int profession);
        bool TryGetLevelInfo(int profession, int level, out ProfessionUpgradeCostDefinition info);
        IReadOnlyDictionary<int, long> GetProps(int levelPropId, int level);
        Dictionary<int, long> GetProfessionLevelPropDiff(int profession, int fromLevel, int toLevel);
        string GetPropNameText(int propType);
        string FormatPropValue(int propType, long value);
        void InitPropItem(ProfessionPlayerPropItem item, int propType, long value, long nextValue);
        int GetItemType(IProfessionItemInfo item);
        long CalcAwardBadge(ProfessionDetailDefinition profession, IProfessionUpgradePlayer player);
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        void Upgrade(int profession, Action onSuccess);
        int MoneyId { get; }
        void ShowLackOfItem(int itemId, long amount);
        bool IsItemSourceFromMerge(IProfessionUpgradePlayer player, int itemId);
        long CalcCanMergeAmount(IProfessionUpgradePlayer player, int itemId, bool recursion);
    }

    public static class ProfessionUpgradeBindings
    {
        public static IProfessionUpgradeBindings Current { get; set; }
        public static IProfessionUpgradeBindings Required => Current ??
            throw new InvalidOperationException("Original profession upgrade services are unbound.");
    }
}
