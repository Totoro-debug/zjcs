using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Only the application calls consumed by the three original controllers.
    // Configurations, player state, transactions and child panels are supplied
    // explicitly; this boundary contains no replacement profession rules.
    public sealed class ProfessionDetailDefinition
    {
        public int Profession, Rank, PrePro, BadgeClassId;
        public string TutorEntityTag;
        public List<int> Skills;
    }
    public interface IProfessionDetailSkill
    {
        int Id { get; }
        int GetRank();
    }
    public interface IProfessionDetailPlayer
    {
        int Profession { get; }
        int GetProfessionLevel(int profession);
        int GetLastRewardedSkillRank(int skill);
        IReadOnlyCollection<IProfessionDetailSkill> FindSkillItems();
        event Action<int, int> OnProfessionUpgrade;
    }
    public interface IProfessionDetailInfoPanel
    {
        void Init(int profession, GameObjectPool<ProfessionPlayerPropItem> propPool);
    }
    public interface IProfessionDetailUpgradePanel { void Init(int profession); }
    public interface IProfessionDetailBindings
    {
        IProfessionDetailPlayer MainPlayer { get; }
        IProfessionImageResourceLoader ResourceLoader { get; }
        int HighestRank { get; }
        ProfessionDetailDefinition GetProfession(int profession);
        List<int> GetSuccessor(int profession);
        string GetSecondIconPath(int profession);
        string GetFullBodyGuid(int profession, Sex sex);
        string GetProfessionNameKey(int profession);
        Color GetPanelFadeColor(int profession);
        Color GetPanelLineColor(int profession);
        bool HasLevelCost(int profession, int level);
        bool IsProfessionTutorUnlock(int profession);
        string GetTutorHeadGuid(int profession);
        bool TryGotoTutor(string uniqueEntitySource);
        void ShowSystemMessage(string languageKey);
        int GetRankBadge(int rank);
        void ClaimSkillRankReward(List<int> skillIds, Action<Dictionary<int, int>> onSuccess);
        void PopupSkillBadge(UIBase container, List<(int itemId, int rank)> ranks, int badgeId, int amount);
    }
    public static class ProfessionDetailBindings
    {
        public static IProfessionDetailBindings Current { get; set; }
        public static IProfessionDetailBindings Required => Current ??
            throw new InvalidOperationException("Original profession detail services are unbound.");
    }
}
