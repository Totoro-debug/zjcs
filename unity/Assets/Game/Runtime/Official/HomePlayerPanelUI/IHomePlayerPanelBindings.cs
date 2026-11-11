using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;

namespace Zjcs.Official.HomePlayerPanelUI
{
    // Data and services consumed by the original controllers. No game rules live here.
    public interface IHomePlayerPanelBindings : IHomePlayerInfoBindings
    {
        new IHomePanelPlayer MainPlayer { get; }
        IProfessionTipDotSystem TipDotSystem { get; }
        float PlayerHpRedDotThreshold { get; }
        IHomePanelResourceLoader Resources { get; }
        DateTime GetDateTime();
        HomePanelItemInfo GetItemInfo(int id);
        HomePanelEntryInfo GetEntry(int id);
        string TextFormatted(string key, params object[] args);
        string GetSeasonLevelUnEffectDesc(int rank);
        string GetEntryNameKey(int id);
        string GetEntryDescKey(int id);
        string GetStatusEffectVaildShowScopesName(IHomePanelPlayer player, int classId);
        bool TryGetCookingBuffEntryDesc(IHomePanelPlayer player, int entryId, out string desc);
        void PopupEntries(UIBase owner, IReadOnlyList<HomeEntryDetailData> entries);
        void PopupEntry(UIBase owner, Func<HomeEntryDetailData> entry);
    }

    public interface IHomePanelPlayer : IHomePlayerInfoPlayer
    {
        long Id { get; }
        float HpPercent { get; }
        IReadOnlyCollection<int> Maps { get; }
        IReadOnlyDictionary<int, IHomePanelStatusEffect> StatusEffectManager { get; }
        long NoBlessCombatRating { get; }
        int MyPlanesRank { get; }
        bool HasCureItem();
        bool HasBlessLevel();
        event Action<float> OnHpUpdate;
        event Action<int, int> OnBlessLevelDataReset;
        event Action<long, long> OnNextRankCombatRatingChanged;
        event Action<IHomePanelStatusEffect> OnStatusEffectDataUpdate;
    }

    public interface IHomePanelPlayerBrief { long Id { get; } bool IsBlessLevelOpen(); }
    public interface IHomePanelStatusEffect
    {
        bool IsActive { get; }
        EStatusSourceType StatusSourceType { get; }
        IHomePanelStatusInfo Info { get; }
        DateTime LastGainedTime { get; }
        int GetDisplayRemainTimes();
    }
    public interface IHomePanelStatusInfo
    {
        StatusEffectIconType IconType { get; }
        int EntryId { get; }
        int ClassId { get; }
        int Duration { get; }
        bool IsReduceOncePerPlay { get; }
    }
    public interface IHomePanelResourceLoader : IHomeHudResourceLoader
    { IHomeHudAssetHandle<Sprite> LoadSprite(string path); }
    public enum HomePanelItemType { Other, Cooking }
    public sealed class HomePanelItemInfo { public HomePanelItemType ItemType; public object PartInfo; }
    public sealed class HomePanelCookingInfo { public CookingType Type; }
    public sealed class HomePanelEntryInfo { public string Icon, Bg; }

    // Original EntryDetailDialog payload, with the C# primary constructor expanded for Unity.
    public struct HomeEntryDetailData
    {
        public string IconSpritePath, BgSpritePath;
        public int StackCount;
        public HomePanelTextKeyArgWrap NameStr, DescStr;
        public Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> AdditionInfoDic;
        public HomeEntryDetailData(string spritePath, HomePanelTextKeyArgWrap nameStr, HomePanelTextKeyArgWrap descStr,
            string bgSpritePath = null, Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> additionInfoDic = null, int stackCount = 0)
        {
            IconSpritePath = spritePath; BgSpritePath = bgSpritePath; StackCount = stackCount;
            NameStr = nameStr; DescStr = descStr; AdditionInfoDic = additionInfoDic;
        }
    }
}
