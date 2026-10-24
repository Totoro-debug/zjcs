using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor.TreasureGacha
{
    public static class OfficialTreasureGachaCatalogAcceptance
    {
        public static void Run()
        {
            try
            {
                VerifyRankUnlockDayBoundary();
                Debug.Log("[TreasureGachaAcceptance] rank unlock day boundary passed");
                VerifyPityAccounting();
                Debug.Log("[TreasureGachaAcceptance] first draw and multi-pity collision passed");
                VerifyDuplicateRelicConversion();
                Debug.Log("[TreasureGachaAcceptance] four official duplicate relic conversions passed");
                VerifyHistoryRetention();
                Debug.Log("[TreasureGachaAcceptance] 180-day and category history limits passed");
                VerifyItemSourceResolution();
                Debug.Log("[TreasureGachaAcceptance] item-source relic and fragment navigation passed");
                VerifyPerformanceCatalog();
                Debug.Log("[TreasureGachaAcceptance] official capsule and machine animation catalog passed");
                VerifyOfficialResultPanel();
                Debug.Log("[TreasureGachaAcceptance] official result panel resource passed");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void VerifyRankUnlockDayBoundary()
        {
            TextAsset source = Resources.Load<TextAsset>("TreasureGacha/treasure-gacha-runtime");
            if (source == null) throw new InvalidOperationException("The official treasure gacha catalog is missing.");
            TreasureGachaRuntimeCatalog catalog = TreasureGachaRuntimeCatalog.Parse(source.text);
            TreasureGachaSeries series = catalog.SortedSeries().FirstOrDefault(value =>
                value.ConditionType == "CompositeAndCondition" && value.PreviewDuration > 0
                && value.ConditionIds.Select(catalog.Condition).Any(condition =>
                    condition?.Type == "ServerRankUnlockDaysCondition"));
            if (series == null) throw new InvalidOperationException("No rank-gated series with a preview is configured.");

            TreasureCondition rankCondition = series.ConditionIds.Select(catalog.Condition)
                .First(value => value?.Type == "ServerRankUnlockDaysCondition");
            DateTime unlock = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Local);
            DateTime opensAt = unlock.Date.AddDays(rankCondition.Days);
            var player = new OfflinePlayerProgress { Rank = rankCondition.Rank };
            player.RankUnlockTimes.Add(new OfflineRankUnlockTime
            {
                Rank = rankCondition.Rank,
                UnixSeconds = new DateTimeOffset(unlock).ToUnixTimeSeconds()
            });
            AddSeriesMaps(catalog, series, player);

            if (catalog.IsSeriesOpen(series, player, opensAt.AddTicks(-1), 0, out DateTime? nextOpen)
                || nextOpen != opensAt)
                throw new InvalidOperationException("The series opened before its configured local reset day.");
            if (!catalog.IsSeriesOpen(series, player, opensAt, 0, out _))
                throw new InvalidOperationException("The series did not open at the configured local reset boundary.");
            if (!catalog.IsSeriesPreview(series, player,
                    opensAt.AddHours(-series.PreviewDuration), 0, out DateTime previewOpensAt)
                || previewOpensAt != opensAt)
                throw new InvalidOperationException("The series preview does not start at the configured offset.");

            rankCondition.IsOnlyRank = true;
            player.Rank = rankCondition.Rank + 1;
            if (catalog.IsSeriesOpen(series, player, opensAt, 0, out _))
                throw new InvalidOperationException("An IsOnlyRank condition accepted a higher rank.");
        }

        private static void AddSeriesMaps(TreasureGachaRuntimeCatalog catalog,
            TreasureGachaSeries series, OfflinePlayerProgress player)
        {
            player.UnlockedMapClassIds.Add(series.MapClassId);
            foreach (TreasureCondition condition in series.ConditionIds.Select(catalog.Condition))
                if (condition?.Type == "MapUnlockCondition"
                    && !player.UnlockedMapClassIds.Contains(condition.MapClassId))
                    player.UnlockedMapClassIds.Add(condition.MapClassId);
        }

        private static void VerifyPityAccounting()
        {
            string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "zjcs-treasure-gacha-acceptance-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var slots = new ProfileSlotCatalog(tempRoot);
                string slotId = slots.CreateSlot();
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                TreasureGachaRuntimeCatalog catalog = OfficialTreasureGachaAssets.LoadDefault();
                DateTime localNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
                long now = new DateTimeOffset(localNow).ToUnixTimeSeconds();
                ProgressionResult setup = profile.ApplyCharacterMutation(player =>
                {
                    player.CharacterCreated = true;
                    player.CharacterName = "Treasure Gacha Acceptance";
                    player.Level = 99;
                    player.Rank = 99;
                    player.MaxProfessionRank = 99;
                    foreach (TreasureGachaSeries row in catalog.Series)
                        if (!player.UnlockedMapClassIds.Contains(row.MapClassId))
                            player.UnlockedMapClassIds.Add(row.MapClassId);
                    foreach (TreasureCondition condition in catalog.Conditions)
                        if (condition.Type == "MapUnlockCondition" && condition.MapClassId > 0
                            && !player.UnlockedMapClassIds.Contains(condition.MapClassId))
                            player.UnlockedMapClassIds.Add(condition.MapClassId);
                    for (int rank = 1; rank <= player.Rank; rank++)
                        player.RankUnlockTimes.Add(new OfflineRankUnlockTime
                        { Rank = rank, UnixSeconds = now - 400L * 86400 });
                    player.Inventory.Add(new OfflineItemStack { ItemId = 301, Count = 100000 });
                    player.Inventory.Add(new OfflineItemStack { ItemId = 2, Count = 100000 });
                }, "acceptance:treasure-gacha:" + slotId);
                if (setup.Status != ProgressionResultStatus.Applied)
                    throw new InvalidOperationException("Could not prepare a local gacha profile: " + setup.Status);

                TreasureGachaSeries series = catalog.SortedSeries().FirstOrDefault(value =>
                    catalog.IsSeriesOpen(value, profile.Save.Player, localNow,
                        profile.Save.CreatedAtUnixSeconds, out _));
                if (series == null) throw new InvalidOperationException("The acceptance profile has no open series.");
                var service = new OfflineTreasureGachaService(profile, catalog);
                TreasureGachaResult first = service.Draw(4, 1, series.ChestGachaId,
                    "acceptance:first-draw", now, true, true);
                if (!first.IsApplied) throw new InvalidOperationException("First draw failed: " + first.Code + " " + first.Message);

                TreasureChestConfig chest = catalog.Chest(catalog.Gacha(4).ChestId);
                TreasureRewardRule rule = chest.RewardRules.Select(catalog.Rule)
                    .First(value => value.IndexMininum.Any(threshold => threshold > 0));
                int recordId = int.Parse(rule.RecordSharedId, System.Globalization.CultureInfo.InvariantCulture);
                int[] qualities = rule.IdList.Select((id, index) => new
                    { Quality = catalog.RandomReward(id).Quality, Threshold = rule.IndexMininum[index], Index = index })
                    .Where(value => value.Quality > 1 && value.Threshold > 0)
                    .GroupBy(value => value.Quality).Select(group => group.First())
                    .OrderBy(value => value.Index).Select(value => value.Quality).Take(3).ToArray();
                foreach (int quality in qualities)
                {
                    OfflineTreasurePityCounter counter = service.State.PityCounters.FirstOrDefault(value =>
                        value.RecordId == recordId && value.Quality == quality);
                    if (counter == null || counter.DrawCount != 1)
                        throw new InvalidOperationException("The first pull did not increment pity quality " + quality + ".");
                }
                if (qualities.Length != 3) throw new InvalidOperationException("The official chest does not expose three pity qualities.");

                foreach (int index in Enumerable.Range(0, rule.IdList.Length))
                    if (catalog.RandomReward(rule.IdList[index]).Quality > 1)
                        rule.IndexMininum[index] = 10;
                ProgressionResult seed = profile.ApplyInventoryTransaction("acceptance:pity-seed", save =>
                {
                    OfflineTreasureGachaProgress state = save.Player.TreasureGacha;
                    OfflineTreasurePoolCount pool = state.PoolCounts.FirstOrDefault(value => value.GachaType == 4);
                    if (pool == null) state.PoolCounts.Add(pool = new OfflineTreasurePoolCount { GachaType = 4 });
                    pool.Count = 7;
                    foreach (int quality in qualities)
                    {
                        OfflineTreasurePityCounter counter = state.PityCounters.First(value =>
                            value.RecordId == recordId && value.Quality == quality);
                        counter.DrawCount = 7;
                        counter.MissCount = 7;
                    }
                });
                if (seed.Status != ProgressionResultStatus.Applied)
                    throw new InvalidOperationException("Could not seed the simultaneous pity state: " + seed.Status);

                for (int index = 0; index < qualities.Length; index++)
                {
                    TreasureGachaResult draw = service.Draw(4, 1, series.ChestGachaId,
                        "acceptance:pity-collision:" + index, now + index, true, true);
                    if (!draw.IsApplied || draw.Awards.Count != 1 || draw.Awards[0].Quality != qualities[index])
                        throw new InvalidOperationException("Simultaneous pity did not resolve in pull order 8/9/10.");
                    OfflineTreasurePityCounter counter = service.State.PityCounters.First(value =>
                        value.RecordId == recordId && value.Quality == qualities[index]);
                    if (counter.MissCount != 0)
                        throw new InvalidOperationException("Pity hit did not reset the quality miss counter " + qualities[index] + ".");
                }
            }
            finally
            {
                string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!tempRoot.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to remove a gacha acceptance path outside the temp directory.");
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        private static void VerifyOfficialResultPanel()
        {
            HomeAssetCatalog assets = HomeAssetCatalog.LoadDefault();
            assets.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath, "TreasureGacha", "ui.json"));
            if (!assets.HasPanel("treasure_gacha_result"))
                throw new InvalidOperationException("The official treasure gacha result panel is not registered.");
            HomeUiNode root = assets.Panel("treasure_gacha_result").root;
            if (root == null || root.name != "GachaResultPanel"
                || !root.children.SelectMany(value => value.children ?? Array.Empty<HomeUiNode>())
                    .Any(value => value.name == "Content"))
                throw new InvalidOperationException("The official result panel hierarchy is incomplete.");

            HomeUiNode[] mainNodes = EnumerateNodes(assets.Panel("treasure_gacha_main").root).ToArray();
            if (mainNodes.Length < 900)
                throw new InvalidOperationException("The flattened official main panel tree was not fully restored.");
            if (mainNodes.Count(value => value.image != null) < 450)
                throw new InvalidOperationException("Official Image components were not restored to the main panel.");
            string[] cardPaths =
            {
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/BlueCard/Panel (1)/ChestImg",
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/PurpleCard/Panel (2)/ChestImg",
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/OrangeCard/Panel (3)/ChestImg",
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/GoldCard/Panel (4)/ChestImg",
            };
            foreach (string path in cardPaths)
            {
                HomeUiNode card = mainNodes.FirstOrDefault(value => value.path == path);
                if (card == null || card.image?.sprite == null || string.IsNullOrEmpty(card.image.sprite.file))
                    throw new InvalidOperationException("An official treasure gacha card image was not restored: " + path);
            }

            HomeUiNode costBackground = mainNodes.FirstOrDefault(value => value.path ==
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/OpenBtnGo/OpenBtn/CommonBtn_CostItem/Content/ItemContent/BgImg");
            if (costBackground?.image?.sprite == null || costBackground.image.type != 1
                || costBackground.image.sprite.border == null || costBackground.image.sprite.border.Length < 4)
                throw new InvalidOperationException("The official gacha cost background is missing its sliced sprite data.");
            Sprite costSprite = assets.FindSprite(costBackground.image.sprite);
            if (costSprite == null || Mathf.Abs(costSprite.border.x - 20f) > 0.01f
                || Mathf.Abs(costSprite.border.y - 19f) > 0.01f
                || Mathf.Abs(costSprite.border.z - 20f) > 0.01f
                || Mathf.Abs(costSprite.border.w - 20f) > 0.01f)
                throw new InvalidOperationException("The official gacha cost sprite border was not preserved.");
        }

        private static void VerifyDuplicateRelicConversion()
        {
            WithAcceptanceProfile("duplicate", (profile, catalog, now) =>
            {
                var service = new OfflineTreasureGachaService(profile, catalog);
                int[] expectedAmounts = { 20, 30, 40, 60 };
                for (int quality = 2; quality <= 5; quality++)
                {
                    int gachaType = quality + 2;
                    TreasureChestConfig chest = catalog.Chest(catalog.Gacha(gachaType).ChestId);
                    TreasureRewardRule rule = catalog.Rule(chest.RewardRules[0]);
                    TreasureRandomReward reward = rule.IdList.Select(catalog.RandomReward)
                        .FirstOrDefault(value => value.Quality == quality && !value.IsPiece);
                    TreasureGachaSeries series = catalog.SortedSeries().FirstOrDefault(value =>
                        catalog.IsSeriesOpen(value, profile.Save.Player,
                            DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime().DateTime,
                            profile.Save.CreatedAtUnixSeconds, out _)
                        && value.ChestGachaItems.Any(itemId =>
                            catalog.Item(itemId)?.ItemType == 305
                            && catalog.Item(itemId).Quality == quality
                            && !catalog.Item(itemId).CanRepeat));
                    TreasureItemConfig relic = series == null ? null : series.ChestGachaItems
                        .Select(catalog.Item).FirstOrDefault(value => value != null
                            && value.ItemType == 305 && value.Quality == quality && !value.CanRepeat);
                    if (reward == null || relic == null)
                        throw new InvalidOperationException("No configured non-repeatable relic exists for quality " + quality + ".");
                    TreasureInfoConfig info = catalog.Treasure(relic.ClassId);
                    int expectedPieceId = info?.TreasurePieceId ?? 0;
                    if (relic.AgioItemId != expectedPieceId || relic.AgioAmount != expectedAmounts[quality - 2])
                        throw new InvalidOperationException("Official duplicate conversion data changed for quality " + quality + ".");

                    rule.IdList = new[] { reward.Id };
                    rule.WeightList = new[] { 100 };
                    rule.CountList = new[] { 1f };
                    rule.IndexMininum = new[] { 0 };
                    reward.IdList = new[] { relic.ClassId };
                    reward.WeightOfIdList = new[] { 100 };
                    reward.MapClassId = 0;
                    reward.ElementTypes = null;
                    reward.IsPiece = false;
                    reward.MinCount = 1;
                    reward.MaxCount = 1;

                    ProgressionResult seed = profile.ApplyInventoryTransaction(
                        "acceptance:duplicate-seed:" + quality, save =>
                        {
                            OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value =>
                                value.ItemId == relic.ClassId);
                            if (stack == null) save.Player.Inventory.Add(new OfflineItemStack
                                { ItemId = relic.ClassId, Count = 1 });
                            else stack.Count = Math.Max(1, stack.Count);
                            OfflineTreasurePoolCount pool = save.Player.TreasureGacha.PoolCounts
                                .FirstOrDefault(value => value.GachaType == gachaType);
                            if (pool == null) save.Player.TreasureGacha.PoolCounts.Add(pool =
                                new OfflineTreasurePoolCount { GachaType = gachaType });
                            pool.Count = 1;
                        });
                    if (seed.Status != ProgressionResultStatus.Applied)
                        throw new InvalidOperationException("Could not seed the duplicate relic state.");

                    TreasureGachaResult result = service.Draw(gachaType, 1, series.ChestGachaId,
                        "acceptance:duplicate-draw:" + quality, now + quality, true, true);
                    if (!result.IsApplied || result.Awards.Count != 1
                        || result.Awards[0].ItemId != relic.ClassId
                        || result.Awards[0].ConvertedItemId != expectedPieceId
                        || result.Awards[0].ConvertedCount != expectedAmounts[quality - 2]
                        || profile.InventoryCount(expectedPieceId) != expectedAmounts[quality - 2])
                        throw new InvalidOperationException("Duplicate relic quality " + quality + " did not convert into its configured pieces.");
                }
            });
        }

        private static void VerifyHistoryRetention()
        {
            WithAcceptanceProfile("history", (profile, catalog, now) =>
            {
                long expiredAt = now - 181L * 86400L;
                ProgressionResult seed = profile.ApplyInventoryTransaction("acceptance:history-seed", save =>
                {
                    OfflineTreasureGachaProgress state = save.Player.TreasureGacha;
                    for (int index = 0; index < 1001; index++)
                        state.History.Add(CreateHistoryRecord("acceptance:precious:" + index, 4,
                            now - index - 1));
                    state.History.Add(CreateHistoryRecord("acceptance:other-box", 5, now - 20));
                    state.History.Add(CreateHistoryRecord("acceptance:expired", 6, expiredAt));
                    for (int index = 0; index < 1001; index++)
                        state.History.Add(CreateHistoryRecord("acceptance:element:" + index,
                            8 + index % 5, now - 2001 - index));
                });
                if (seed.Status != ProgressionResultStatus.Applied)
                    throw new InvalidOperationException("Could not seed history retention boundaries.");

                var service = new OfflineTreasureGachaService(profile, catalog);
                TreasureGachaSeries series = catalog.SortedSeries().First(value =>
                    catalog.IsSeriesOpen(value, profile.Save.Player,
                        DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime().DateTime,
                        profile.Save.CreatedAtUnixSeconds, out _));
                TreasureGachaResult precious = service.Draw(4, 1, series.ChestGachaId,
                    "acceptance:history-precious-draw", now, true, true);
                TreasureGachaResult element = service.Draw(8, 1, 0,
                    "acceptance:history-element-draw", now + 1, true, true);
                if (!precious.IsApplied || !element.IsApplied)
                    throw new InvalidOperationException("History retention boundary draws failed.");

                OfflineTreasureDrawRecord[] rows = service.State.History.ToArray();
                OfflineTreasureDrawRecord[] preciousRows = rows.Where(value => value.GachaType == 4).ToArray();
                OfflineTreasureDrawRecord[] elementRows = rows.Where(value => value.GachaType >= 8).ToArray();
                if (catalog.Settings.HistoryDays != 180 || catalog.Settings.HistoryLimit != 1000
                    || preciousRows.Length != 1000 || elementRows.Length != 1000
                    || rows.Count(value => value.GachaType == 5) != 1
                    || rows.Any(value => value.ReceiptId == "acceptance:expired")
                    || preciousRows.Min(value => value.UnixSeconds) != now - 999
                    || !preciousRows.Any(value => value.ReceiptId == "acceptance:history-precious-draw")
                    || !elementRows.Any(value => value.ReceiptId == "acceptance:history-element-draw"))
                    throw new InvalidOperationException("History did not expire old rows and trim oldest rows by precious and shared element categories.");
            });
        }

        private static void VerifyItemSourceResolution()
        {
            WithAcceptanceProfile("source", (profile, catalog, now) =>
            {
                DateTime localNow = DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime().DateTime;
                TreasureGachaSeries series = catalog.SortedSeries().First(value =>
                    catalog.IsSeriesOpen(value, profile.Save.Player, localNow,
                        profile.Save.CreatedAtUnixSeconds, out _)
                    && value.ChestGachaItems.Length > 0);
                int relicId = series.ChestGachaItems[0];
                int pieceId = catalog.Treasure(relicId)?.TreasurePieceId ?? 0;
                if (pieceId <= 0) throw new InvalidOperationException("The source navigation fixture has no official relic piece.");
                var service = new OfflineTreasureGachaService(profile, catalog);
                TreasureGachaSourceNavigation relic = service.ResolveItemSource(relicId, now);
                TreasureGachaSourceNavigation piece = service.ResolveItemSource(pieceId, now);
                TreasureGachaSourceNavigation unknown = service.ResolveItemSource(int.MaxValue, now);
                TreasureGachaConfig elementGacha = catalog.Gachas.First(value => value.CountAchievementType == 8);
                int elementItemId = service.ProbabilityItems(elementGacha.GachaType, 0, now).First().ItemId;
                int elementType = catalog.Item(elementItemId).ElementType;
                TreasureGachaSourceNavigation element = service.ResolveElementItemSource(elementItemId,
                    elementType, now);
                TreasureGachaSourceNavigation wrongElement = service.ResolveElementItemSource(elementItemId,
                    elementType + 1, now);
                if (relic == null || piece == null || relic.ResolvedTreasureItemId != relicId
                    || relic.SelectedSeriesId != series.ChestGachaId
                    || piece.SourceItemId != pieceId || piece.ResolvedTreasureItemId != relicId
                    || piece.SelectedSeriesId != series.ChestGachaId
                    || unknown != null || element == null
                    || element.SelectedElementGachaType != elementGacha.GachaType || wrongElement != null)
                    throw new InvalidOperationException("Item-source navigation did not apply official precious and element source rules.");
            });
        }

        private static OfflineTreasureDrawRecord CreateHistoryRecord(string receipt, int gachaType, long timestamp)
        {
            return new OfflineTreasureDrawRecord
            {
                ReceiptId = receipt,
                GachaType = gachaType,
                SeriesId = 0,
                UnixSeconds = timestamp,
                Awards = new List<OfflineTreasureAward>
                { new OfflineTreasureAward { ItemId = 250, Count = 1 } }
            };
        }

        private static void WithAcceptanceProfile(string name,
            Action<OfflineMainlineProgressSession, TreasureGachaRuntimeCatalog, long> verify)
        {
            string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "zjcs-treasure-gacha-" + name + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var slots = new ProfileSlotCatalog(tempRoot);
                string slotId = slots.CreateSlot();
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                TreasureGachaRuntimeCatalog catalog = OfficialTreasureGachaAssets.LoadDefault();
                long now = new DateTimeOffset(new DateTime(2026, 1, 1, 12, 0, 0,
                    DateTimeKind.Local)).ToUnixTimeSeconds();
                ProgressionResult setup = profile.ApplyCharacterMutation(player =>
                {
                    player.CharacterCreated = true;
                    player.CharacterName = "Treasure Gacha " + name;
                    player.Level = 99;
                    player.Rank = 99;
                    player.MaxProfessionRank = 99;
                    foreach (TreasureGachaSeries row in catalog.Series)
                        if (!player.UnlockedMapClassIds.Contains(row.MapClassId))
                            player.UnlockedMapClassIds.Add(row.MapClassId);
                    foreach (TreasureCondition condition in catalog.Conditions)
                        if (condition.Type == "MapUnlockCondition" && condition.MapClassId > 0
                            && !player.UnlockedMapClassIds.Contains(condition.MapClassId))
                            player.UnlockedMapClassIds.Add(condition.MapClassId);
                    for (int rank = 1; rank <= player.Rank; rank++)
                        player.RankUnlockTimes.Add(new OfflineRankUnlockTime
                        { Rank = rank, UnixSeconds = now - 400L * 86400 });
                    foreach (int itemId in catalog.Gachas.Select(value => value.CostItemId).Distinct())
                        player.Inventory.Add(new OfflineItemStack { ItemId = itemId, Count = 100000 });
                    player.Inventory.Add(new OfflineItemStack { ItemId = 2, Count = 100000 });
                }, "acceptance:treasure-gacha:" + slotId);
                if (setup.Status != ProgressionResultStatus.Applied)
                    throw new InvalidOperationException("Could not prepare a local gacha profile: " + setup.Status);
                verify(profile, catalog, now);
            }
            finally
            {
                string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!tempRoot.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to remove a gacha acceptance path outside the temp directory.");
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        private static IEnumerable<HomeUiNode> EnumerateNodes(HomeUiNode root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (HomeUiNode child in root.children ?? Array.Empty<HomeUiNode>())
                foreach (HomeUiNode descendant in EnumerateNodes(child))
                    yield return descendant;
        }

        private static void VerifyPerformanceCatalog()
        {
            TreasureGachaPerformanceData data = TreasureGachaPerformancePlayer.LoadDefaultData();
            var keys = new HashSet<string>(data.animations.Select(value => value.key), StringComparer.Ordinal);
            string capsule = "CAB-f8b8d4723f8c600b96a5c17edc5cc61c:-3592130628675380995:machine:";
            string[] elements = { "0", "100", "101", "102", "103", "104" };
            foreach (string element in elements)
                for (int quality = 1; quality <= 5; quality++)
                    for (int position = 1; position <= 10; position++)
                    {
                        string prefix = capsule + quality + ":" + element + ":";
                        string suffix = position.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                        if (!keys.Contains(prefix + "shock_" + suffix) || !keys.Contains(prefix + "qiu_" + suffix))
                            throw new InvalidOperationException("An official capsule quality, element or position animation is missing.");
                    }
            if (!keys.Contains("CAB-f8b8d4723f8c600b96a5c17edc5cc61c:-2360317434235585549:loop")
                || !keys.Contains("CAB-f8b8d4723f8c600b96a5c17edc5cc61c:-1126455669465147305:loop"))
                throw new InvalidOperationException("An official treasure gacha machine body loop is missing.");
        }
    }
}
