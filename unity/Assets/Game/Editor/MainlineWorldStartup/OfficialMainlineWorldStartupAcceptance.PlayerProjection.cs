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
            save.World.Maps.Add(new OfflineMapProgress { MapClassId = world.Data.MapClassId,
                Regions = ids.Select(id => new OfflineRegionProgress { Region = id, Progress = 10000 }).ToList() });
            var raw = ids.ToDictionary(id => id, id => id * 7 + 3);
            string before = JsonUtility.ToJson(save);
            int reads = 0;
            var player = MainlineSharedPlayerProjection.Create(world, save, 731, map =>
            {
                if (map != world.Data.MapClassId) throw new InvalidDataException("Projection read an unvisited map.");
                reads++;
                return raw;
            });
            object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
            var type = player.GetType();
            if (reads != 1 || before != JsonUtility.ToJson(save)
                || !ReferenceEquals(Get(Get(player, "DataWrap"), "Player"), player)
                || (long)Get(player, "Id") != 731 || (string)Get(player, "Name") != save.Player.CharacterName
                || (int)Get(player, "Level") != 20 || Convert.ToInt32(Get(player, "SubRank")) != 5
                || Convert.ToInt32(Get(player, "Sex")) != 1 || Convert.ToInt32(Get(player, "Profession")) != 1)
                throw new InvalidDataException("Original player identity, wrap owner or local save isolation differs.");
            foreach (int region in ids)
                if ((int)type.GetMethod("GetWorldMapRegionProgress").Invoke(player, new object[] { world.Data.MapClassId, region }) != raw[region])
                    throw new InvalidDataException("Original player received normalized rather than raw progress.");
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
