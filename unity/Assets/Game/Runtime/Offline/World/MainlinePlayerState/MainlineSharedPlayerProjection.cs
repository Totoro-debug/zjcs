using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.MainlinePlayerState
{
    // Builds a detached original player before its real ClientInit. This ports
    // the local condition inputs only; it does not fabricate inventory, equipment,
    // server timestamps or a replacement property calculation.
    public static class MainlineSharedPlayerProjection
    {
        public static object Create(MainlineSharedWorld world, OfflineMainlineSave save, long playerId,
            Func<int, IReadOnlyDictionary<int, int>> readRegionPoints)
        {
            if (world == null || !world.IsLoaded || world.IsReady)
                throw new InvalidOperationException("Load the original client map before projecting its local player.");
            if (save?.Player == null || save.World == null) throw new ArgumentNullException(nameof(save));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (readRegionPoints == null) throw new ArgumentNullException(nameof(readRegionPoints));
            var shared = world.Assemblies;
            var playerType = shared.CommonType("Player");
            var player = playerType.GetMethod("CreateNewPlayer").Invoke(null, null);
            var source = save.Player;
            Set(player, "Id", playerId);
            Set(player, "Name", source.CharacterName);
            Set(player, "Level", source.Level);
            Set(player, "SubRank", Enum.ToObject(shared.CommonType("SubRank"), source.SubRank));
            Set(player, "Sex", Enum.ToObject(shared.CommonType("Sex"), source.Wardrobe.Sex));
            var profession = Get(player, "ProfessionData");
            var professionType = shared.CommonType("Profession");
            Set(profession, "CurProfession", Enum.ToObject(professionType, source.Wardrobe.Profession));
            Set(profession, "InitialWeaponType", Enum.ToObject(shared.CommonType("ItemType"), source.InitialWeaponType));
            var unlocked = (IDictionary)Get(Get(profession, "UnlockedData"), "RawData");
            foreach (var value in source.ProfessionLevels)
                unlocked.Add(Enum.ToObject(professionType, value.Profession), value.Level);
            Set(player, "ActiveableEntityDataDict", ReadActivations(world, player, save));

            var stories = Get(player, "StoryManager");
            var storyType = shared.CommonType("PlayerStory");
            var addStory = stories.GetType().GetMethod("Add", new[] { typeof(string), storyType });
            foreach (var value in source.Stories)
            {
                var story = Activator.CreateInstance(storyType);
                Set(story, "PlayerId", playerId);
                Set(story, "Key", value.Key);
                Set(story, "Count", value.Count);
                Set(story, "Status", Enum.ToObject(shared.CommonType("StoryStatus"), value.Status));
                // Restore the recorded fields directly; SetStoryStatus would
                // stamp a new LastPlayingTime while merely reading a save.
                addStory.Invoke(stories, new[] { (object)value.Key, story });
            }

            var stages = Get(player, "StageManager");
            var stageType = shared.CommonType("PlayerStageDataWrap");
            var stageParser = world.Configuration.Manager.GetParser(shared.CommonType("StageInfoParser"));
            var getStage = stageParser.GetType().GetMethod("Get", new[] { typeof(int) });
            var addStage = stages.GetType().GetMethod("Add", new[] { typeof(int), stageType });
            foreach (var value in save.World.StageWinCounts)
            {
                var info = getStage.Invoke(stageParser, new object[] { value.StageId });
                if (info == null) throw new InvalidDataException("Saved map stage is absent from the original table: " + value.StageId);
                // The mainline save records lifetime map victories. Daily stages
                // need their own dated records; never stamp today's date onto
                // an undated lifetime count during a projection.
                if (Convert.ToInt32(Get(info, "Reset")) != 0)
                    throw new NotSupportedException("A resetting stage requires its dated local record: " + value.StageId);
                var stage = Activator.CreateInstance(stageType);
                Set(stage, "PlayerId", playerId);
                Set(stage, "StageId", value.StageId);
                Set(stage, "WinCount", value.Count);
                addStage.Invoke(stages, new[] { (object)value.StageId, stage });
            }

            var progress = (IDictionary)Get(player, "WorldMapRegionProgressDict");
            var regionParser = world.Configuration.Manager.GetParser(shared.CommonType("MapRegionInfoParser"));
            var regionRows = regionParser.GetType().GetMethod("GetMapRegionDict");
            var setRegionStatus = playerType.GetMethod("SetMapRegionStatus");
            var statusType = shared.CommonType("MapRegionStatus");
            foreach (var map in save.World.Maps)
            {
                var points = readRegionPoints(map.MapClassId);
                var regions = map.Regions.Select(value => value.Region).OrderBy(value => value).ToArray();
                if (points == null || !regions.SequenceEqual(Enumerable.Range(0, regions.Length))
                    || !regions.SequenceEqual(points.Keys.OrderBy(value => value)) || points.Values.Any(value => value < 0))
                    throw new InvalidDataException("Local raw region points differ from the visited original map: " + map.MapClassId);
                var originalRegions = regionRows.Invoke(regionParser, new object[] { map.MapClassId }) as IDictionary;
                if (originalRegions == null || !regions.SequenceEqual(originalRegions.Keys.Cast<int>().OrderBy(id => id)))
                    throw new InvalidDataException("Saved region coverage differs from the original visited map: " + map.MapClassId);
                progress.Add(map.MapClassId, regions.Select(region => points[region]).ToList());
                // The original progress message initializes a visited map with region -1.
                // Its own MapDefaultRegionStatus selects private/public defaults.
                // Restore before ClientInit so reading a save emits no live transition.
                setRegionStatus.Invoke(player, new object[] { map.MapClassId, -1, Enum.ToObject(statusType, 0) });
                foreach (var activated in save.World.ActivatedGoddessRegions.Where(value => value.MapClassId == map.MapClassId))
                {
                    if (!originalRegions.Contains(activated.Region))
                        throw new InvalidDataException("Activated goddess region is absent from the original map.");
                    setRegionStatus.Invoke(player, new object[] { map.MapClassId, activated.Region, Enum.ToObject(statusType, 2) });
                }
            }
            var update = playerType.GetMethod("UpdateBlackboardData").MakeGenericMethod(typeof(long));
            foreach (string tag in save.World.CompletedEntityTags)
            {
                var key = Activator.CreateInstance(shared.CommonType("BlackboardDataKeyMapLogicEntityEnd"), tag);
                update.Invoke(player, new[] { key, (object)1L });
            }
            return player;
        }

        public static void SynchronizeRegionProgress(object player, int mapClassId, IReadOnlyDictionary<int, int> points)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (mapClassId <= 0 || points == null || points.Count == 0
                || !points.Keys.OrderBy(id => id).SequenceEqual(Enumerable.Range(0, points.Count))
                || points.Values.Any(value => value < 0))
                throw new InvalidDataException("Original map progress needs contiguous nonnegative raw region points.");
            var values = Enumerable.Range(0, points.Count).Select(id => points[id]).ToList();
            var progress = (IDictionary)Get(player, "WorldMapRegionProgressDict");
            if (progress.Contains(mapClassId) && ((IEnumerable<int>)progress[mapClassId]).SequenceEqual(values)) return;
            player.GetType().GetMethod("SetWorldMapProgress").Invoke(player, new object[] { mapClassId, values });
        }

        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);

        public static void SynchronizeActivations(MainlineSharedWorld world, object player, OfflineMainlineSave save)
        {
            if (world == null || !world.IsLoaded) throw new InvalidOperationException("Load the original map before projecting activations.");
            if (player == null || save?.World == null) throw new ArgumentNullException(nameof(save));
            // Validate the complete local snapshot before publishing any original event.
            var desired = ReadActivations(world, player, save);
            var data = (IDictionary)Get(player, "ActiveableEntityDataDict");
            var messageType = world.Assemblies.CommonType("MapActiveableEntityInfo");
            var update = player.GetType().GetMethod("OnUpdateActiveableEntity");
            foreach (DictionaryEntry entry in desired)
                foreach (DictionaryEntry kind in (IDictionary)entry.Value)
                    foreach (int id in (List<int>)kind.Value)
                    {
                        var current = data.Contains(entry.Key) ? (IDictionary)data[entry.Key] : null;
                        if (current != null && current.Contains(kind.Key) && ((List<int>)current[kind.Key]).Contains(id)) continue;
                        // Each message owns an independent snapshot. The original pre-update
                        // callback must still see the old list; one activation produces one event.
                        var next = (IDictionary)Activator.CreateInstance(desired.GetType().GetGenericArguments()[1]);
                        if (current != null)
                            foreach (DictionaryEntry previous in current)
                                next.Add(previous.Key, new List<int>((List<int>)previous.Value));
                        if (!next.Contains(kind.Key)) next.Add(kind.Key, new List<int>());
                        ((List<int>)next[kind.Key]).Add(id);
                        var message = Activator.CreateInstance(messageType);
                        Set(message, "MapClassId", entry.Key);
                        Set(message, "ActiveEntType", kind.Key);
                        Set(message, "ActiveEntLoId", id);
                        Set(message, "ActiveableEntDataDict", next);
                        update.Invoke(player, new[] { message });
                    }
        }

        private static IDictionary ReadActivations(MainlineSharedWorld world, object player, OfflineMainlineSave save)
        {
            var data = (IDictionary)Activator.CreateInstance(Get(player, "ActiveableEntityDataDict").GetType());
            var activeType = world.Assemblies.CommonType("MapWorldActiveableEntityType");
            var metadataType = world.Assemblies.CommonType("MapRegionEntityInfoManager");
            var metadata = metadataType.GetProperty("Instance").GetValue(null);
            var goddessId = metadataType.GetMethod("GetMapRegionGoddessId");
            var placement = metadataType.GetMethod("GetContainerEntityInfo");
            void Add(int mapId, string kind, int id)
            {
                if (id <= 0) throw new InvalidDataException("Activated original " + kind + " placement is absent: " + mapId);
                if (!data.Contains(mapId)) data.Add(mapId, Activator.CreateInstance(data.GetType().GetGenericArguments()[1]));
                var map = (IDictionary)data[mapId];
                var key = Enum.Parse(activeType, kind);
                if (!map.Contains(key)) map.Add(key, new List<int>());
                var ids = (List<int>)map[key];
                if (ids.Contains(id)) throw new InvalidDataException("Duplicate original statue activation: " + mapId + "/" + id);
                ids.Add(id);
            }
            foreach (var region in save.World.ActivatedGoddessRegions)
                Add(region.MapClassId, "Goddess", (int)goddessId.Invoke(metadata, new object[] { region.MapClassId, region.Region }));
            foreach (var witch in save.World.ActivatedWitches)
            {
                var info = placement.Invoke(metadata, new object[] { witch.MapClassId, witch.PlacementId });
                var spawn = info == null ? null : Get(info, "DefSpawnEntityInfo");
                if (spawn == null || !Equals(Get(spawn, "StageType"), Enum.Parse(world.Assemblies.CommonType("MapEntityWorldStageType"), "Witch")))
                    throw new InvalidDataException("Activated witch differs from its original placement: " + witch.MapClassId + "/" + witch.PlacementId);
                Add(witch.MapClassId, "Witch", witch.PlacementId);
            }
            return data;
        }
    }
}
