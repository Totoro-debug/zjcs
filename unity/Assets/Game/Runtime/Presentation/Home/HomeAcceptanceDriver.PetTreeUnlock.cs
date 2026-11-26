using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeUI;
using Zjcs.Offline.State;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomePetGachaUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyNativePetTreeUnlock(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            GameObject tree = runtime.NativeFurnitureInstance(EFurniture.FruitTree);
            var pet = tree.GetComponentInChildren<HomeFruitTreePet>(true);
            bool passed = Check("pet-unlock-initial", () =>
            {
                var state = runtime.Profile.Save.Player.FruitTree;
                Assert(!state.ProductionRepaired && !state.PetFacilityRepaired
                    && runtime.Profile.Save.World.ActivatedGoddessRegions.Count == 0,
                    "isolated pet-tree profile has neither repair nor a production unlock goddess");
                Assert(pet.GetState() == FurnitureBase.EFurnitureState.CanRepair
                    && tree.GetComponentInChildren<HomeFruitTreeProduceFruit>(true).GetState() == FurnitureBase.EFurnitureState.Locked,
                    "official pet repair condition is met independently of locked production");
                return "native pet repair is available while production remains locked";
            });
            if (!passed) { completed(false); yield break; }
            CenterUnlockTree(runtime, tree);
            yield return ClickUnlockTree(runtime, tree, true, EFurniture.FruitTree2);
            float deadline = Time.realtimeSinceStartup + 15;
            while (!runtime.IsFurnitureStoryRunning && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-started", () =>
            {
                Assert(runtime.IsFurnitureStoryRunning && runtime.StoryStatus("FruitTreeUnlock2") == 1,
                    "native pet-side collider starts the original second unlock graph");
                return "official pet unlock graph starts and persists its running state";
            });
            bool dialogue = false;
            yield return AdvanceUnlockDialogue(runtime, "pet-unlock-before-repair", value => dialogue = value);
            passed &= dialogue;
            if (!passed) { completed(false); yield break; }
            yield return ClickUnlockTree(runtime, tree, true, EFurniture.FruitTree2);
            deadline = Time.realtimeSinceStartup + 8;
            FurnitureRepairDialog repair;
            while (!(repair = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairDialog>())
                && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-repair-window", () =>
            {
                Assert(repair && repair.Furniture == EFurniture.FruitTree2 && repair.RepairBtn,
                    "pet graph opens its own original repair window");
                return "native pet description, model and repair confirmation are bound";
            });
            if (!passed) { completed(false); yield break; }
            yield return Capture("pet-unlock-repair-window.png");
            ClickPartnerUi(repair.RepairBtn);
            deadline = Time.realtimeSinceStartup + 15;
            while (!runtime.Profile.Save.Player.FruitTree.PetFacilityRepaired && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-independent-repair", () =>
            {
                runtime.Profile.Reload();
                var state = runtime.Profile.Save.Player.FruitTree;
                Assert(state.PetFacilityRepaired && !state.ProductionRepaired && state.StoredCount == 0
                    && !state.ProductionRepairStorySeen && !state.CollectionTutorialSeen,
                    "the confirmed offline repair changes only pet ownership without production stock or tutorials");
                return "pet repair commits without the official additional production repair";
            });
            if (!passed) { completed(false); yield break; }
            bool notification = false;
            yield return CloseUnlockRepairNotification(runtime, "pet-unlock", value => notification = value);
            passed &= notification;
            if (!passed) { completed(false); yield break; }
            dialogue = false;
            yield return AdvanceUnlockDialogue(runtime, "pet-unlock-after-repair", value => dialogue = value);
            passed &= dialogue;
            deadline = Time.realtimeSinceStartup + 12;
            while (runtime.IsFurnitureStoryRunning && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-completed", () =>
            {
                runtime.Profile.Reload();
                var state = runtime.Profile.Save.Player.FruitTree;
                Assert(!runtime.IsFurnitureStoryRunning && runtime.StoryStatus("FruitTreeUnlock2") == 2
                    && runtime.StoryCount("FruitTreeUnlock2") == 1 && state.PetRepairStorySeen
                    && !state.ProductionRepaired && runtime.StoryStatus("FruitTreeUnlock1") == 0,
                    "pet graph finishes exactly once without starting or completing production");
                Assert(pet.GetState() == FurnitureBase.EFurnitureState.Functional,
                    "original pet sub-building reaches its functional state");
                return "original pet unlock graph and independent repair are committed";
            });
            yield return Capture("pet-unlock-completed.png");
            if (passed)
            {
                bool hints = false;
                yield return VerifyPetTreeIndicators(runtime, tree, value => hints = value);
                passed &= hints;
            }
            if (passed)
            {
                runtime.Close();
                yield return null;
                var slots = new ProfileSlotCatalog(Root);
                var restored = HomeFeatureRuntime.BeginWithNativePointer(slots.GetSlotIds().First(), Root, null,
                    new AcceptanceNativePointer(), null, null);
                restored.TutorialAutoStartEnabled = false;
                for (int frame = 0; frame < 5; frame++) yield return null;
                passed &= Check("pet-unlock-restored", () =>
                {
                    var state = restored.Profile.Save.Player.FruitTree;
                    Assert(state.PetFacilityRepaired && !state.ProductionRepaired && state.StoredCount == 0
                        && state.PetRepairStorySeen && !state.ProductionRepairStorySeen
                        && !restored.IsFurnitureStoryRunning && restored.StoryCount("FruitTreeUnlock2") == 1
                        && restored.StoryStatus("FruitTreeUnlock2") == 2 && restored.StoryStatus("FruitTreeUnlock1") == 0,
                        "cold city reopening preserves independent pet repair and one story completion");
                    return "pet completion restores without additional production repair or replay";
                });
                restored.Close();
            }
            completed(passed);
        }

        private IEnumerator VerifyPetTreeIndicators(HomeFeatureRuntime runtime, GameObject tree, Action<bool> completed)
        {
            var achievement = tree.GetComponentInChildren<AchievementRedDotItem>(true);
            int scoreType = runtime.PetGacha.Catalog.Gacha.ScoreAchievementType;
            int group = runtime.PetGacha.Catalog.Achievements.First(row => row.AchievementType == scoreType).Group;
            long target = runtime.PetGacha.Achievements(group, 0, 1).Single().Target;
            var seed = runtime.Profile.ApplyInventoryTransaction("acceptance:pet-tree:score-hint", save => save.Player.PetGacha.Score = target);
            Assert(seed.IsApplied, "isolated score boundary commits");
            float deadline = Time.realtimeSinceStartup + 8;
            while (achievement.IsMeet != true && Time.realtimeSinceStartup < deadline) yield return null;
            bool passed = Check("pet-unlock-hints-score-ready", () =>
            {
                Assert(achievement.IsShow && achievement.ShowRedDot(), "native achievement indicator refreshes from committed local score");
                return "official score achievement key and native update event expose an available reward";
            });
            if (!passed) { completed(false); yield break; }
            yield return Capture("pet-unlock-score-hint.png");
            var claim = runtime.PetGacha.ClaimAchievement(group, 0, "acceptance:pet-tree:claim-hint", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            deadline = Time.realtimeSinceStartup + 8;
            while (achievement.IsMeet != false && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-hints-score-claimed", () =>
            {
                Assert(claim.IsApplied && !achievement.IsShow && !achievement.ShowRedDot(),
                    "real local achievement claim removes the original indicator through its reward event");
                return "committed reward claims refresh the original achievement indicator";
            });
            int independent = runtime.PetGacha.Catalog.AchievementsFor(group)
                .Count(row => row.CompleteCount > 0 && row.CycleIncrementCount <= 0);
            int cycleIndex = independent + 1;
            var cycle = runtime.PetGacha.Achievements(group, cycleIndex / 100, 100)[cycleIndex % 100];
            seed = runtime.Profile.ApplyInventoryTransaction("acceptance:pet-tree:cycle-hint", save =>
            {
                save.Player.PetGacha.Score = cycle.Target;
                for (int index = 0; index < cycleIndex; index++)
                    if (!save.Player.PetGacha.AchievementClaims.Any(row => row.Group == group && row.Index == index))
                        save.Player.PetGacha.AchievementClaims.Add(new OfflinePetGachaAchievementClaim { Group = group, Index = index });
            });
            Assert(seed.IsApplied, "isolated historical claims and the next cycle boundary commit");
            deadline = Time.realtimeSinceStartup + 8;
            while (achievement.IsMeet != true && Time.realtimeSinceStartup < deadline) yield return null;
            bool cycleReady = achievement.IsShow && runtime.PetGacha.HasAvailableAchievementReward(group);
            claim = runtime.PetGacha.ClaimAchievement(group, cycleIndex, "acceptance:pet-tree:claim-cycle", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            deadline = Time.realtimeSinceStartup + 8;
            while (achievement.IsMeet != false && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("pet-unlock-hints-cycle", () =>
            {
                Assert(cycleReady && claim.IsApplied && !achievement.IsShow && !runtime.PetGacha.HasAvailableAchievementReward(group),
                    "availability follows the first unclaimed cycle target and the actual next-cycle reward claim");
                return "independent and cycle achievements share the local availability predicate and native reward events";
            });
            var material = tree.GetComponentInChildren<GachaBlueDotForSetupMinCount>(true);
            int item = runtime.PetGacha.Catalog.Gacha.CostItemId;
            int minimum = PrivateField<int>(material, "mMinCountForShow");
            Assert(material.GachaServices.TryGetChestCost(runtime.PetGacha.Catalog.Gacha.ChestId, item, out long cost), "original pet chest material cost is bound");
            foreach (int amount in new[] { minimum - 1, minimum, minimum - 1 })
            {
                var result = runtime.Profile.ApplyInventoryTransaction("acceptance:pet-tree:material-hint:" + Guid.NewGuid().ToString("N"), save =>
                {
                    var stack = save.Player.Inventory.FirstOrDefault(row => row.ItemId == item);
                    if (stack == null) { stack = new OfflineItemStack { ItemId = item }; save.Player.Inventory.Add(stack); }
                    stack.Count = cost * amount;
                });
                Assert(result.IsApplied, "isolated material boundary commits");
                bool expected = amount >= minimum;
                deadline = Time.realtimeSinceStartup + 8;
                while (material.IsMeet != expected && Time.realtimeSinceStartup < deadline) yield return null;
                var gacha = material.GachaServices.GetGacha((Zjcs.Official.HomeTipDots.GachaType)runtime.PetGacha.Catalog.Gacha.GachaType);
                log.Add("VIEW pet material hint amount=" + amount + "; count=" + material.GachaServices.GetItemAmount(item)
                    + "; meet=" + material.IsMeet + "; show=" + material.IsShow + "; fresh=" + material.ShowRedDot()
                    + "; active=" + material.gameObject.activeInHierarchy + "; enabled=" + material.enabled
                    + "; policy=" + material.IsActive + "; gacha-limit=" + material.GachaServices.IsGachaFunctionLimited(gacha.GachaType)
                    + "; daily-limit=" + material.GachaServices.IsGachaDailyCountLimited(gacha.GachaClassType)
                    + "; chest-limit=" + material.GachaServices.IsChestFunctionLimited(gacha.ChestId));
                passed &= material.IsShow == expected && material.ShowRedDot() == expected;
            }
            passed &= Check("pet-unlock-hints-material", () =>
            {
                Assert(passed, "native material indicator follows below/at/below its original serialized threshold");
                return "actual inventory changes drive the original material cost, minimum count and event subscriptions";
            });
            completed(passed);
        }
    }
}
