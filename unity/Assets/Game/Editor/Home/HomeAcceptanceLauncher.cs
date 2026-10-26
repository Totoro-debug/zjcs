using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Home
{
    /// <summary>
    /// Launches the home Play Mode acceptance run:
    /// Unity.exe -projectPath unity -executeMethod Zjcs.Editor.Home.HomeAcceptanceLauncher.Run -homeAcceptance
    /// The run creates an isolated profile, enters Play Mode, captures screenshots
    /// and exits the editor with the result code.
    /// </summary>
    public static class HomeAcceptanceLauncher
    {
        private static bool autoRefreshSuspended;

        private static string Root => Environment.GetEnvironmentVariable("ZJCS_HOME_ACCEPTANCE_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "zjcs-home-acceptance");

        public static void Run()
        {
            try
            {
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshSuspended = true;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                string runId = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
                Environment.SetEnvironmentVariable("ZJCS_HOME_ACCEPTANCE_ROOT",
                    Path.Combine(Path.GetTempPath(), "zjcs-home-acceptance-" + runId));
                Environment.SetEnvironmentVariable("ZJCS_HOME_ACCEPTANCE_OUTPUT",
                    Path.Combine(Directory.GetCurrentDirectory(), "output", "home-acceptance-" + runId));
                EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
                ConfigurePortraitGameView();
                PrepareProfile();
                Debug.Log("[HomeAcceptance] entering play mode with " + Root);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                RestoreAssetRefresh();
                Debug.LogError("[HomeAcceptance] launcher failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) RestoreAssetRefresh();
        }

        private static void RestoreAssetRefresh()
        {
            if (!autoRefreshSuspended) return;
            autoRefreshSuspended = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssetDatabase.AllowAutoRefresh();
        }

        private static void ConfigurePortraitGameView(int width = 450, int height = 800)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.ToObject(groupType, 1) });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
            MethodInfo countMethod = group.GetType().GetMethod("GetTotalCount");
            MethodInfo getSizeMethod = group.GetType().GetMethod("GetGameViewSize");
            int count = (int)countMethod.Invoke(group, null);
            int selectedIndex = -1;
            if (getSizeMethod != null)
            {
                for (int index = 0; index < count; index++)
                {
                    object existing = getSizeMethod.Invoke(group, new object[] { index });
                    Type existingType = existing.GetType();
                    int existingWidth = (int)existingType.GetProperty("width").GetValue(existing);
                    int existingHeight = (int)existingType.GetProperty("height").GetValue(existing);
                    string display = existingType.GetProperty("displayText").GetValue(existing) as string;
                    if (existingWidth == width && existingHeight == height
                        && display == "Home Acceptance Portrait")
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }
            if (selectedIndex < 0)
            {
                object size = Activator.CreateInstance(sizeType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { Enum.ToObject(kindType, 1), (object)width, height,
                        "Home Acceptance Portrait" }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selectedIndex = (int)countMethod.Invoke(group, null) - 1;
            }
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic).SetValue(view, selectedIndex);
        }

        private static void PrepareProfile()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length > 0)
                throw new IOException("The acceptance profile folder must be empty: " + Root);
            Directory.CreateDirectory(Root);
            var slots = new ProfileSlotCatalog(Root);
            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            void Apply(string step, Action<OfflinePlayerProgress> mutation)
            {
                ProgressionResult result = session.ApplyCharacterMutation(mutation,
                    "acceptance:character:" + slotId + ":" + step);
                if (result.Status != ProgressionResultStatus.Applied
                    && result.Status != ProgressionResultStatus.Unchanged
                    && result.Status != ProgressionResultStatus.Ready)
                    throw new InvalidOperationException("The acceptance profile " + step + " could not be created: "
                        + result.Status + ". " + result.Message + ". " + DescribeValidation(session.Save));
            }
            Apply("identity", player =>
            {
                player.CharacterName = "验收角色";
                player.CharacterCreated = true;
                player.InitialWeaponType = 200;
            });
            Apply("progression", player =>
            {
                player.Experience = 100000;
                player.Level = 20;
                player.SubRank = 3;
                player.Rank = 1;
                player.MaxProfessionRank = 1;
                player.Wardrobe.Profession = 1;
            });
            Apply("crystal", player =>
            {
                if (player.Inventory == null) player.Inventory = new System.Collections.Generic.List<OfflineItemStack>();
                OfflineItemStack crystal = player.Inventory.FirstOrDefault(value => value.ItemId == 120);
                if (crystal == null)
                {
                    crystal = new OfflineItemStack { ItemId = 120 };
                    player.Inventory.Add(crystal);
                }
                crystal.Count = checked(crystal.Count + 5);
            });
            Apply("gold", player =>
            {
                OfflineItemStack gold = player.Inventory.FirstOrDefault(value => value.ItemId == 1);
                if (gold == null)
                {
                    gold = new OfflineItemStack { ItemId = 1 };
                    player.Inventory.Add(gold);
                }
                gold.Count = Math.Max(gold.Count, 10000000);
            });
            Apply("unlocked-maps", player =>
            {
                if (player.UnlockedMapClassIds == null)
                    player.UnlockedMapClassIds = new System.Collections.Generic.List<int>();
                foreach (int mapId in new[] { 11, 12, 13, 14, 17 })
                    if (!player.UnlockedMapClassIds.Contains(mapId)) player.UnlockedMapClassIds.Add(mapId);
            });
            Debug.Log("[HomeAcceptance] profile " + slotId + " ready at " + Root);
        }

        private static string DescribeValidation(OfflineMainlineSave source)
        {
            OfflineMainlineSave save = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(source));
            Type sessionType = typeof(OfflineMainlineProgressSession);
            sessionType.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { save });
            OfflinePlayerProgress player = save.Player;
            player.CharacterName = "验收角色";
            player.CharacterCreated = true;
            player.InitialWeaponType = 200;
            bool Invoke(string name, params object[] args) => (bool)sessionType.GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            return "validation components: wishes=" + OfflineWishProgress.IsValid(player.Wishes)
                + ", treasure=" + OfflineTreasureGachaProgress.IsValid(player.TreasureGacha)
                + ", petGacha=" + OfflinePetGachaProgress.IsValid(player.PetGacha)
                + ", fruitTree=" + OfflineFruitTreeProgress.IsValid(player.FruitTree)
                + ", gathering=" + OfflineMaterialGatheringProgress.IsValid(player.MaterialGathering)
                + ", skills=" + OfflineSkillProgression.IsValid(player.Skills)
                + ", pets=" + OfflinePetProgress.IsValid(player.Pets, player.Skills)
                + ", characterProperties=" + OfflineCharacterPropertyProgress.IsValid(player.CharacterProperties)
                + "[season=" + player.CharacterProperties.SeasonId
                + ", treasureRank=" + player.CharacterProperties.TreasurePlaneRank
                + ", astrologicalCost=" + player.CharacterProperties.AstrologicalCost
                + ", library=" + player.CharacterProperties.LegionLibraryLevel
                + ", buildings=" + string.Join(";", player.CharacterProperties.LegionBuildingLevels
                    .Select(value => value == null ? "null" : value.BuildingId + ":" + value.Level))
                + ", upgrade=" + (player.CharacterProperties.LegionBuildingUpgrade == null ? "null"
                    : player.CharacterProperties.LegionBuildingUpgrade.BuildingId + ":"
                        + player.CharacterProperties.LegionBuildingUpgrade.HangUpType + ":"
                        + player.CharacterProperties.LegionBuildingUpgrade.TargetLevel + ":"
                        + player.CharacterProperties.LegionBuildingUpgrade.Progress + "/"
                        + player.CharacterProperties.LegionBuildingUpgrade.RequiredProgress + ":"
                        + player.CharacterProperties.LegionBuildingUpgrade.LastProgressUnixSeconds + ":"
                        + player.CharacterProperties.LegionBuildingUpgrade.ProgressRemainder)
                + ", cooking=" + string.Join(";", player.CharacterProperties.CookingUses
                    .Select(value => value == null ? "null" : value.CookingId + ":" + value.UseTimes))
                + ", extra=" + player.CharacterProperties.ExtraProperties.Count
                + ", friendships=" + player.CharacterProperties.NpcFriendships.Count
                + ", talentTrees=" + player.CharacterProperties.NpcTalentTrees.Count
                + ", treasures=" + player.CharacterProperties.Treasures.Count
                + ", elements=" + player.CharacterProperties.TreasureElementLevels.Count
                + ", periodic=" + player.CharacterProperties.PeriodicCards.Count
                + ", mapSkills=" + player.CharacterProperties.LegionMapSkills.Count
                + ", cabinet=" + player.CharacterProperties.EquipCabinetScores.Count + "]"
                + ", npcTravel=" + OfflineNpcTravelProgress.IsValid(player.NpcTravel)
                + ", homeShop=" + OfflineHomeShopProgress.IsValid(player.HomeShop)
                + ", wardrobe=" + Invoke("ValidateWardrobe", player.Wardrobe, player.Inventory)
                + ", equipment=" + Invoke("ValidateEquipment",
                    player.GetType().GetField("Equipment").GetValue(player))
                + ", inventoryCount=" + player.Inventory.Count
                + ", mapIds=" + string.Join(",", player.UnlockedMapClassIds ?? new System.Collections.Generic.List<int>())
                + ", overall=" + Invoke("Validate", save);
        }
    }
}
