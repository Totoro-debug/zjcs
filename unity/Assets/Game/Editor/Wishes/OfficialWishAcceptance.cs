using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Wishes
{
    public static class OfficialWishAcceptance
    {
        public static void Run()
        {
            string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "zjcs-wish-acceptance-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(tempRoot);
            try
            {
                TextAsset source = Resources.Load<TextAsset>("Wishes/wishes-runtime");
                if (source == null) throw new FileNotFoundException("The official wish runtime data is missing.");
                WishRuntimeCatalog catalog = WishRuntimeCatalog.Parse(source.text);
                ProfileSlotCatalog slots = new ProfileSlotCatalog(tempRoot);
                WishProfession profession = catalog.professions.FirstOrDefault(value => value.Rank == 1);
                if (profession == null) throw new InvalidOperationException("The official wish catalog has no rank-one profession.");

                VerifyGuideDrawFriendshipAndShop(catalog, slots, profession.Profession);
                VerifyGuaranteeCollision(catalog, slots, profession.Profession);
                VerifyDuplicateAndPieceAwards(catalog, slots);
                Debug.Log("[WishAcceptance] guide, draw, pity, friendship, shop, conversion and persistence passed");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
            finally
            {
                string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!tempRoot.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to remove a wish acceptance path outside the temp directory.");
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        private static void VerifyGuideDrawFriendshipAndShop(WishRuntimeCatalog catalog,
            ProfileSlotCatalog slots, int profession)
        {
            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession profile = CreateProfile(slots, slotId, catalog, profession);
            var service = new OfflineWishService(profile, catalog);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            long ticketsBefore = profile.InventoryCount(110);
            WishFixedReward guide = catalog.FixedReward(catalog.settings.SkillGachaGuideFixedRewardId);
            WishResult guideResult = service.ClaimGuideReward(now, "acceptance:wish:guide:" + slotId);
            if (!guideResult.IsApplied || guide.Reward == null || guide.Reward.Length != 1
                || guide.Reward[0].ItemId != 110 || guide.Reward[0].Count != 5
                || profile.InventoryCount(110) - ticketsBefore != 5)
                throw new InvalidOperationException("The configured first-entry reward did not grant five wish tickets.");
            if (service.ClaimGuideReward(now, "acceptance:wish:guide-repeat:" + slotId).Code != WishResultCode.Unchanged)
                throw new InvalidOperationException("The first-entry reward was claimable more than once.");

            WishGacha regular = catalog.Gacha(0);
            WishChest chest = catalog.Chest(regular.ChestId);
            WishRewardRule rule = DrawRule(catalog, 0);
            int ruleId = SharedRuleId(rule);
            int[] qualities = rule.IdList.Select(catalog.RandomReward).Where(value => value != null && value.Quality > 1)
                .Select(value => value.Quality).Distinct().ToArray();
            var previousMisses = new Dictionary<int, int>();
            foreach (OfflineWishPityCounter counter in profile.Save.Player.Wishes.PityCounters.Where(value => value.RuleId == ruleId))
                previousMisses[counter.Quality] = counter.MissCount;

            var drawCosts = chest.CostItems.ToDictionary(value => value.ItemId, value => profile.InventoryCount(value.ItemId));
            string receipt = "acceptance:wish:first-draw:" + slotId;
            WishResult draw = service.Draw(0, 1, profession, receipt, now, true);
            if (!draw.IsApplied || draw.Awards.Count != 1)
                throw new InvalidOperationException("The normal first draw failed: " + draw.Code + " " + draw.Message);
            WishResult duplicateReceipt = service.Draw(0, 1, profession, receipt, now, true);
            if (duplicateReceipt.Code != WishResultCode.Unchanged || duplicateReceipt.Awards.Count != draw.Awards.Count)
                throw new InvalidOperationException("A repeated draw receipt was not idempotent.");
            foreach (WishAmount cost in chest.CostItems)
                if (drawCosts[cost.ItemId] - profile.InventoryCount(cost.ItemId) != cost.Count)
                    throw new InvalidOperationException("A repeated draw consumed the cost more than once.");
            foreach (int quality in qualities)
            {
                OfflineWishPityCounter counter = service.State.PityCounters.FirstOrDefault(value =>
                    value.RuleId == ruleId && value.Quality == quality);
                if (counter == null) throw new InvalidOperationException("A configured quality pity counter was not recorded.");
                previousMisses.TryGetValue(quality, out int previousMiss);
                int expected = draw.Awards[0].Quality == quality ? 0
                    : previousMiss + 1;
                if (counter.MissCount != expected)
                    throw new InvalidOperationException("A draw did not reset only its hit quality counter.");
            }

            WishResult friendship = service.Draw(1, 1, 0, "acceptance:wish:friend-empty:" + slotId, now, true);
            if (friendship.Code != WishResultCode.InsufficientItems || profile.InventoryCount(3) != 0
                || service.PoolCount(1) != 0)
                throw new InvalidOperationException("Friendship wishing granted currency or drew without existing coins.");

            DateTime localNow = DateTime.Now;
            service.EnsureShopDay(5, profession, localNow);
            if (service.ShopGoods(5, profession, localNow).Count != 9)
                throw new InvalidOperationException("The daily skill-piece shop did not create its nine configured slots.");
            WishResult refreshed = service.RefreshShop(5, profession, localNow, "acceptance:wish:refresh:" + slotId);
            if (!refreshed.IsApplied || service.State.Shops.First(value => value.ShopId == 5).ManualRefreshCount != 1)
                throw new InvalidOperationException("The daily manual refresh did not consume one configured refresh.");
            service.EnsureShopDay(5, profession, localNow.Date.AddDays(1));
            if (service.State.Shops.First(value => value.ShopId == 5).ManualRefreshCount != 0)
                throw new InvalidOperationException("Manual refresh count did not reset at the local day boundary.");

            OfflineMainlineProgressSession restored = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            var restoredService = new OfflineWishService(restored, catalog);
            if (!restoredService.GuideRewardClaimed || restoredService.PoolCount(0) != 1
                || restoredService.History(0, now, 0, 10).Count != 1
                || restoredService.State.Shops.First(value => value.ShopId == 5).ManualRefreshCount != 0)
                throw new InvalidOperationException("Wish progress did not survive reopening the profile save.");
        }

        private static void VerifyGuaranteeCollision(WishRuntimeCatalog source, ProfileSlotCatalog slots, int professionId)
        {
            WishRuntimeCatalog catalog = WishRuntimeCatalog.Parse(JsonUtility.ToJson(source));
            WishProfession profession = catalog.Profession(professionId);
            WishGacha gacha = catalog.Gacha(0);
            WishRewardRule rule = DrawRule(catalog, 0);
            var qualityIndices = rule.IdList.Select((id, index) => new { Reward = catalog.RandomReward(id), Index = index })
                .Where(value => value.Reward != null && value.Reward.Quality > 1)
                .GroupBy(value => value.Reward.Quality).Select(group => group.First()).Take(2).ToArray();
            if (qualityIndices.Length != 2)
                throw new InvalidOperationException("The regular pool does not expose two distinct high quality pity groups.");
            rule.IndexMininum = new int[rule.IdList.Length];
            foreach (var quality in qualityIndices) rule.IndexMininum[quality.Index] = 10;
            int professionQuality = qualityIndices[0].Reward.Quality;
            SetEnsureHighRank(catalog, profession,
                new[] { new WishQualityMiss { Quality = professionQuality, MissCount = 10 } });

            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession profile = CreateProfile(slots, slotId, catalog, professionId);
            ProgressionResult seed = profile.ApplyInventoryTransaction("acceptance:wish:pity-seed:" + slotId, save =>
            {
                OfflineWishProgress wish = save.Player.Wishes;
                wish.PoolCounts.Add(new OfflineWishPoolCounter { GachaType = 0, Count = 7 });
                foreach (var quality in qualityIndices)
                    wish.PityCounters.Add(new OfflineWishPityCounter
                    { RuleId = SharedRuleId(rule), Quality = quality.Reward.Quality, MissCount = 7 });
                var professionCounter = new OfflineWishProfessionPityCounter
                { Profession = professionId, GachaType = 0, QualityMissCounts = new int[5] };
                professionCounter.QualityMissCounts[professionQuality] = 8;
                wish.ProfessionPityCounters.Add(professionCounter);
            });
            if (seed.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not seed simultaneous wish guarantees: " + seed.Status);

            var service = new OfflineWishService(profile, catalog);
            MethodInfo ensure = typeof(OfflineWishService).GetMethod("EnsureGuarantees",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (ensure == null) throw new MissingMethodException("OfflineWishService.EnsureGuarantees");
            ensure.Invoke(service, new object[] { service.State, 0, professionId, rule });
            OfflineWishScheduledGuarantee[] scheduled = service.State.ScheduledGuarantees
                .Where(value => value.GachaType == 0 && value.DeadlineSequence == 10).OrderBy(value => value.TargetSequence).ToArray();
            if (scheduled.Length != 3 || !scheduled.Select(value => value.TargetSequence).SequenceEqual(new long[] { 8, 9, 10 }))
                throw new InvalidOperationException("Three colliding guarantees were not spread over pulls 8, 9 and 10.");

            SetEnsureHighRank(catalog, profession, Array.Empty<WishQualityMiss>());
            string twoSlotId = slots.CreateSlot();
            OfflineMainlineProgressSession twoProfile = CreateProfile(slots, twoSlotId, catalog, professionId);
            ProgressionResult twoSeed = twoProfile.ApplyInventoryTransaction("acceptance:wish:two-pity-seed:" + twoSlotId, save =>
            {
                OfflineWishProgress wish = save.Player.Wishes;
                wish.PoolCounts.Add(new OfflineWishPoolCounter { GachaType = 0, Count = 8 });
                foreach (var quality in qualityIndices)
                    wish.PityCounters.Add(new OfflineWishPityCounter
                    { RuleId = SharedRuleId(rule), Quality = quality.Reward.Quality, MissCount = 8 });
            });
            if (twoSeed.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not seed two simultaneous wish guarantees: " + twoSeed.Status);
            var twoService = new OfflineWishService(twoProfile, catalog);
            MethodInfo twoEnsure = typeof(OfflineWishService).GetMethod("EnsureGuarantees",
                BindingFlags.Instance | BindingFlags.NonPublic);
            twoEnsure.Invoke(twoService, new object[] { twoService.State, 0, professionId, rule });
            long[] twoTargets = twoService.State.ScheduledGuarantees
                .Where(value => value.GachaType == 0 && value.DeadlineSequence == 10)
                .OrderBy(value => value.TargetSequence).Select(value => value.TargetSequence).ToArray();
            if (!twoTargets.SequenceEqual(new long[] { 9, 10 }))
                throw new InvalidOperationException("Two colliding guarantees were not spread over pulls 9 and 10.");
        }

        private static void VerifyDuplicateAndPieceAwards(WishRuntimeCatalog source, ProfileSlotCatalog slots)
        {
            WishProfession profession = source.professions.FirstOrDefault(value => value.Rank == 1
                && source.SettingsForProfession(value.Profession).EnsureHighRankPieceCount == 7
                && source.items.Any(item => (item.ItemType == 111 || item.ItemType == 112)
                    && source.SkillAvailable(item.ClassId - 2000, value.Profession)));
            if (profession == null) throw new InvalidOperationException("No rank-one profession has the official seven-piece override.");
            WishItem piece = source.items.First(value => (value.ItemType == 111 || value.ItemType == 112)
                && source.SkillAvailable(value.ClassId - 2000, profession.Profession));
            WishSkill pieceSkill = source.Skill(piece.ClassId - 2000);
            WishItem repeatedSkill = source.items.FirstOrDefault(value => (value.ItemType == 100 || value.ItemType == 101)
                && source.Skill(value.ClassId)?.maxRank > 1 && source.SkillAvailable(value.ClassId, profession.Profession)
                && value.AgioItemId > 0 && value.AgioAmount > 0
                && source.Item(value.AgioItemId)?.RecycleGroupCount <= value.AgioAmount
                && source.Item(value.AgioItemId)?.RecycleAwards?.Length > 0);
            if (repeatedSkill == null || pieceSkill == null)
                throw new InvalidOperationException("The official catalog has no suitable skill conversion test case.");

            WishRuntimeCatalog catalog = WishRuntimeCatalog.Parse(JsonUtility.ToJson(source));
            ForceReward(catalog, piece.ClassId, 9, 9);
            ClearRegularGuarantees(catalog, profession.Profession);
            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession profile = CreateProfile(slots, slotId, catalog, profession.Profession);
            var service = new OfflineWishService(profile, catalog);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            WishResult pieceDraw = service.Draw(0, 1, profession.Profession,
                "acceptance:wish:three-piece:" + slotId, now, true);
            if (!pieceDraw.IsApplied || pieceDraw.Awards[0].ItemId != piece.ClassId
                || pieceDraw.Awards[0].Count != 3 || profile.InventoryCount(piece.ClassId) != 3)
                throw new InvalidOperationException("A skill-piece result did not grant the confirmed fixed amount of three.");

            WishItem duplicate = catalog.Item(repeatedSkill.ClassId);
            WishSkill definition = catalog.Skill(duplicate.ClassId);
            ForceReward(catalog, duplicate.ClassId, 1, 1);
            ProgressionResult owned = profile.ApplyCharacterMutation(player =>
                player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = duplicate.ClassId, Rank = 1 }),
                "acceptance:wish:owned-skill:" + slotId);
            if (owned.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not prepare duplicate skill conversion: " + owned.Status);
            WishResult repeated = service.Draw(0, 1, profession.Profession,
                "acceptance:wish:duplicate:" + slotId, now + 1, true);
            if (!repeated.IsApplied || repeated.Awards[0].ConvertedItemId != duplicate.AgioItemId
                || repeated.Awards[0].ConvertedCount != duplicate.AgioAmount
                || profile.InventoryCount(duplicate.AgioItemId) < duplicate.AgioAmount)
                throw new InvalidOperationException("A duplicate skill did not convert by its configured agio rule.");

            ProgressionResult maxRank = profile.ApplyCharacterMutation(player =>
                player.Skills.Skills.First(value => value.ClassId == duplicate.ClassId).Rank = definition.maxRank,
                "acceptance:wish:max-rank:" + slotId);
            if (maxRank.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not prepare max-rank recycle: " + maxRank.Status);
            WishResult recycled = service.Draw(0, 1, profession.Profession,
                "acceptance:wish:recycled-skill:" + slotId, now + 2, true);
            if (!recycled.IsApplied || recycled.Awards[0].RecycledItemId <= 0
                || recycled.Awards[0].RecycledCount <= 0)
                throw new InvalidOperationException("A max-rank duplicate did not recycle its configured overflow pieces.");
        }

        private static OfflineMainlineProgressSession CreateProfile(ProfileSlotCatalog slots, string slotId,
            WishRuntimeCatalog catalog, int profession)
        {
            OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            int[] currencies = catalog.chests.SelectMany(value => value.CostItems ?? Array.Empty<WishAmount>())
                .Select(value => value.ItemId)
                .Concat(catalog.shopGoods.Select(value => value.Currency))
                .Concat(catalog.shops.SelectMany(value => value.RefreshCurrency ?? Array.Empty<int>()))
                .Where(value => value > 0 && value != 3).Distinct().ToArray();
            ProgressionResult setup = profile.ApplyInventoryTransaction("acceptance:wish:setup:" + slotId, save =>
            {
                save.Player.Wardrobe.Profession = profession;
                foreach (int itemId in currencies)
                    save.Player.Inventory.Add(new OfflineItemStack { ItemId = itemId, Count = 1000000 });
            });
            if (setup.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not prepare a local wish profile: " + setup.Status);
            return profile;
        }

        private static void ForceReward(WishRuntimeCatalog catalog, int itemId, int minCount, int maxCount)
        {
            WishRewardRule shared = DrawRule(catalog, 0);
            WishRandomReward forced = catalog.randomRewards.First();
            forced.Quality = catalog.Item(itemId).Quality;
            forced.MinCount = minCount;
            forced.MaxCount = maxCount;
            forced.Rank = -1;
            forced.ItemTypes = Array.Empty<int>();
            forced.ElementTypes = Array.Empty<int>();
            forced.BlackList = Array.Empty<int>();
            forced.IdList = new[] { itemId };
            forced.WeightOfIdList = new[] { 1 };
            shared.IdList = new[] { forced.Id };
            shared.WeightList = new[] { 1 };
        }

        private static void ClearRegularGuarantees(WishRuntimeCatalog catalog, int professionId)
        {
            WishRewardRule rule = DrawRule(catalog, 0);
            rule.IndexMininum = new int[rule.IdList.Length];
            SetEnsureHighRank(catalog, catalog.Profession(professionId), Array.Empty<WishQualityMiss>());
        }

        private static WishRewardRule DrawRule(WishRuntimeCatalog catalog, int gachaType)
        {
            WishChest chest = catalog.Chest(catalog.Gacha(gachaType).ChestId);
            WishRewardRule selected = chest.RewardRules.Select(catalog.RewardRule).First(value => value != null);
            return catalog.RewardRule(SharedRuleId(selected)) ?? selected;
        }

        private static WishSkillGachaSettings MutableSettings(WishRuntimeCatalog catalog, WishProfession profession)
        {
            WishRankSkillGachaSettings rank = (catalog.settings.RankSkillGachaSettings
                ?? Array.Empty<WishRankSkillGachaSettings>()).FirstOrDefault(value => value.Rank == profession.Rank);
            if (rank == null) return catalog.settings.DefaultSkillGachaSettings;
            return new WishSkillGachaSettings
            {
                EnsureHighRank = rank.EnsureHighRank,
                EnsureHighRankPieceCount = rank.EnsureHighRankPieceCount
            };
        }

        private static void SetEnsureHighRank(WishRuntimeCatalog catalog, WishProfession profession,
            WishQualityMiss[] guarantees)
        {
            WishRankSkillGachaSettings rank = (catalog.settings.RankSkillGachaSettings
                ?? Array.Empty<WishRankSkillGachaSettings>()).FirstOrDefault(value => value.Rank == profession.Rank);
            if (rank == null) catalog.settings.DefaultSkillGachaSettings.EnsureHighRank = guarantees;
            else rank.EnsureHighRank = guarantees;
        }

        private static int SharedRuleId(WishRewardRule rule) =>
            int.TryParse(rule.RecordSharedId, out int id) && id > 0 ? id : rule.Id;
    }
}
