using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static partial class DailyDungeonModuleRuleAcceptance
    {
        private static void ValidateLeaderboardGifts(ProfileSlotCatalog slots, DailyDungeonRuntimeData catalog)
        {
            string slot = slots.CreateSlot();
            DailyDungeonModule module = DailyDungeonModule.Open(slots, slot, catalog);
            DailyDungeonLeaderboardGift first = module.BuildLeaderboardGiftModel(11, 0).Single(row => row.RequiredCount == 4);
            if (first.SourceId != 1200001 || first.State != DailyDungeonAchievementState.Processing
                || first.Rewards.Count != 2 || first.Rewards.Sum(row => row.Count) != 10
                || module.BuildLeaderboardGiftModel(121, 50).Count != 0
                || module.ClaimLeaderboardGift(first.Id, (_, __) => throw new Exception("Premature grant")))
                throw new InvalidOperationException("Official Type 57 thresholds, rewards or private-route exclusion changed.");
            int initialClaims = module.RemainEntries;
            List<int> route = FindTerminalRoute(module, module.FindMap(module.ResolveLevel(11, 0).MapGroupId).startRoomId);
            if (route == null) throw new InvalidOperationException("World Tree gift test route is missing.");
            for (int run = 0; run < 4; run++)
            {
                if (!module.TryBegin(11, 0, _ => true, (_, __) => true, (_, __, ___) => true,
                    catalog.settings.DailyDungeonBuyCostItemId, DateTimeOffset.Now.ToUnixTimeSeconds(), out string error))
                    throw new InvalidOperationException(error);
                foreach (int room in route)
                {
                    if (module.ActiveRun.currentRoomId != room && !module.ChooseNextRoom(room))
                        throw new InvalidOperationException("Gift test route selection failed.");
                    if (!module.RecordRoomCleared(room, string.Empty))
                        throw new InvalidOperationException("Gift test route room clear failed.");
                }
                int grade = module.CurrentGrade();
                if (grade != 7 || !module.Finish(grade) || module.Finish(grade))
                    throw new InvalidOperationException("A route did not count exactly once on official S completion.");
                first = module.BuildLeaderboardGiftModel(11, 0).Single(row => row.Id == first.Id);
                if (first.Progress != run + 1 || module.BuildLeaderboardGiftModel(11, 1).Any(row => row.Progress != 0))
                    throw new InvalidOperationException("Type 57 progress leaked across difficulties or lost a completion.");
            }
            module = DailyDungeonModule.Open(slots, slot, catalog);
            first = module.BuildLeaderboardGiftModel(11, 0).Single(row => row.Id == first.Id);
            if (first.Progress != 4 || first.State != DailyDungeonAchievementState.Completed
                || module.RemainEntries != initialClaims || module.DailyDungeonGetRewardCount != 0
                || module.ClaimLeaderboardGift(first.Id, (_, __) => false))
                throw new InvalidOperationException("Gift progress did not persist independently from claim counts.");
            var receipts = new HashSet<string>();
            string savePath = Path.Combine(slots.GetSlotDirectory(slot), "daily-dungeon-save.json");
            bool failed = false;
            using (var locked = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { module.ClaimLeaderboardGift(first.Id, (receipt, _) => { receipts.Add(receipt); return true; }); }
                catch (IOException) { failed = true; }
            }
            if (!failed) throw new InvalidOperationException("Gift write-failure fixture did not block persistence.");
            module = DailyDungeonModule.Open(slots, slot, catalog);
            if (!module.ClaimLeaderboardGift(first.Id, (receipt, _) => { receipts.Add(receipt); return true; })
                || receipts.Count != 1 || module.ClaimLeaderboardGift(first.Id, (_, __) => throw new Exception("Duplicate grant")))
                throw new InvalidOperationException("Gift recovery changed its receipt or allowed a duplicate claim.");
            module = DailyDungeonModule.Open(slots, slot, catalog);
            if (module.BuildLeaderboardGiftModel(11, 0).Single(row => row.Id == first.Id).State != DailyDungeonAchievementState.Received)
                throw new InvalidOperationException("Received leaderboard gift did not survive cold reopen.");
        }
    }
}
