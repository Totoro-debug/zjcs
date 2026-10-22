using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineCharacterProgressionService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog catalog;
        private readonly CharacterPropCalculator propertyCalculator;

        public OfflineCharacterProgressionService(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            propertyCalculator = new CharacterPropCalculator(catalog);
            RefreshDerivedProgress(session.Save.Player);
        }

        public OfflinePlayerProgress Player => session.Save.Player;
        public OfficialCharacterCatalog Catalog => catalog;
        public CharacterPropCalculator PropertyCalculator => propertyCalculator;

        public CharacterPropSnapshot EvaluateProps(long nowUnixSeconds)
        {
            return propertyCalculator.Evaluate(Player, nowUnixSeconds);
        }

        public void AddPropertySource(IOfflineCharacterPropSource source)
        {
            propertyCalculator.AddSource(source);
        }
        public IReadOnlyDictionary<int, long> BaseBattleProperties()
        {
            return catalog.CalcCharacterBaseBattleProperties(Player.Level, Player.SubRank);
        }

        public IReadOnlyDictionary<int, long> AppearanceProperties(long nowUnixSeconds)
        {
            Dictionary<int, long> expiryTimes = Player.Wardrobe.Expiries
                .ToDictionary(value => value.ItemId, value => value.ExpiresAtUnixSeconds);
            return catalog.AppearanceProperties(Player.Wardrobe.AppearanceLevel, Player.Inventory,
                expiryTimes, nowUnixSeconds);
        }

        public ProgressionResult InitializeWardrobe(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A wardrobe initialization event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineWardrobeProgress wardrobe = player.Wardrobe;
                if (wardrobe.BaseSkins.Count == 0)
                    wardrobe.BaseSkins = catalog.DefaultSkins(wardrobe.Sex, wardrobe.Profession)
                        .OrderBy(pair => pair.Key).Select(pair => new OfflineAppearanceSkin
                        { Type = pair.Key, Id = pair.Value }).ToList();
                var dyeByType = wardrobe.Dyes.ToDictionary(value => value.Type);
                foreach (KeyValuePair<int, OfflineAppearanceDye> dye in catalog.DefaultDyes())
                    if (!dyeByType.ContainsKey(dye.Key)) wardrobe.Dyes.Add(dye.Value);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public CharacterExperienceSnapshot Experience()
        {
            return Experience(Player.Experience, Player.BlessingExperience);
        }

        public CharacterExperienceSnapshot Experience(long experience, long blessingExperience)
        {
            OfflinePlayerProgress player = Player;
            int rank = RankOf(player.SubRank);
            int highestSubRank = HighestSubRank(rank);
            int blessLimit = catalog.LevelLimits.Where(row => RankOf(row.SubRank) == rank)
                .Select(row => row.PlayerBlessLevel).DefaultIfEmpty(0).Max();
            bool blessingOpen = player.HasActiveSeasonRecord && player.SubRank == highestSubRank
                && blessLimit > 0 && player.Level >= OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
            int seasonBase = Math.Min(player.Level, OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits));
            bool atSeasonRankLimit = player.HasActiveSeasonRecord && player.SubRank >= highestSubRank;
            return OfflineCharacterExperience.Calculate(experience, blessingExperience,
                player.SubRank, player.SeasonRank, seasonBase, blessingOpen,
                player.HasActiveSeasonRecord && !atSeasonRankLimit,
                atSeasonRankLimit, blessLimit, catalog.CumulativeLevelExperience,
                catalog.LevelLimits, catalog.BlessLevels);
        }

        public ProgressionResult AwardExperience(long amount, string eventId)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A positive official experience award and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                RefreshDerivedProgress(player);
                CharacterExperienceSnapshot current = Snapshot(player);
                long remainder = amount;
                if (current.IsBlessing)
                {
                    player.BlessingExperience = checked(player.BlessingExperience + remainder);
                    remainder = 0;
                }
                if (remainder > 0)
                {
                    long nextLimit = CumulativeAt(OfflineCharacterExperience.BaseLevelLimit(
                        player.SubRank + (current.IsStoredExperience ? 1 : 0), catalog.LevelLimits));
                    long accepted = current.IsStoredExperience
                        ? Math.Min(remainder, Math.Max(0, nextLimit - player.Experience)) : remainder;
                    player.Experience = checked(player.Experience + accepted);
                }
                RefreshDerivedProgress(player);
            }, eventId);
        }

        public ProgressionResult UpgradeSubRank(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A rank-up event ID is required.");
            Dictionary<string, object> nextRank = catalog.Find("player_subrank", Player.SubRank + 1, "SubRank");
            if (nextRank == null) return Invalid("The next official subrank is not configured.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.SubRank + 1 != Integer(nextRank, "SubRank"))
                    throw new InvalidOperationException("The character rank changed; reopen the rank panel.");
                int requiredLevel = OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
                if (player.Level < requiredLevel) throw new InvalidOperationException("The official level requirement is not met.");
                Dictionary<string, object> costs = nextRank.TryGetValue("Items", out object value)
                    && value is Dictionary<string, object> map ? map : new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> cost in costs)
                {
                    int itemId = int.Parse(cost.Key, CultureInfo.InvariantCulture);
                    long count = Convert.ToInt64(cost.Value, CultureInfo.InvariantCulture);
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == itemId);
                    if (count < 0 || stack == null || stack.Count < count)
                        throw new InvalidOperationException("Required rank-up materials are missing.");
                }
                foreach (KeyValuePair<string, object> cost in costs)
                {
                    int itemId = int.Parse(cost.Key, CultureInfo.InvariantCulture);
                    long count = Convert.ToInt64(cost.Value, CultureInfo.InvariantCulture);
                    player.Inventory.First(item => item.ItemId == itemId).Count -= count;
                }
                player.SubRank++;
                player.Rank = RankOf(player.SubRank);
                RefreshDerivedProgress(player);
            }, eventId);
        }

        public ProgressionResult ClaimRankTutorial(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A tutorial claim event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.RankTutorialRewardClaimed) throw new InvalidOperationException("The rank tutorial reward was already claimed.");
                if (player.Level < 10) throw new InvalidOperationException("The rank tutorial is not unlocked.");
                if (!player.RankTutorialSeen) throw new InvalidOperationException("Read the rank tutorial before claiming its reward.");
                AddItem(player, 43003, 2);
                AddItem(player, 1, 2000);
                player.RankTutorialRewardClaimed = true;
            }, eventId);
        }

        public bool HasPendingRankTutorial => Player.Level >= 10 && !Player.RankTutorialRewardClaimed;

        public ProgressionResult MarkRankTutorialSeen(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A tutorial view event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.Level < 10) throw new InvalidOperationException("The rank tutorial is not unlocked.");
                player.RankTutorialSeen = true;
            }, eventId);
        }

        public ProgressionResult WearAppearance(int itemId, long nowUnixSeconds, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                WardrobeAppearanceState state = ReadAppearance(player.Wardrobe);
                var owned = player.Inventory.ToDictionary(item => item.ItemId, item => item.Count);
                if (!OfflineWardrobeRules.TryWear(state, itemId, owned, catalog.Appearances,
                    catalog.Skins, catalog.AllowedSkins, nowUnixSeconds))
                    throw new InvalidOperationException("This appearance cannot be worn by the current character.");
                WriteAppearance(player.Wardrobe, state);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult TakeOffAppearance(int type, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                WardrobeAppearanceState state = ReadAppearance(player.Wardrobe);
                if (!OfflineWardrobeRules.TakeOff(state, type))
                    throw new InvalidOperationException("There is no wardrobe item in this slot.");
                WriteAppearance(player.Wardrobe, state);
            }, eventId);
        }

        public ProgressionResult WearTitle(int itemId, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                if (!IsOwned(player, itemId) || ItemType(itemId) != 339)
                    throw new InvalidOperationException("The selected title is not owned.");
                player.Wardrobe.WornTitleId = itemId;
                if (Expiry(player.Wardrobe, itemId) > 0 && !player.Wardrobe.LimitedTitleCollection.Contains(itemId))
                    player.Wardrobe.LimitedTitleCollection.Add(itemId);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult TakeOffTitle(string eventId)
        {
            return session.ApplyCharacterMutation(player => player.Wardrobe.WornTitleId = 0, eventId);
        }

        public ProgressionResult SetDye(int type, int colorId, int hue, int saturation, int value,
            string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || !catalog.IsValidDye(type, colorId, hue, saturation, value))
                return Invalid("The selected color is outside the official dye configuration.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineAppearanceDye dye = player.Wardrobe.Dyes.FirstOrDefault(entry => entry.Type == type);
                if (dye == null)
                {
                    dye = new OfflineAppearanceDye { Type = type };
                    player.Wardrobe.Dyes.Add(dye);
                }
                dye.ColorId = colorId;
                dye.Hue = hue;
                dye.Saturation = saturation;
                dye.Value = value;
            }, eventId);
        }

        public ProgressionResult RecordTimedAppearanceAward(int itemId, int count, long nowUnixSeconds,
            string eventId)
        {
            if (itemId <= 0 || count <= 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(eventId)
                || !catalog.Appearances.ContainsKey(itemId) && catalog.ItemType(itemId) != 339)
                return Invalid("A valid timed appearance award is required.");
            Dictionary<string, object> config = catalog.FindTimedAppearance(itemId);
            if (config == null || Integer(config, "Duration") <= 0)
                return Invalid("The appearance has no supported official time limit configuration.");
            return session.ApplyCharacterMutation(player =>
            {
                AddItem(player, itemId, count);
                OfflineTimedAppearance expiry = player.Wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
                long previous = expiry == null ? nowUnixSeconds : expiry.ExpiresAtUnixSeconds;
                long next = OfflineCharacterExperience.ExtendTimedAppearance(nowUnixSeconds, previous,
                    Integer(config, "Duration"), Integer(config, "MaxDuration"), count,
                    Boolean(config, "CanOverlapTime"));
                if (expiry == null)
                {
                    expiry = new OfflineTimedAppearance { ItemId = itemId };
                    player.Wardrobe.Expiries.Add(expiry);
                }
                expiry.ExpiresAtUnixSeconds = next;
                if (catalog.ItemType(itemId) == 339 && !player.Wardrobe.LimitedTitleCollection.Contains(itemId))
                    player.Wardrobe.LimitedTitleCollection.Add(itemId);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult SynchronizeExpiredItems(long nowUnixSeconds, string eventId)
        {
            if (Player.Wardrobe.Expiries.All(expiry => nowUnixSeconds <= expiry.ExpiresAtUnixSeconds))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            return session.ApplyCharacterMutation(player =>
            {
                foreach (OfflineTimedAppearance expiry in player.Wardrobe.Expiries.ToArray())
                {
                    if (nowUnixSeconds <= expiry.ExpiresAtUnixSeconds) continue;
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == expiry.ItemId);
                    if (stack != null) player.Inventory.Remove(stack);
                    player.Wardrobe.WornItems.RemoveAll(item => ItemIdForType(player.Wardrobe, item.Type) == expiry.ItemId);
                    player.Wardrobe.WearTimes.RemoveAll(item => !player.Wardrobe.WornItems.Any(worn => worn.Type == item.Type));
                    if (player.Wardrobe.WornTitleId == expiry.ItemId) player.Wardrobe.WornTitleId = 0;
                    player.Wardrobe.Expiries.Remove(expiry);
                }
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public WardrobeResolution ResolveAppearance()
        {
            return OfflineWardrobeRules.Resolve(ReadAppearance(Player.Wardrobe),
                catalog.Appearances, catalog.Skins, catalog.AllowedSkins);
        }

        public bool CanWearAppearance(int itemId, long nowUnixSeconds)
        {
            if (Player.Wardrobe.WornItems.Any(item => item.Id == itemId)) return true;
            var owned = Player.Inventory.ToDictionary(item => item.ItemId, item => item.Count);
            if (!owned.ContainsKey(itemId)) owned[itemId] = 1;
            WardrobeAppearanceState state = ReadAppearance(Player.Wardrobe);
            return OfflineWardrobeRules.TryWear(state, itemId, owned, catalog.Appearances,
                catalog.Skins, catalog.AllowedSkins, nowUnixSeconds);
        }

        private CharacterExperienceSnapshot Snapshot(OfflinePlayerProgress player)
        {
            return OfflineCharacterExperience.Calculate(player.Experience, player.BlessingExperience,
                player.SubRank, player.SeasonRank, Math.Min(player.Level,
                    OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits)),
                player.HasActiveSeasonRecord && player.SubRank == HighestSubRank(RankOf(player.SubRank)),
                player.HasActiveSeasonRecord, player.HasActiveSeasonRecord
                    && player.SubRank >= HighestSubRank(player.SeasonRank), BlessLimit(player.SeasonRank),
                catalog.CumulativeLevelExperience, catalog.LevelLimits, catalog.BlessLevels);
        }

        private int BlessLimit(int rank)
        {
            return catalog.LevelLimits.Where(row => RankOf(row.SubRank) == rank)
                .Select(row => row.PlayerBlessLevel).DefaultIfEmpty(0).Max();
        }

        private int HighestSubRank(int rank)
        {
            return catalog.Rows("player_subrank").Select(row => Integer(row, "SubRank"))
                .Where(value => RankOf(value) == rank).DefaultIfEmpty(0).Max();
        }

        private long CumulativeAt(int level)
        {
            IReadOnlyList<long> values = catalog.CumulativeLevelExperience;
            return values[Math.Max(0, Math.Min(level, values.Count - 1))];
        }

        private void RefreshDerivedProgress(OfflinePlayerProgress player)
        {
            player.Rank = RankOf(player.SubRank);
            CharacterExperienceSnapshot snapshot = Snapshot(player);
            player.Level = snapshot.Level;
            player.SeasonBaseLevel = OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
            RefreshWardrobeProgress(player);
        }

        private void RefreshWardrobeProgress(OfflinePlayerProgress player)
        {
            player.Wardrobe.FashionExperience = catalog.FashionExperience(player.Inventory,
                player.Wardrobe.LimitedTitleCollection);
            player.Wardrobe.AppearanceLevel = catalog.AppearanceLevel(player.Wardrobe.FashionExperience);
        }

        private WardrobeAppearanceState ReadAppearance(OfflineWardrobeProgress saved)
        {
            var state = new WardrobeAppearanceState { Sex = saved.Sex, Profession = saved.Profession };
            foreach (OfflineAppearanceSkin value in saved.BaseSkins) state.BaseSkinByType[value.Type] = value.Id;
            foreach (OfflineAppearanceSkin value in saved.WornSkins) state.EquippedSkinBySlot[value.Type] = value.Id;
            foreach (OfflineAppearanceSkin value in saved.WornItems) state.AppendedItemByType[value.Type] = value.Id;
            foreach (OfflineAppearanceWearTime value in saved.WearTimes) state.WearTimeByType[value.Type] = value.UnixSeconds;
            foreach (OfflineAppearanceDye value in saved.Dyes) state.DyeByType[value.Type] = value;
            return state;
        }

        private static void WriteAppearance(OfflineWardrobeProgress saved, WardrobeAppearanceState state)
        {
            saved.WornSkins = state.EquippedSkinBySlot.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceSkin { Type = pair.Key, Id = pair.Value }).ToList();
            saved.WornItems = state.AppendedItemByType.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceSkin { Type = pair.Key, Id = pair.Value }).ToList();
            saved.WearTimes = state.WearTimeByType.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceWearTime { Type = pair.Key, UnixSeconds = pair.Value }).ToList();
            saved.Dyes = state.DyeByType.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        }

        private bool IsOwned(OfflinePlayerProgress player, int itemId)
        {
            return player.Inventory.Any(item => item.ItemId == itemId && item.Count > 0);
        }

        private int ItemType(int itemId)
        {
            return catalog.ItemType(itemId);
        }

        private static long Expiry(OfflineWardrobeProgress wardrobe, int itemId)
        {
            OfflineTimedAppearance expiry = wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
            return expiry == null ? 0 : expiry.ExpiresAtUnixSeconds;
        }

        private static int ItemIdForType(OfflineWardrobeProgress wardrobe, int type)
        {
            OfflineAppearanceSkin entry = wardrobe.WornItems.FirstOrDefault(value => value.Type == type);
            return entry == null ? 0 : entry.Id;
        }

        private static void AddItem(OfflinePlayerProgress player, int itemId, long count)
        {
            OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == itemId);
            if (stack == null)
            {
                stack = new OfflineItemStack { ItemId = itemId };
                player.Inventory.Add(stack);
            }
            stack.Count = checked(stack.Count + count);
        }

        private int RankOf(int subRank)
        {
            return catalog.RankOfSubRank(subRank);
        }

        private static int Integer(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static bool Boolean(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value is bool result && result;
        }

        private static ProgressionResult Invalid(string message)
        {
            return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
        }
    }
}
