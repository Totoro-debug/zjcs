using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Zjcs.Offline.Progression;
namespace Zjcs.Editor
{
    public static class RefreshClockAcceptance
    {
        public static void Run()
        {
            ProjectTools.Validate();
            RunRules();
            ActivityDomainBossPageDataAcceptance.Run();
            Zjcs.Editor.Progression.OfflineWorldExplorationValidation.Run();
        }
        public static void RunRules()
        {
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Validation workspace required.");
            var open = new DateTime(2026, 11, 9, 6, 0, 0, DateTimeKind.Local);
            Require(OfflineRefreshClock.DayStart(open.AddSeconds(-1)) == open.AddDays(-1), "05:59:59 is the previous day");
            Require(OfflineRefreshClock.DayStart(open) == open && OfflineRefreshClock.DayStart(open.AddHours(2)) == open, "06:00 switches once; 08:00 does not refresh again");
            Require(OfflineNpcTravelService.LocalTravelDayKey(open.AddSeconds(-1)) == 20261108 && OfflineNpcTravelService.LocalTravelDayKey(open) == 20261109, "Travel refresh boundary");
            var pet = (OfflinePetGachaService)FormatterServices.GetUninitializedObject(typeof(OfflinePetGachaService));
            typeof(OfflinePetGachaService).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pet, OfficialPetGachaAssets.LoadDefault());
            var day = typeof(OfflinePetGachaService).GetMethod("DayKey", BindingFlags.NonPublic | BindingFlags.Instance);
            var week = typeof(OfflinePetGachaService).GetMethod("WeekKey", BindingFlags.NonPublic | BindingFlags.Instance);
            Require((int)day.Invoke(pet, new object[]{open.AddSeconds(-1)}) == 20261108 && (int)day.Invoke(pet, new object[]{open}) == 20261109, "Pet daily count follows 06:00, not midnight");
            Require((int)week.Invoke(pet, new object[]{open.AddSeconds(-1)}) == 20261102 && (int)week.Invoke(pet, new object[]{open}) == 20261109, "Pet weekly count follows Monday 06:00");
            var commerce = (Zjcs.Offline.Commerce.OfflineCommerceSession)FormatterServices.GetUninitializedObject(typeof(Zjcs.Offline.Commerce.OfflineCommerceSession));
            var today = commerce.GetType().GetMethod("Today", BindingFlags.NonPublic | BindingFlags.Instance);
            Require((int)today.Invoke(commerce, new object[]{open}) == 1 + (int)today.Invoke(commerce, new object[]{open.AddSeconds(-1)}), "Commerce daily and weekly epoch switches at 06:00");
            var shop = (OfflineHomeShopService)FormatterServices.GetUninitializedObject(typeof(OfflineHomeShopService));
            var shopCatalog = OfficialHomeShopCatalog.Load(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets/StreamingAssets/HomeShop/rules.json"));
            shop.GetType().GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shop, shopCatalog);
            var cycle = shop.GetType().GetMethod("WeekCycleKey", BindingFlags.NonPublic | BindingFlags.Instance);
            var monday = open;
            // Identifiers retain the official 08:00 anchor for save compatibility.
            long beforeKey = (long)cycle.Invoke(shop, new object[]{new DateTimeOffset(monday.AddSeconds(-1)).ToUnixTimeSeconds()});
            long afterKey = (long)cycle.Invoke(shop, new object[]{new DateTimeOffset(monday).ToUnixTimeSeconds()});
            Require(afterKey > beforeKey && afterKey == (long)cycle.Invoke(shop, new object[]{new DateTimeOffset(monday.AddHours(2)).ToUnixTimeSeconds()}), "Shop weekly refresh at 06:00 occurs once and retains legacy stock identifiers");
            string dailyPath = Path.Combine(root, "legacy-daily.json");
            var legacy = new DailyChallengeSave { firstEnteredLocalTicks = open.AddDays(-2).Ticks, highestLogicDayTicks = open.AddHours(2).Ticks };
            legacy.activities.Add(new DailyChallengeActivitySave { activityId = 209, logicDay = open.AddHours(2).Ticks.ToString(), participationCount = 1 });
            File.WriteAllText(dailyPath, JsonUtility.ToJson(legacy));
            var daily = new OfflineDailyChallengeService(dailyPath);
            Require(daily.GetLogicDayStartLocal(open.AddHours(3)) == open, "Legacy daily high-water boundary normalizes to 06:00");
            Require(!daily.CanParticipate(209, open.AddHours(3), 335, 1, out _), "Legacy daily participation is not cleared by changing the boundary");
            string weeklyPath = Path.Combine(root, "legacy-weekly.json");
            var oldWeek = open.AddDays(-1).AddHours(2);
            var oldSave = new WeeklyChallengeSave();
            oldSave.periods.Add(new WeeklyChallengePeriodSave { startLocalTicks = oldWeek.Ticks });
            File.WriteAllText(weeklyPath, JsonUtility.ToJson(oldSave));
            var weekly = new OfflineWeeklyChallengeSession(weeklyPath);
            Require(weekly.CurrentPeriodStartLocalTicks(open) == oldWeek.Ticks, "Existing weekly identity preserves checkpoint and reward receipts");
            WeeklyChallengeSessionAcceptance.Run();
            Debug.Log("[Refresh clock] PASS: 06:00 boundaries, daily/weekly pet counters, travel, legacy day/week saves, weekly rewards and stock identifiers.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
