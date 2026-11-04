using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.MainlineNativeGround
{
    public static partial class NativeGroundAcceptance
    {
        private static IEnumerator VerifyLabelContext()
        {
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var colors = Resources.Load<CombatRatingConfig>("Map13/Native/combat-rating-config");
            var font = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-text-catalog")
                .Load<TMP_FontAsset>("Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
            var files = new LocalLanguageFiles(Application.streamingAssetsPath);
            var source13 = JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
            var source16 = JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map16/Native/region-label-data").text);
            // Region IDs, rather than serialized row order, address player state.
            Array.Reverse(source13.regions);
            string original13 = JsonUtility.ToJson(source13), original16 = JsonUtility.ToJson(source16);
            var definition = JsonUtility.FromJson<OfficialMapDefinition>(File.ReadAllText(Path.Combine(repository,
                "unity/Assets/Official/World/Map/16/map16.json")));
            DateTime now = DateTime.Now;
            var player = new OfflinePlayerProgress { Rank = 4 };
            player.RankUnlockTimes.Add(new OfflineRankUnlockTime
                { Rank = 4, UnixSeconds = new DateTimeOffset(now.AddDays(-13)).ToUnixTimeSeconds() });
            IMapRegionTime clock = new OfflineCharacterRankRegionTime(definition, () => player, () => now);
            var latePlayer = new OfflinePlayerProgress { Rank = 5 };
            IMapRegionTime otherClock = new OfflineCharacterRankRegionTime(definition, () => latePlayer, () => now);
            var state13 = new LabelState(); var state16 = new LabelState();
            var owner = new object();
            using (var context13 = new MainlineMapLabelContext(source13, state13, owner, colors, font, files))
            using (var context16 = new MainlineMapLabelContext(source16, state16, owner, colors, font, files, clock))
            using (var other16 = new MainlineMapLabelContext(source16, new LabelState(), owner, colors, font, files, otherClock))
            {
                Require(ReferenceEquals(context13.Map, owner) && context13.MapClassId == 13 && context16.MapClassId == 16,
                    "The public context retains its map identity and borrowed owner.");
                Require(!ReferenceEquals(context13.Data, source13) && !ReferenceEquals(context13.Data.regionGrid, source13.regionGrid)
                    && !ReferenceEquals(context16.Data.regions[0], other16.Data.regions[0]), "Independent original configuration copies.");
                int timed = source16.regions.First(row => row.LimitServerRank == (Rank)4).Region;
                Require(context16.GetRegion(16, timed).IsLimitedByType(130) == MapRegionLimitType.RegionUnlockTime
                    && !other16.GetRegion(16, timed).IsLimited(130), "Two contexts keep independent rank clocks.");
                now = clock.GetUnlockTime(timed).AddTicks(-1);
                Require(context16.GetRegion(16, timed).IsLimited(130), "Before exact local unlock boundary.");
                now = clock.GetUnlockTime(timed);
                Require(!context16.GetRegion(16, timed).IsLimited(130), "Exact local unlock boundary.");
                now = now.AddDays(-10);
                var events = new List<string>();
                context16.OnPlanesRankUnlock += rank => events.Add("rank:" + (int)rank);
                player.Rank = 5; context16.Refresh();
                Require(!context16.GetRegion(16, timed).IsLimited(130) && events.SequenceEqual(new[] { "rank:5" }),
                    "The same context refreshes original rank listeners and bypasses the date at higher rank.");
                Require(context13.GetMapRegionStatus(16, 0) == MapRegionStatus.None
                    && context13.GetWorldMapRegionProgress(16, 0) == 0, "Cross-map state does not leak.");
                events.Clear();
                context13.OnLevelUpdate += _ => events.Add("level");
                context13.OnCombatRatingChanged += (previous, current) =>
                { Require(previous == 100000 && current == 200000, "Original combat-rating event values."); events.Add("combat"); };
                context13.OnMapStatusUpdate += (map, region, previous, current) =>
                { Require(map == 13 && region == 0 && previous == MapRegionStatus.Private && current == MapRegionStatus.Public,
                    "Original region status event values."); events.Add("region"); };
                context13.OnWorldMapProgressUpdate += (map, values) =>
                { Require(map == 13 && values[0] == 3 && values.Skip(1).All(value => value == 0),
                    "Progress arrays use region ID despite reversed source enumeration."); events.Add("progress"); };
                state13.Level++; state13.Rating = 200000; state13.Public = true; state13.Progress = 3;
                context13.Refresh();
                Require(events.SequenceEqual(new[] { "level", "combat", "region", "progress" }), "Original label event order.");
                context13.Refresh();
                Require(events.Count == 4 && context16.GetWorldMapRegionProgress(16, 0) == 0, "No duplicate refresh or shared player state.");
                context13.Data.regionGrid[0] = -123;
                Require(JsonUtility.ToJson(source13) == original13 && JsonUtility.ToJson(source16) == original16,
                    "Borrowed map sources remain unchanged after state and time changes.");
                context13.Dispose(); context13.Dispose();
                Require(context13.Map == null && font && colors && other16.Map != null, "Idempotent disposal preserves other contexts and borrowed assets.");
                bool rejected = false;
                try { context13.Refresh(); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "Disposed contexts reject refresh.");
            }
            yield return null; yield return null;
        }
        private sealed class LabelState : IMainlineWorldLabelState
        {
            public int Level = 130, Progress; public long Rating = 100000; public bool Public;
            public int PlayerLevel => Level;
            public int PlayerSubRank => 12;
            public long PlayerCombatRating => Rating;
            public bool IsPublicRegion(int region) => region == 0 && Public;
            public int GetRegionProgress(int region) => region == 0 ? Progress : 0;
        }
    }
}
