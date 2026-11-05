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

            var progress = (IDictionary)Get(player, "WorldMapRegionProgressDict");
            foreach (var map in save.World.Maps)
            {
                var points = readRegionPoints(map.MapClassId);
                var regions = map.Regions.Select(value => value.Region).OrderBy(value => value).ToArray();
                if (points == null || !regions.SequenceEqual(Enumerable.Range(0, regions.Length))
                    || !regions.SequenceEqual(points.Keys.OrderBy(value => value)) || points.Values.Any(value => value < 0))
                    throw new InvalidDataException("Local raw region points differ from the visited original map: " + map.MapClassId);
                progress.Add(map.MapClassId, regions.Select(region => points[region]).ToList());
            }
            var update = playerType.GetMethod("UpdateBlackboardData").MakeGenericMethod(typeof(long));
            foreach (string tag in save.World.CompletedEntityTags)
            {
                var key = Activator.CreateInstance(shared.CommonType("BlackboardDataKeyMapLogicEntityEnd"), tag);
                update.Invoke(player, new[] { key, (object)1L });
            }
            return player;
        }

        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
    }
}
