using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionProgressionValidation
    {
        [MenuItem("Zjcs/Validate/Profession Local Transactions")]
        public static void Run()
        {
            OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
            var package = (Dictionary<string, object>)OfficialEquipmentJson.Parse(assets.CharacterPanels.text);
            var rules = (Dictionary<string, object>)package["profession_rules"];
            var achievements = new OfficialProfessionAchievementRules(((List<object>)rules["profession_achievement"])
                .Cast<Dictionary<string, object>>().OrderBy(row => Convert.ToInt32(row["Id"])));
            string root = Path.Combine(Path.GetTempPath(), "zjcs-profession-validation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                var tasks = OfficialMainlineTaskCatalog.LoadDefault();
                var session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
                var service = new OfflineCharacterProgressionService(session, assets.Load());
                Require(session.ApplyCharacterMutation(player =>
                {
                    player.ProfessionLevels = new List<OfflineProfessionLevel>
                    {
                        new OfflineProfessionLevel { Profession = 10, Level = 3 },
                        new OfflineProfessionLevel { Profession = 1, Level = 2 }
                    };
                    player.Inventory.RemoveAll(item => item.ItemId == 42200 || item.ItemId == 303);
                    player.Inventory.Add(new OfflineItemStack { ItemId = 42200, Count = long.MaxValue - 500 });
                }).IsApplied, "Seed an isolated screenshot-equivalent profession state");
                string before = JsonUtility.ToJson(session.Save);
                byte[] beforeDisk = File.ReadAllBytes(slots.GetProgressPath(slot));
                Require(service.ClaimProfessionAchievements(achievements).Status == ProgressionResultStatus.InvalidState,
                    "Reject an overflowing original item reward");
                Require(JsonUtility.ToJson(session.Save) == before && File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(beforeDisk),
                    "Overflow rolls back the entire group reward and all achievement records");
                Require(session.ApplyCharacterMutation(player => player.Inventory.RemoveAll(item => item.ItemId == 42200)).IsApplied,
                    "Remove the isolated overflow fixture");
                before = JsonUtility.ToJson(session.Save);
                beforeDisk = File.ReadAllBytes(slots.GetProgressPath(slot));
                bool writeFailed = false;
                using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { service.ClaimProfessionAchievements(achievements); }
                    catch (IOException) { writeFailed = true; }
                }
                Require(writeFailed, "Inject an actual local save write failure");
                Require(JsonUtility.ToJson(session.Save) == before && File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(beforeDisk),
                    "Failed write preserves both memory and the previous save");
                Require(service.ClaimProfessionAchievements(achievements).IsApplied,
                    "Claim all four original completed rows in one transaction");
                Require(Count(session.Save.Player, 42200) == 1200 && Recorded(session.Save.Player) == 4,
                    "Persist exactly 1200 original reward items and four packed achievement identities");
                byte[] claimed = File.ReadAllBytes(slots.GetProgressPath(slot));
                Require(service.ClaimProfessionAchievements(achievements).Status == ProgressionResultStatus.Unchanged
                    && File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(claimed),
                    "Repeated group claim neither duplicates rewards nor rewrites the save");
                var restored = OfflineMainlineProgressSession.Open(slots, slot, tasks);
                var restoredService = new OfflineCharacterProgressionService(restored, assets.Load());
                Require(Count(restored.Save.Player, 42200) == 1200 && Recorded(restored.Save.Player) == 4
                    && restoredService.ClaimProfessionAchievements(achievements).Status == ProgressionResultStatus.Unchanged,
                    "Cold load restores rewards and repeat protection");
                Require(restored.ApplyCharacterMutation(player => player.ProfessionLevels[0].Level = 4).IsApplied,
                    "Advance the isolated total profession level to six");
                Require(restoredService.ClaimProfessionAchievements(achievements).IsApplied
                    && Count(restored.Save.Player, 42200) == 1200 && Count(restored.Save.Player, 303) == 2
                    && Recorded(restored.Save.Player) == 5,
                    "Claim the original level-six cycle seed without repeating earlier rewards");
                var afterCycle = OfflineMainlineProgressSession.Open(slots, slot, tasks);
                Require(Count(afterCycle.Save.Player, 303) == 2 && Recorded(afterCycle.Save.Player) == 5,
                    "Cold load restores the cycle seed reward");
                string beforePreview = JsonUtility.ToJson(restored.Save);
                foreach (int profession in new[] { 1, 10 }) restoredService.ResolveProfessionAppearance(profession);
                Require(JsonUtility.ToJson(restored.Save) == beforePreview,
                    "Profession preview preserves saved equipment, appearance and current profession");
                Debug.Log("Profession local transactions validated: group rewards, overflow and disk-failure rollback, cold reload, repeat protection, cycle seed and non-mutating appearance previews.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        public static void ValidateRecoveredDynamicAtlas()
        {
            const string path = "Assets/ProfessionNativeAtlasProbe.asset";
            try
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Require(texture != null && texture.name == "MPLUSRounded1c-Medium Atlas",
                    "Import the original zero-size native Texture2D object");
                Require(texture.width == 0 && texture.height == 0 && texture.format == TextureFormat.Alpha8
                    && texture.isReadable && texture.GetRawTextureData().Length == 0,
                    "Retain original empty Alpha8 atlas dimensions, readability and payload");
                Require(texture.filterMode == FilterMode.Bilinear && texture.anisoLevel == 1
                    && texture.wrapMode == TextureWrapMode.Repeat && texture.mipMapBias == 0f,
                    "Retain original dynamic atlas filtering and wrapping");
                Debug.Log("Profession original zero-size dynamic font atlas imported and verified in Unity.");
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
        private static long Count(OfflinePlayerProgress player, int item) => player.Inventory
            .Where(stack => stack.ItemId == item).Sum(stack => stack.Count);
        private static int Recorded(OfflinePlayerProgress player) => player.AppliedCharacterEventIds
            .Count(id => id.StartsWith("profession-achievement:", StringComparison.Ordinal));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Profession transaction validation failed: " + message);
        }
    }
}
