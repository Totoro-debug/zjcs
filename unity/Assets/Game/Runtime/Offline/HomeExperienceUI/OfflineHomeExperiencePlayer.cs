using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRewards;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Offline.HomeExperienceUI
{
    // Supplied by the owning scene's real season/function/property services.
    // Player rank is deliberately not substituted for the world plane rank.
    public sealed class OfflineHomeExperienceEnvironment
    {
        public Rank PlanesRank;
        public int ServerRecordSeason;
        public HomeExperienceSeasonRecord CurrentRecordData;
        public bool PlayerExpLimitFunctionLimited;
        public double PlayerLevelLimitProperty;
    }

    // A committed profile projection. The owner synchronizes after transactions
    // or reloads; merely opening the HUD never rewrites canonical growth data.
    public sealed class OfflineHomeExperiencePlayer : IHomeExperienceRewardPlayer, IDisposable
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly Func<OfflineHomeExperienceEnvironment> environment;
        private HomeExperiencePlayer observed;
        private bool disposed;
        public HomeExperienceTables Tables { get; }
        public int LevelLimit => Current.LevelLimit;
        public int Rank => (int)Current.Rank;
        public int BaseLevel => Current.BaseLevel;
        public int Level => Current.Level;
        public int SubRank => (int)Current.SubRank;
        public int PlanesRank => (int)Current.PlanesRank;
        public event Action<HomeExperienceItem, long, object> OnItemCountUpdate;
        public event Action<int> OnSubRankUpdate;
        public event Action OnBlessLevelDataUpdate;
        public event Action<object> OnSeasonRecordDataAdd;
        public event Action<object> OnSeasonRecordDataEndUpdate;
        public event Action<int> OnLevelUpdate;
        public event Action<long, long> OnBlessExpUpdate;
        public event Action<int, int> OnBlessLevelUp;

        public OfflineHomeExperiencePlayer(OfflineMainlineProgressSession profile, HomeExperienceTables tables,
            Func<OfflineHomeExperienceEnvironment> environment)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            observed = Capture();
        }

        private HomeExperiencePlayer Current => disposed ? throw new ObjectDisposedException(nameof(OfflineHomeExperiencePlayer)) : observed;
        public bool IsBlessLevelOpen() => Current.IsBlessLevelOpen();
        public bool ShowPlayerStoreExpLimit() => Current.ShowPlayerStoreExpLimit();
        public int GetMaxStoreExpLevel() => Current.GetMaxStoreExpLevel();
        public bool IsMaxBlessLevel() => Current.IsMaxBlessLevel();
        public long GetCurBlessPlayerExp() => Current.GetCurBlessPlayerExp();
        public int GetCurBlessPlayerLevel() => Current.GetCurBlessPlayerLevel();
        public long GetCurrentExp() => Current.GetCurrentExp();
        public long GetExpRequestByBlessLevel(int level) => Current.GetExpRequestByBlessLevel(level);
        public long GetItemAmount(int itemId)
        {
            var current = Current;
            return itemId == Tables.PlayerExpItemId ? current.Experience : profile.InventoryCount(itemId);
        }

        private HomeExperiencePlayer Capture()
        {
            var saved = profile.Save.Player;
            var state = environment() ?? throw new InvalidOperationException("The owning scene must bind real home experience environment data.");
            var player = new HomeExperiencePlayer(Tables)
            {
                SubRank = (SubRank)saved.SubRank, BaseLevel = saved.SeasonBaseLevel, Level = saved.Level,
                Experience = saved.Experience, ServerRecordSeason = state.ServerRecordSeason, PlanesRank = state.PlanesRank,
                CurrentRecordData = state.CurrentRecordData == null ? null : new HomeExperienceSeasonRecord
                    { Season = state.CurrentRecordData.Season, RecordRank = state.CurrentRecordData.RecordRank },
                PlayerExpLimitFunctionLimited = state.PlayerExpLimitFunctionLimited,
                PlayerLevelLimitProperty = state.PlayerLevelLimitProperty
            };
            // The local SeasonRank is a plane-rank key, not a season ID.
            // Resolve it through the selected official Rank -> Season parser.
            int season = Tables.AstrologicalSeasonConfigParser.GetSeasonByRank((Rank)saved.SeasonRank);
            if (saved.HasActiveSeasonRecord && season > 0)
                player.BlessRecords.Add(season, new HomeExperienceBlessRecord { PlayerBlessExp = saved.BlessingExperience });
            player.TryRefreshLevel();
            return player;
        }

        public void SynchronizeCommittedPlayer()
        {
            var before = Current;
            var after = Capture();
            observed = after;
            // Ordinary XP: original Player.TryRefreshLevel precedes item events.
            // Bless XP: AddBlessExp/MsgPlayerBlessLevelUpdate issue level updates,
            // then the combined ordinary+bless XP event consumed by the handler.
            bool blessChanged = before.GetCurBlessPlayerExp() != after.GetCurBlessPlayerExp();
            if (blessChanged) OnBlessLevelDataUpdate?.Invoke();
            if (before.Level != after.Level) OnLevelUpdate?.Invoke(after.Level);
            int previousBlessLevel = before.GetCurBlessPlayerLevel(), blessLevel = after.GetCurBlessPlayerLevel();
            if (previousBlessLevel != blessLevel)
            {
                OnBlessLevelUp?.Invoke(previousBlessLevel, blessLevel);
                // AddBlessExp explicitly fires Level again after the blessing
                // level callback, in addition to TryRefreshLevel's notification.
                if (blessChanged) OnLevelUpdate?.Invoke(after.Level);
            }
            if (before.SubRank != after.SubRank) OnSubRankUpdate?.Invoke((int)after.SubRank);
            if (before.Experience != after.Experience)
                OnItemCountUpdate?.Invoke(new HomeExperienceItem { Id = Tables.PlayerExpItemId }, before.Experience, null);
            if (blessChanged)
            {
                long currentExp = after.GetCurrentExp();
                OnBlessExpUpdate?.Invoke(currentExp - (after.GetCurBlessPlayerExp() - before.GetCurBlessPlayerExp()), currentExp);
                OnBlessLevelDataUpdate?.Invoke();
            }
            bool hadRecord = before.TryGetSeasonBlessData(before.ServerRecordSeason, out _);
            bool hasRecord = after.TryGetSeasonBlessData(after.ServerRecordSeason, out _);
            if (hadRecord && (!hasRecord || before.ServerRecordSeason != after.ServerRecordSeason))
                OnSeasonRecordDataEndUpdate?.Invoke(before.CurrentRecordData);
            if (hasRecord && (!hadRecord || before.ServerRecordSeason != after.ServerRecordSeason))
                OnSeasonRecordDataAdd?.Invoke(after.CurrentRecordData);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; observed = null;
            OnItemCountUpdate = null; OnSubRankUpdate = null; OnBlessLevelDataUpdate = null;
            OnSeasonRecordDataAdd = null; OnSeasonRecordDataEndUpdate = null; OnLevelUpdate = null;
            OnBlessExpUpdate = null; OnBlessLevelUp = null;
        }
    }
}
