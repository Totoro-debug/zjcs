using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World.MainlinePlayerState;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private static object ProjectLocalPlayer(MainlineSharedWorld world)
        {
            var save = new OfflineMainlineSave();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "共享原玩家";
            save.Player.Level = 20;
            save.Player.SubRank = 5;
            save.Player.InitialWeaponType = 201;
            save.Player.Wardrobe.Sex = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession = 1, Level = 20 });
            save.Player.Stories.Add(new OfflineStoryProgress { Key = "projection-fixture", Count = 3, Status = 2 });
            save.World.CompletedEntityTags.Add("projection-completed-tag");
            var stageParser = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("StageInfoParser"));
            var originalStages = ((IEnumerable)stageParser.GetType().GetMethod("GetAll").Invoke(stageParser, null)).Cast<object>().ToArray();
            var mainStage = originalStages.First(value => Convert.ToInt32(NativeMap14SharedPrefabs.Get(value, "Type")) == 1
                && Convert.ToInt32(NativeMap14SharedPrefabs.Get(value, "Reset")) == 0);
            int stageId = (int)NativeMap14SharedPrefabs.Get(mainStage, "Id");
            save.World.StageWinCounts.Add(new OfflineStageWinCount { StageId = stageId, Count = 7 });
            var maximum = (IDictionary)NativeMap14SharedPrefabs.Get(world.Data.Placement, "MaxRegionProgressDict");
            var ids = maximum.Keys.Cast<int>().OrderBy(value => value).ToArray();
            var maps = JsonUtility.FromJson<PlacementExpected>(
                Resources.Load<TextAsset>("MainlineSharedWorld/MapConfiguration/placement-expected").text).Maps;
            var rawByMap = maps.ToDictionary(map => map.MapClassId,
                map => map.Regions.ToDictionary(region => region.Region, region => region.Region * 7 + 3));
            foreach (var map in maps)
            {
                save.World.Maps.Add(new OfflineMapProgress { MapClassId = map.MapClassId,
                    Regions = map.Regions.Select(region => new OfflineRegionProgress { Region = region.Region, Progress = 10000 }).ToList() });
                var goddess = map.Regions.FirstOrDefault(region => region.GoddessId > 0);
                if (goddess != null) save.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = map.MapClassId, Region = goddess.Region });
                var mapParser = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("MapInfoWrapParser"));
                var args = new object[] { map.MapClassId, null };
                mapParser.GetType().GetMethod("TryLoadMapInfoWrap").Invoke(mapParser, args);
                var placements = ((IDictionary)NativeMap14SharedPrefabs.Get(args[1], "RegionInfos")).Values.Cast<IEnumerable>()
                    .SelectMany(values => values.Cast<object>());
                var witch = placements.FirstOrDefault(value => NativeMap14SharedPrefabs.Get(value, "DefSpawnEntityInfo") is object spawn
                    && Convert.ToInt32(NativeMap14SharedPrefabs.Get(spawn, "StageType")) == 216);
                if (witch != null) save.World.ActivatedWitches.Add(new OfflineMapEntityKey
                    { MapClassId = map.MapClassId, PlacementId = (int)NativeMap14SharedPrefabs.Get(witch, "LoEntityId") });
            }
            var raw = rawByMap[world.Data.MapClassId];
            string before = JsonUtility.ToJson(save);
            var reads = new Dictionary<int, int>();
            var player = MainlineSharedPlayerProjection.Create(world, save, 731, map =>
            {
                reads[map] = reads.TryGetValue(map, out int count) ? count + 1 : 1;
                return rawByMap[map];
            });
            object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
            var type = player.GetType();
            if (reads.Count != maps.Length || reads.Values.Any(count => count != 1) || before != JsonUtility.ToJson(save)
                || !ReferenceEquals(Get(Get(player, "DataWrap"), "Player"), player)
                || (long)Get(player, "Id") != 731 || (string)Get(player, "Name") != save.Player.CharacterName
                || (int)Get(player, "Level") != 20 || Convert.ToInt32(Get(player, "SubRank")) != 5
                || Convert.ToInt32(Get(player, "Sex")) != 1 || Convert.ToInt32(Get(player, "Profession")) != 1)
                throw new InvalidDataException("Original player identity, wrap owner or local save isolation differs.");
            foreach (var map in maps)
                foreach (var region in map.Regions)
                    if ((int)type.GetMethod("GetWorldMapRegionProgress").Invoke(player, new object[] { map.MapClassId, region.Region }) != rawByMap[map.MapClassId][region.Region])
                        throw new InvalidDataException("Original player received normalized rather than raw progress.");
            var regionStatuses = (IDictionary)Get(player, "MapRegionStatusDict");
            if (regionStatuses.Count != maps.Length)
                throw new InvalidDataException("Visited-map status restoration added or omitted a map.");
            var metadataType = world.Assemblies.CommonType("MapInfoManager");
            var metadata = metadataType.GetProperty("Instance").GetValue(null);
            foreach (var map in maps)
            {
                var info = metadataType.GetMethod("GetMapPartInfo").Invoke(metadata, new object[] { map.MapClassId });
                int defaultStatus = Convert.ToInt32(Get(info, "MapDefaultRegionStatus"));
                var statuses = (IList)regionStatuses[map.MapClassId];
                if (statuses.Count != map.Regions.Length)
                    throw new InvalidDataException("Original visited-map status indices differ.");
                foreach (var region in map.Regions)
                {
                    int expectedStatus = save.World.ActivatedGoddessRegions.Any(value =>
                        value.MapClassId == map.MapClassId && value.Region == region.Region) ? 2 : defaultStatus;
                    if (Convert.ToInt32(type.GetMethod("GetMapRegionStatus").Invoke(player, new object[] { map.MapClassId, region.Region })) != expectedStatus)
                        throw new InvalidDataException("Original cross-map status query lost a default or public goddess region.");
                }
            }
            // A snapshot must not reveal a map that this local slot never visited.
            var partial = JsonUtility.FromJson<OfflineMainlineSave>(before);
            int absentMap = maps.Last().MapClassId;
            partial.World.Maps.RemoveAll(map => map.MapClassId == absentMap);
            partial.World.ActivatedGoddessRegions.RemoveAll(value => value.MapClassId == absentMap);
            partial.World.ActivatedWitches.RemoveAll(value => value.MapClassId == absentMap);
            string partialBefore = JsonUtility.ToJson(partial);
            var partialPlayer = MainlineSharedPlayerProjection.Create(world, partial, 732, map => rawByMap[map]);
            if (((IDictionary)Get(partialPlayer, "MapRegionStatusDict")).Contains(absentMap)
                || Convert.ToInt32(type.GetMethod("GetMapRegionStatus").Invoke(partialPlayer, new object[] { absentMap, 0 })) != 0
                || partialBefore != JsonUtility.ToJson(partial))
                throw new InvalidDataException("A cold region projection revealed an unvisited map or mutated its save.");
            // Contiguous but truncated data must not silently use a partial map.
            var malformed = JsonUtility.FromJson<OfflineMainlineSave>(before);
            var truncated = malformed.World.Maps.First(map => map.Regions.Count > 1);
            truncated.Regions.RemoveAt(truncated.Regions.Count - 1);
            bool coverageRejected = false;
            try
            {
                MainlineSharedPlayerProjection.Create(world, malformed, 733, map => map == truncated.MapClassId
                    ? rawByMap[map].Where(pair => truncated.Regions.Any(region => region.Region == pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value)
                    : rawByMap[map]);
            }
            catch (InvalidDataException) { coverageRejected = true; }
            if (!coverageRejected || before != JsonUtility.ToJson(save))
                throw new InvalidDataException("Incomplete official region coverage was accepted or changed the source save.");
            Debug.Log("MAINLINE_PLAYER_SAVED_REGION_STATUS_PASS maps=" + maps.Length + " regions=" + maps.Sum(map => map.Regions.Length)
                + " unvisited=none truncated=rejected input=unchanged");
            raw[ids[0]] = int.MaxValue;
            if ((int)type.GetMethod("GetWorldMapRegionProgress").Invoke(player, new object[] { world.Data.MapClassId, ids[0] }) == int.MaxValue)
                throw new InvalidDataException("Original player shares the mutable input progress dictionary.");
            var stories = Get(player, "StoryManager");
            var originalStory = stories.GetType().GetProperty("Item").GetValue(stories, new object[] { "projection-fixture" });
            if ((int)Get(originalStory, "Count") != 3 || Convert.ToInt32(Get(originalStory, "Status")) != 2
                || (DateTime)Get(originalStory, "LastPlayingTime") != default
                || (int)type.GetMethod("GetStoryCount").Invoke(player, new object[] { "projection-fixture" }) != 3
                || Convert.ToInt32(type.GetMethod("GetStoryStatus").Invoke(player, new object[] { "projection-fixture" })) != 2)
                throw new InvalidDataException("Original story projection changed the saved state or fabricated a timestamp.");
            var profession = Get(player, "ProfessionData");
            if (Convert.ToInt32(Get(profession, "InitialWeaponType")) != 201
                || (int)((IDictionary)Get(Get(profession, "UnlockedData"), "RawData"))[Enum.ToObject(world.Assemblies.CommonType("Profession"), 1)] != 20)
                throw new InvalidDataException("Original profession projection lost its weapon or unlocked level.");
            if ((int)type.GetMethod("GetStageCount").Invoke(player, new object[] { stageId }) != 7
                || (DateTime)Get(type.GetMethod("GetStageData").Invoke(player, new object[] { stageId }), "LastUpdateTime") != default)
                throw new InvalidDataException("Original permanent stage count differs or received a fabricated timestamp.");
            foreach (var map in maps)
            {
                foreach (var region in map.Regions)
                    if ((bool)type.GetMethod("IsGoddessActive").Invoke(player, new object[] { map.MapClassId, region.Region })
                        != save.World.ActivatedGoddessRegions.Any(value => value.MapClassId == map.MapClassId && value.Region == region.Region))
                        throw new InvalidDataException("Original goddess activation query lost its cross-map placement.");
                var witchType = Enum.Parse(world.Assemblies.CommonType("MapWorldActiveableEntityType"), "Witch");
                if ((int)type.GetMethod("GetMapActiveEntCount").Invoke(player, new[] { (object)map.MapClassId, witchType })
                    != save.World.ActivatedWitches.Count(value => value.MapClassId == map.MapClassId))
                    throw new InvalidDataException("Original per-map witch count differs.");
            }
            if (save.World.ActivatedGoddessRegions.Count == 0 || save.World.ActivatedWitches.Count == 0
                || (int)type.GetMethod("GetAllMapActiveEntCount").Invoke(player, new[] {
                    Enum.Parse(world.Assemblies.CommonType("MapWorldActiveableEntityType"), "Witch") }) != save.World.ActivatedWitches.Count)
                throw new InvalidDataException("Original global witch count differs or the activation fixture is empty.");
            var key = Activator.CreateInstance(world.Assemblies.CommonType("BlackboardDataKeyMapLogicEntityEnd"), "projection-completed-tag");
            if ((long)type.GetMethods().Single(method => method.Name == "GetBlackboardDataValue" && method.IsGenericMethod
                && method.GetGenericArguments().Length == 1)
                .MakeGenericMethod(typeof(long)).Invoke(player, new[] { key }) != 1)
                throw new InvalidDataException("Original tag completion condition lost its blackboard value.");
            bool rejected = false;
            var reset = Get(mainStage, "Reset");
            try
            {
                // Explicit invalid-input fixture; the selected table contains
                // no daily stage. Always restore the original cached record.
                NativeMap14SharedPrefabs.Set(mainStage, "Reset", Enum.ToObject(world.Assemblies.CommonType("StageResetType"), 1));
                try { MainlineSharedPlayerProjection.Create(world, save, 731, _ => raw); }
                catch (NotSupportedException) { rejected = true; }
            }
            finally { NativeMap14SharedPrefabs.Set(mainStage, "Reset", reset); }
            if (!rejected || (int)type.GetMethod("GetStageCount").Invoke(player, new object[] { stageId }) != 7)
                throw new InvalidDataException("Undated daily victories were accepted or changed the projected player.");
            Debug.Log("MAINLINE_PLAYER_PROJECTION_PASS map=" + world.Data.MapClassId + " raw_regions=" + ids.Length);
            return player;
        }
    }
}
