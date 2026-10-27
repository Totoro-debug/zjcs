using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfficialMap12PlacedChest
    {
        public int PlacementId, EntityClassId, Region, Area, X, Y, Level, SubRank, RegionLevel, RegionSubRank,
            PrivateLevelType, PublicLevelType, CompleteState;
        public bool Absolute;
        public int[] RuleIds, DropItems;
        public string LogicTag;

        // WorldEntityLevelComponent.GetLevelAndSubRank: public chests use the
        // player's level; private chests retain the Lo-level initialization.
        public (int level, int subRank) RewardLevel(int playerLevel, int playerSubRank, bool publicRegion)
        {
            int type = publicRegion ? PublicLevelType : PrivateLevelType;
            if (type == 2) return (playerLevel, playerSubRank);
            if (type != 1) throw new InvalidDataException("Unbound Map12 chest entity level type: " + type);
            return (Absolute ? Level : checked(RegionLevel + Level), SubRank == 0 ? RegionSubRank : SubRank);
        }
    }
    [Serializable] public sealed class OfficialMap12ChestData
    {
        public string manifest;
        public int MapGroupId;
        public OfficialMap12PlacedChest[] chests;
        public OfficialWorldExplorationData rewards;
    }
    public sealed class OfficialMap12ChestCatalog
    {
        public OfficialMap12ChestData Data { get; }
        public OfficialMap12ChestCatalog(string json)
        {
            Data = JsonUtility.FromJson<OfficialMap12ChestData>(json);
            if (Data?.manifest != "871_156598" || Data.chests?.Length != 83 || Data.rewards?.rewardRules == null
                || Data.chests.Select(v => v.PlacementId).Distinct().Count() != 83
                || Data.chests.Any(v => v.CompleteState != 1 || v.RuleIds == null || v.EntityClassId < 8200 || v.EntityClassId > 8203))
                throw new InvalidDataException("Selected Map12 placed chest bindings are incomplete.");
            var raw = Object(Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(json));
            foreach (var row in Values(Object(raw["rewards"]), "rewardRules").Select(Object)
                .Zip(Data.rewards.rewardRules, (source, target) => (source, target)))
                if (row.source["Condition"] == null) row.target.Condition = null;
        }
        public OfficialMap12PlacedChest Find(int placementId) => Data.chests.SingleOrDefault(v => v.PlacementId == placementId);
        public static string CompletionTag(int placementId) => "map12:entity:" + placementId;
        public static OfficialMap12ChestCatalog Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Map12/chests");
            if (asset == null) throw new InvalidDataException("Local Map12 chest resources are missing.");
            return new OfficialMap12ChestCatalog(asset.text);
        }
    }

    public sealed partial class OfflineWorldExplorationService
    {
        public ProgressionResult CollectMap12Chest(OfficialMap12ChestCatalog catalog, int placementId, bool publicRegion,
            long now, out IReadOnlyDictionary<int, long> awards)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            session.Reload();
            var output = new Dictionary<int, long>();
            awards = output;
            OfficialMap12PlacedChest chest = catalog.Find(placementId);
            if (chest == null || now <= 0) return Failure("Invalid placed Map12 chest.", ProgressionResultStatus.InvalidState);
            string tag = OfficialMap12ChestCatalog.CompletionTag(placementId);
            if (session.Save.World.CompletedEntityTags.Contains(tag)) return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            string receipt = "map12-chest:" + placementId;
            ProgressionResult result = session.ApplyInventoryTransaction(receipt, save =>
            {
                var level = chest.RewardLevel(save.Player.Level, save.Player.SubRank, publicRegion);
                // Use the chest's original region, even when the player stands
                // across its boundary. Reward RNG and inventory commit together.
                var context = new OfflineExploreContext { MapClassId = 12, MapGroupId = catalog.Data.MapGroupId, Area = chest.Area,
                    Region = chest.Region, X = chest.X, Y = chest.Y, Level = level.level, SubRank = level.subRank,
                    RegionLevel = chest.RegionLevel, RegionSubRank = chest.RegionSubRank, DropItems = chest.DropItems };
                foreach (int id in chest.RuleIds) GrantRule(save, id, context, now, output, catalog.Data.rewards.rewardRules);
                save.World.CompletedEntityTags.Add(tag);
                if (!string.IsNullOrEmpty(chest.LogicTag) && !save.World.CompletedEntityTags.Contains(chest.LogicTag))
                    save.World.CompletedEntityTags.Add(chest.LogicTag);
            }, chest.EntityClassId, 1);
            if (!result.IsApplied) output.Clear();
            return result;
        }
    }
}
