using System;
using Zjcs.Official.HomeExperienceRewards;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Rules = Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Offline.HomeExperienceUI
{
    // Console presentation state is scoped to the owner, separate from saved XP.
    public sealed class OfflineHomeExperienceStore : IHomeExperienceRewardStore
    {
        public bool Inited { get; }
        public long HandledExp { get; set; }
        public int HandledLevel { get; set; }
        public OfflineHomeExperienceStore(OfflineHomeExperiencePlayer player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            // Original ConsoleDataManager.Init snapshots current player values.
            Inited = true; HandledLevel = player.Level; HandledExp = player.GetCurrentExp();
        }
    }

    public sealed class OfflineHomeExperienceServices : IHomeExperienceServices, IHomeExperienceRewardBindings, IDisposable
    {
        private readonly Action<int> notification;
        private readonly Func<long, object> compact;
        private readonly Func<int, string> seasonName;
        private readonly Action<int> resolveSubRank;
        private bool disposed;
        public OfflineHomeExperiencePlayer PlayerData { get; }
        public HomeExperienceRewardHandler Handler { get; }
        public IHomeExperiencePlayer MainPlayer => disposed ? null : PlayerData;
        public IHomeExperienceRewardPlayer Player => disposed ? null : PlayerData;
        public bool ManagerAvailable => !disposed;
        public SafeAction OnPlayerUpdate { get; } = new SafeAction();
        public IHomeLevelExpParser LevelExpParser { get; }
        public IHomeLevelBlessParser LevelBlessInfoParser { get; }
        public IHomeExperienceRewards Rewards => Handler;
        public IHomeExperienceRewardStore Store { get; }
        public IHomeExperienceCombatRatingBlockers CombatRatingBlockers { get; }
        public int PlayerExpItemId => PlayerData.Tables.PlayerExpItemId;
        public int PlanesRank => PlayerData.PlanesRank;

        public OfflineHomeExperienceServices(OfflineHomeExperiencePlayer player, IHomeExperienceRewardStore store,
            IHomeExperienceCombatRatingBlockers combatRatingBlockers, Action<int> notification,
            Func<long, object> compact, Func<int, string> seasonName, Action<int> resolveSubRank)
        {
            PlayerData = player ?? throw new ArgumentNullException(nameof(player));
            Store = store ?? throw new ArgumentNullException(nameof(store));
            CombatRatingBlockers = combatRatingBlockers;
            this.notification = notification ?? throw new ArgumentNullException(nameof(notification));
            this.compact = compact ?? throw new ArgumentNullException(nameof(compact));
            this.seasonName = seasonName ?? throw new ArgumentNullException(nameof(seasonName));
            this.resolveSubRank = resolveSubRank ?? throw new ArgumentNullException(nameof(resolveSubRank));
            LevelExpParser = new ExpParser(player.Tables.LevelExpParser);
            LevelBlessInfoParser = new BlessParser(player);
            Handler = new HomeExperienceRewardHandler(this);
            Handler.Activate();
        }

        public void ResolveSubRank(int subRank) => resolveSubRank(subRank);
        public object Compact(long value) => compact(value);
        public string GetSeasonNameByRank(int rank) => seasonName(rank);
        public void ShowLevelUpNotification(int previousLevel) => notification(previousLevel);
        public void Dispose()
        {
            if (disposed) return;
            Handler.Dispose(); OnPlayerUpdate.ClearListener(); disposed = true;
        }

        private sealed class ExpParser : IHomeLevelExpParser
        {
            private readonly Rules.LevelExpParser parser;
            public ExpParser(Rules.LevelExpParser parser) { this.parser = parser; }
            public long GetExp(int id, int level) => parser.GetExp(id, level);
            public int CalcLevelByExp(int id, long exp) => parser.CalcLevelByExp(id, exp);
            public long GetLevelUpExp(int id, int level) => parser.GetLevelUpExp(id, level);
            public long GetLevelUpExp(int id, int fromLevel, int toLevel) => parser.GetLevelUpExp(id, fromLevel, toLevel);
        }
        private sealed class BlessParser : IHomeLevelBlessParser
        {
            private readonly OfflineHomeExperiencePlayer player;
            public BlessParser(OfflineHomeExperiencePlayer player) { this.player = player; }
            public int GetBlessLevel(int rank, long exp) => player.Tables.LevelBlessInfoParser.GetBlessLevel((Rules.Rank)rank, exp);
            public HomeLevelBlessInfo Get(int rank, int level)
            {
                var row = player.Tables.LevelBlessInfoParser.Get((Rules.Rank)rank, level);
                return row == null ? null : new HomeLevelBlessInfo { Exp = row.Exp };
            }
            public long GetExpRequestByBlessLevel(int level) => player.GetExpRequestByBlessLevel(level);
        }
    }
}
