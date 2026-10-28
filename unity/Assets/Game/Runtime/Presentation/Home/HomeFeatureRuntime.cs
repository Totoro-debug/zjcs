using System;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Offline.Commerce;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Inventory;
using Zjcs.Presentation.Cooking;
using Zjcs.Presentation.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Wishes;
using Zjcs.Presentation.Combat;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Entry point of the offline home module.  It opens the character save and
    /// the home save, rebuilds the official scene and HUD, and routes every
    /// furniture action through the offline rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeFeatureRuntime : MonoBehaviour, IInventoryHomeNavigationContext
    {
        private const int OfficialMailFunctionType = 52;
        private const int HomeTutorialFixedRewardId = 7;
        private const string HomeTutorialFixedRewardReceipt = "home:tutorial:fixed-reward:7";
        private ProfileSlotCatalog slots;
        private OfflineMainlineProgressSession profile;
        private OfflineDailyChallengeService dailyChallenges;
        private OfflineNpcTravelService npcTravel;
        private OfficialNpcTravelCatalog npcTravelCatalog;
        private OfflineNpcTravelPropertyCalculator npcTravelProperties;
        private OfflineDailyDungeonSession dailyDungeons;
        private OfficialFantasyTowerCatalog fantasyTowerCatalog;
        private OfflineFantasyTowerSession fantasyTower;
        private OfflineNpcAssistanceProgress npcAssistanceProgress;
        private FightOfficialNpcAssistanceCatalog npcAssistanceCatalog;
        private OfflineArenaSession arena;
        private OfflineCharacterProgressionService progression;
        private OfflineAstrologicalService astrological;
        private float astrologicalChargeStartTime;
        private float astrologicalNextChargeTick;
        private bool equipmentPropertiesLoaded;
        private OfficialHomeRules rules;
        private HomeTutorialCatalog tutorials;
        private OfflineHomeProgression homeProgression;
        private WishRuntimeCatalog tutorialRewardCatalog;
        private OfflineHomeIncomeSession session;
        private HomeAssetCatalog catalog;
        private HomeSceneView scene;
        private HomePlayerActorView homePlayer;
        private HomeInputController input;
        private HomeEnvironmentView environment;
        private HomeMeteorView meteorView;
        private HomeRewardView rewards;
        private HomeHudView hud;
        private IDisposable homeRenderPipeline;
        private TreasureGachaRuntimeCatalog treasureGachaCatalog;
        private OfflineTreasureGachaService treasureGacha;
        private PetGachaRuntimeCatalog petGachaCatalog;
        private OfflinePetGachaService petGacha;
        private OfficialPetGachaAssetCatalog petGachaAssets;
        private OfficialMaterialGatheringCatalog materialGatheringCatalog;
        private OfflineMaterialGatheringService materialGathering;
        private OfflineFruitTreeService fruitTree;
        private WishScreenController wishes;
        private CookingFeatureRuntime cookingRuntime;
        private HomeShopScreenController homeShop;
        private HomeTutorialHost tutorialHost;
        private HomeTutorialRunner tutorialRunner;
        private HomeConditionContext context;
        private Action onClose;
        private Action onOpenMainland;
        private string slotId;
        private string savesRoot;
        private Transform uiParent;
        private float advanceTimer;
        private long lastAdvanceUnix;
        private bool fruitClockRollbackWarned;
        private string pendingFruitClockRollbackMessage;
        private int pendingFruitTreeRepairTutorialType = -1;
        private bool pendingFruitTreeCollectionTutorial;
        private AudioSource fruitTreeTutorialVoiceSource;
        private AudioClip currentFruitTreeTutorialVoice;
        private int fruitTreeTutorialVoiceRequestVersion;
        private HomeSettingsProfile homeSettings;
        private AudioSource settingsClickSource;
        private AudioClip settingsClickClip;
        private bool settingsClickLoading;
        private bool tutorialAutoStartEnabled = true;

        public event Action<int> FurnitureClickedForTutorial;
        public event Action<int> FurnitureRepairedForTutorial;
        public event Action<int, long, string> HangUpCollectedForTutorial;
        public event Action<string> FastForwardAppliedForTutorial;

        public string ActiveProfileSlotId => slotId;
        public OfflineHomeIncomeSession Session => session;
        public OfflineHomeProgression HomeProgression => homeProgression;
        public HomeAssetCatalog Catalog => catalog;
        public CookingFeatureRuntime CookingRuntime => cookingRuntime;
        public HomeHudView Hud => hud;
        public OfficialHomeRules Rules => rules;
        public HomeTutorialCatalog Tutorials => tutorials;
        public HomeSettingsProfile HomeSettings => homeSettings;
        public OfflineMainlineProgressSession Profile => profile;
        public OfflineDailyChallengeService DailyChallenges => dailyChallenges;
        public OfflineNpcTravelService NpcTravel => npcTravel;
        public OfficialNpcTravelCatalog NpcTravelCatalog => npcTravelCatalog;
        public IReadOnlyDictionary<int, long> NpcTravelNpcProperties(int npcClassId)
        {
            EnsureProgression();
            npcTravelProperties ??= new OfflineNpcTravelPropertyCalculator(
                progression.Catalog, npcTravelCatalog);
            return npcTravelProperties.Calculate(profile.Save.Player, npcClassId);
        }
        public IReadOnlyList<int> NpcTravelQuickSelect(int travelId, IEnumerable<int> eligibleNpcIds)
        {
            EnsureProgression();
            npcTravelProperties ??= new OfflineNpcTravelPropertyCalculator(
                progression.Catalog, npcTravelCatalog);
            return npcTravelProperties.QuickSelect(profile.Save.Player, travelId, eligibleNpcIds);
        }
        public OfflineDailyDungeonSession DailyDungeons => dailyDungeons;
        public OfflineFantasyTowerSession FantasyTower => fantasyTower;
        internal OfficialFantasyTowerCatalog FantasyTowerCatalog =>
            fantasyTowerCatalog ?? (fantasyTowerCatalog = OfficialFantasyTowerCatalog.LoadDefault());
        public OfflineNpcAssistanceProgress NpcAssistanceProgress => npcAssistanceProgress;
        public FightOfficialNpcAssistanceCatalog NpcAssistanceCatalog => npcAssistanceCatalog;
        public OfflineArenaSession Arena => arena;
        public OfflineCharacterProgressionService DailyDungeonProgression
        {
            get { EnsureProgression(); return progression; }
        }
        public Transform HomePlayerTransform => homePlayer?.Transform;
        public OfflineAstrologicalService Astrological
        {
            get
            {
                EnsureAstrological();
                return astrological;
            }
        }
        public HomeConditionContext Context => context;
        public OfflineTreasureGachaService TreasureGacha => treasureGacha;
        public TreasureGachaRuntimeCatalog TreasureGachaCatalog => treasureGachaCatalog;
        public OfflinePetGachaService PetGacha => petGacha;
        public PetGachaRuntimeCatalog PetGachaCatalog => petGachaCatalog;
        public OfficialPetGachaAssetCatalog PetGachaAssets => petGachaAssets;
        public int BlessingLevel { get; private set; } = 1;
        public HomeEnvironmentView Environment => environment;
        public HomeSceneView Scene => scene;
        public string SavesRoot => savesRoot;
        public HomeRewardView Rewards => rewards;
        public HomeTutorialRunner TutorialRunner => tutorialRunner;
        public bool TutorialAutoStartEnabled
        {
            get => tutorialAutoStartEnabled;
            set => tutorialAutoStartEnabled = value;
        }
        public bool IsWishScreenOpen => wishes != null && wishes.IsOpen;

        public bool HasFurnitureForSkin(int type)
        {
            if (type == OfflineFruitTreeService.ProductionFurnitureType)
                return profile != null && profile.Save.Player.FruitTree.ProductionRepaired;
            if (type == OfflineFruitTreeService.PetFurnitureType)
                return profile != null && profile.Save.Player.FruitTree.PetFacilityRepaired;
            return session != null && session.OwnsFurniture(type);
        }

        public void RefreshRewards() => rewards?.Refresh();

        public void RefreshCounts() => hud?.RefreshCounts();

        public float CookingMaxHp()
        {
            EnsureProgression();
            if (!equipmentPropertiesLoaded)
                throw new InvalidOperationException("Official equipment properties are required for cooking.");
            CharacterPropCalculator calculator = progression.PropertyCalculator;
            CharacterPropSnapshot snapshot = calculator.Evaluate(profile.Save.Player, Now());
            if (!snapshot.OutOfFightBattleProps.TryGetValue(calculator.Props.Id("MaxHp"), out long maxHp)
                || maxHp <= 0)
                throw new InvalidOperationException("The official character MaxHp property is unavailable.");
            return maxHp;
        }

        public long DailyDungeonCombatRating()
        {
            EnsureProgression();
            return progression.PropertyCalculator.Evaluate(profile.Save.Player, profile.Save.World, Now()).CombatRating;
        }

        public int DailyDungeonRankForSubRank(int subRank)
        {
            EnsureProgression();
            return progression.Catalog.RankOfSubRank(subRank);
        }

        public int DailyDungeonHighSubRank(int rank)
        {
            EnsureProgression();
            return progression.Catalog.Rows("player_subrank")
                .Where(row => Convert.ToInt32(row["Rank"], CultureInfo.InvariantCulture) == rank)
                .Select(row => Convert.ToInt32(row["SubRank"], CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0).Max();
        }

        public bool DailyDungeonProfessionMatches(int profession, int ancestor)
        {
            EnsureProgression();
            if (profession <= 0 || ancestor <= 0) return false;
            int current = profession;
            var visited = new HashSet<int>();
            while (current > 0 && visited.Add(current))
            {
                if (current == ancestor) return true;
                Dictionary<string, object> row = progression.Catalog.Rows("profession_base")
                    .FirstOrDefault(value => Convert.ToInt32(value["Profession"], CultureInfo.InvariantCulture) == current);
                current = row == null ? 0
                    : Convert.ToInt32(row["PrePro"], CultureInfo.InvariantCulture);
            }
            return false;
        }

        public bool ConsumeDailyDungeonItem(string receipt, int itemId, long count)
        {
            if (!ConsumeItems(itemId, count, receipt)) return false;
            hud?.RefreshCounts();
            return true;
        }

        public bool ConsumeArenaCurrency(string receipt, int itemId, long count)
        {
            if (!ConsumeItems(itemId, count, receipt)) return false;
            hud?.RefreshCounts();
            return true;
        }

        public void GrantArenaReward(string receipt, int itemId, long count)
        {
            if (string.IsNullOrWhiteSpace(receipt) || itemId <= 0 || count <= 0)
                throw new ArgumentException("A valid arena reward receipt and amount are required.");
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                    save.Player, itemId, count, Now());
                if (!granted.IsApplied)
                    throw new InvalidOperationException("Arena reward item could not be granted.");
            });
            if (result.Status != ProgressionResultStatus.Applied
                && result.Status != ProgressionResultStatus.Unchanged
                && result.Status != ProgressionResultStatus.Ready)
                throw new InvalidOperationException("Arena reward settlement failed.");
            ReloadContext();
            hud?.RefreshAll();
        }

        public int MapRegionProgress(int mapClassId, int region)
        {
            OfflineMapProgress map = profile?.Save?.World?.Maps?.FirstOrDefault(value =>
                value.MapClassId == mapClassId);
            return map?.Regions?.FirstOrDefault(value => value.Region == region)?.Progress ?? 0;
        }

        public int MaterialGatheringUnlockedLevel(int dungeonType)
        {
            EnsureMaterialGathering();
            if (!materialGatheringCatalog.TryGetDungeon(dungeonType, out MaterialGatheringDungeon dungeon)) return 0;
            int highest = 0;
            for (int index = 0; index < dungeon.UnlockStages.Length; index++)
                if (MaterialGatheringStageCleared(dungeon.UnlockStages[index])) highest = index + 1;
            return highest;
        }

        public MaterialGatheringDungeon MaterialGatheringDungeon(int dungeonType)
        {
            EnsureMaterialGathering();
            return materialGatheringCatalog.TryGetDungeon(dungeonType, out MaterialGatheringDungeon dungeon)
                ? dungeon : null;
        }

        public OfflineMaterialGatheringDungeonState MaterialGatheringState(int dungeonType)
        {
            EnsureMaterialGathering();
            return materialGathering.State.Dungeons.FirstOrDefault(value => value.DungeonType == dungeonType);
        }

        public MaterialGatheringMaterial MaterialGatheringMaterial(int materialId)
        {
            EnsureMaterialGathering();
            return materialGatheringCatalog.TryGetMaterial(materialId, out MaterialGatheringMaterial material)
                ? material : null;
        }

        public bool MaterialGatheringQuickUnlocked(int dungeonType)
        {
            MaterialGatheringDungeon dungeon = MaterialGatheringDungeon(dungeonType);
            return dungeon != null && !rules.IsFunctionLimited(84, dungeon.Id);
        }

        public bool MaterialGatheringQuickEnabled
        {
            get { EnsureMaterialGathering(); return profile.Save.Player.MaterialGathering.QuickEnabled; }
        }

        public bool SetMaterialGatheringQuickEnabled(bool enabled)
        {
            EnsureMaterialGathering();
            if (profile.Save.Player.MaterialGathering.QuickEnabled == enabled) return true;
            ProgressionResult result = profile.ApplyInventoryTransaction(
                "mopup:quick-setting:" + slotId + ":" + Guid.NewGuid().ToString("N"),
                save => save.Player.MaterialGathering.QuickEnabled = enabled);
            return result.Status == ProgressionResultStatus.Applied
                || result.Status == ProgressionResultStatus.Unchanged;
        }

        public MaterialGatheringActionResult GatherMaterial(int dungeonType, bool quick)
        {
            EnsureMaterialGathering();
            EnsureProgression();
            CharacterPropSnapshot snapshot = progression.PropertyCalculator.Evaluate(
                profile.Save.Player, profile.Save.World, Now());
            MaterialGatheringActionResult result = materialGathering.Gather(dungeonType,
                quick ? materialGatheringCatalog.MaximumActionCount : 1,
                quick, MaterialGatheringQuickUnlocked(dungeonType), profile.Save.Player.SubRank,
                snapshot.NonBattleProps,
                "mopup:gather:" + slotId + ":" + Guid.NewGuid().ToString("N"));
            if (result.IsApplied)
            {
                ReloadContext();
                hud?.RefreshAll();
            }
            return result;
        }

        private bool MaterialGatheringStageCleared(int stageId)
        {
            return profile != null && profile.StageWinCount(stageId) > 0;
        }

        private void EnsureMaterialGathering()
        {
            materialGatheringCatalog ??= OfficialMaterialGatheringCatalog.LoadDefault();
            if (materialGathering == null)
            {
                EnsureProgression();
                materialGathering = new OfflineMaterialGatheringService(profile, materialGatheringCatalog,
                    progression.PropertyCalculator.Props);
            }
        }

        public bool GrantDailyDungeonRewards(string receipt, Zjcs.Offline.World.DailyDungeonRunSave run,
            int[] rewardRuleIds)
        {
            if (string.IsNullOrWhiteSpace(receipt) || run == null || rewardRuleIds == null) return false;
            EnsureProgression();
            var random = new DailyDungeonRewardRandom(receipt);
            var itemCounts = new Dictionary<int, long>();
            int profession = profile.Save.Player.Wardrobe.Profession;
            long combatRating = DailyDungeonCombatRating();
            foreach (int ruleId in rewardRuleIds)
            {
                Zjcs.Offline.World.DailyDungeonRuntimeReward rule = dailyDungeons.Catalog.rewardRules
                    .Where(value => value.Id == ruleId)
                    .OrderBy(value => value.Order < 0 ? int.MaxValue : value.Order)
                    .FirstOrDefault(value => DailyDungeonRewardConditionMet(value, profession, combatRating));
                if (rule == null || rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                    return false;
                long weightTotal = rule.WeightList.Where(value => value > 0).Sum(value => (long)value);
                if (weightTotal <= 0 || rule.Percent <= 0) continue;
                for (int index = 0; index < rule.IdList.Length; index++)
                {
                    int itemId = rule.IdList[index];
                    int weight = rule.WeightList[index];
                    float count = rule.CountList[index];
                    if (itemId <= 0 || weight <= 0 || count <= 0) continue;
                    if (rule.LevelCountId > 0)
                        count *= dailyDungeons.LevelNumber(rule.LevelCountId, profile.Save.Player.Level);
                    if (rule.SubRankCountId != null && index < rule.SubRankCountId.Length
                        && rule.SubRankCountId[index] > 0)
                        count *= dailyDungeons.LevelNumber(rule.SubRankCountId[index], profile.Save.Player.SubRank);
                    long amount = Math.Max(0L, (long)count);
                    double probability = rule.Percent * weight / weightTotal;
                    if (probability < 1d && !random.Roll(probability)) continue;
                    if (probability <= 0d || amount <= 0) continue;
                    itemCounts[itemId] = itemCounts.TryGetValue(itemId, out long current)
                        ? checked(current + amount) : amount;
                }
            }
            if (itemCounts.Count == 0) return true;
            OfficialEquipmentCatalog equipment = progression.EquipmentCatalog;
            var dropRange = dailyDungeons.FindMap(run.mapGroupId)?.equipmentDrops?
                .FirstOrDefault(value => value.difficulty == run.difficulty);
            long now = Now();
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (KeyValuePair<int, long> reward in itemCounts.OrderBy(value => value.Key))
                {
                    int type = progression.Catalog.ItemType(reward.Key);
                    bool randomEquipment = type == 110;
                    bool isEquipment = equipment?.Find("equip", reward.Key) != null;
                    Zjcs.Offline.World.DailyDungeonRuntimeEquipmentPool pool = randomEquipment
                        ? dailyDungeons.Catalog.randomEquipmentPools?.FirstOrDefault(value => value.id == reward.Key)
                        : null;
                    if (randomEquipment && (pool?.equipIds == null || pool.weights == null
                        || pool.equipIds.Length != pool.weights.Length || !pool.weights.Any(value => value > 0)))
                        throw new InvalidOperationException("The official daily dungeon random equipment pool is unavailable: " + reward.Key);
                    if (randomEquipment || isEquipment)
                    {
                        if (equipment == null || dropRange == null)
                            throw new InvalidOperationException("Official daily dungeon equipment drop range is unavailable.");
                        long weightTotal = randomEquipment ? pool.weights.Where(value => value > 0).Sum(value => (long)value) : 0;
                        for (long index = 0; index < reward.Value; index++)
                        {
                            int equipId = reward.Key;
                            if (randomEquipment)
                            {
                                long pick = (long)(random.NextDouble() * weightTotal);
                                int selected = -1;
                                for (int poolIndex = 0; poolIndex < pool.weights.Length; poolIndex++)
                                {
                                    if (pool.weights[poolIndex] <= 0) continue;
                                    pick -= pool.weights[poolIndex];
                                    if (pick < 0) { selected = poolIndex; break; }
                                }
                                if (selected < 0) throw new InvalidOperationException("Daily dungeon equipment selection failed.");
                                equipId = pool.equipIds[selected];
                            }
                            long instanceId = save.Player.Equipment.NextInstanceId;
                            OfflineEquipmentInstance item = OfficialEquipmentGenerator.Generate(equipment,
                                equipId, instanceId, new EquipmentRollContext
                                {
                                    DropLevel = Math.Max(1, save.Player.Level),
                                    PlayerLevel = save.Player.Level,
                                    SubRank = save.Player.SubRank,
                                    SourceOperate = 61,
                                    Daily = true,
                                    MinimumLevel = dropRange.minimumLevel,
                                    MaximumLevel = dropRange.maximumLevel,
                                    MinimumBlessLevel = dropRange.minimumBlessLevel,
                                    MaximumBlessLevel = dropRange.maximumBlessLevel
                                }, random.NextFloat);
                            save.Player.Equipment.Instances.Add(item);
                            save.Player.Equipment.NextInstanceId++;
                        }
                    }
                    else
                    {
                        InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                            reward.Key, reward.Value, now);
                        if (!granted.IsApplied)
                            throw new InvalidOperationException("Unsupported daily dungeon reward item: " + reward.Key);
                    }
                }
            });
            bool applied = result.Status == ProgressionResultStatus.Applied
                || result.Status == ProgressionResultStatus.Unchanged || result.Status == ProgressionResultStatus.Ready;
            if (applied)
            {
                ReloadContext();
                hud?.RefreshAll();
            }
            return applied;
        }

        public List<FantasyTowerReward> RollFantasyTowerRewards(int stageId, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt)) throw new ArgumentException("A stable reward receipt is required.", nameof(receipt));
            ulong state = 14695981039346656037UL;
            foreach (char value in receipt) state = unchecked((state ^ value) * 1099511628211UL);
            if (state == 0) state = 0x9e3779b97f4a7c15UL;
            return FantasyTowerCatalog.RollRewards(stageId, profile.Save.Player.Level,
                profile.Save.Player.SubRank, ref state);
        }

        public bool GrantFantasyTowerRewards(string receipt, IReadOnlyList<FantasyTowerReward> rewards)
        {
            if (string.IsNullOrWhiteSpace(receipt) || rewards == null) return false;
            long now = Now();
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (FantasyTowerReward reward in rewards)
                {
                    if (reward == null || reward.itemId <= 0 || reward.count < 0)
                        throw new InvalidDataException("Official Fantasy Tower reward is invalid.");
                    if (reward.count == 0) continue;
                    InventoryOperationResult applied = OfflineInventoryOperations.Grant(
                        save.Player, reward.itemId, reward.count, now);
                    if (!applied.IsApplied)
                        throw new InvalidOperationException("Fantasy Tower reward item could not be granted: "
                            + reward.itemId + " " + applied.Status);
                }
            });
            return result.Status == ProgressionResultStatus.Applied
                || result.Status == ProgressionResultStatus.Unchanged || result.Status == ProgressionResultStatus.Ready;
        }

        public FantasyTowerBattleRunner StartFantasyTower(int entryIndex)
        {
            if (entryIndex < 1 || entryIndex > 4) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            if (GetComponent<FantasyTowerBattleRunner>() != null)
                throw new InvalidOperationException("A Fantasy Tower battle is already active.");
            FantasyTowerBattleRunner runner = gameObject.AddComponent<FantasyTowerBattleRunner>();
            try { runner.Begin(this, entryIndex); }
            catch { Destroy(runner); throw; }
            return runner;
        }

        public void SetFantasyTowerBackgroundExecution(bool enabled)
        {
            FantasyTowerBattleRunner runner = GetComponent<FantasyTowerBattleRunner>();
            if (runner != null && runner.IsAutoPushEnabled)
                runner.SetBackgroundExecution(enabled);
        }

        public FantasyTowerBattleRunner StartFantasyTowerAfterCountdown(int entryIndex)
        {
            if (entryIndex < 1 || entryIndex > 4) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            if (!IsFantasyTowerUnlocked())
                throw new InvalidOperationException("Official Fantasy Tower progression unlock is not met.");
            if (GetComponent<FantasyTowerBattleRunner>() != null)
                throw new InvalidOperationException("A Fantasy Tower battle is already active.");
            FantasyTowerBattleRunner runner = gameObject.AddComponent<FantasyTowerBattleRunner>();
            try { runner.BeginAutoNext(this, entryIndex); }
            catch { Destroy(runner); throw; }
            return runner;
        }

        public bool IsFantasyTowerUnlocked()
        {
            return Enumerable.Range(1, 4).Any(entry => fantasyTower.GetEntry(entry).clearedLayer >= 3);
        }

        public void RefreshAfterFantasyTowerReward()
        {
            ReloadContext();
            hud?.RefreshAll();
        }

        public FantasyTowerResult ClaimFantasyTowerResult(string resultId)
        {
            FantasyTowerResult result = fantasyTower.ClaimResult(resultId, profile);
            if (result == FantasyTowerResult.Applied) RefreshAfterFantasyTowerReward();
            return result;
        }

        private bool DailyDungeonRewardConditionMet(Zjcs.Offline.World.DailyDungeonRuntimeReward reward,
            int profession, long combatRating)
        {
            if (string.IsNullOrEmpty(reward.ConditionType)) return true;
            if (reward.ConditionType == "PlayerProfessionCondition")
                return profession > 0 && profession == reward.ConditionProfession;
            if (reward.ConditionType == "PlayerProfessionFactionCondition")
                return DailyDungeonProfessionMatches(profession, reward.ConditionProfession);
            if (reward.ConditionType == "CompositeAndCondition" || reward.ConditionType == "CompositeOrCondition")
            {
                int[] ids = reward.ConditionIds ?? Array.Empty<int>();
                if (ids.Length == 0) return false;
                bool Match(int id) => dailyDungeons.IsConditionMet(id, profile.Save.Player.Level,
                    combatRating, profile.Save.Player.SubRank, (_, __) => false,
                    DailyDungeonHighSubRank, DailyDungeonRankForSubRank, profession,
                    DailyDungeonProfessionMatches, profile.InventoryCount);
                return reward.ConditionType == "CompositeAndCondition"
                    ? ids.All(Match) : ids.Any(Match);
            }
            return false;
        }

        private sealed class DailyDungeonRewardRandom
        {
            private uint state;

            public DailyDungeonRewardRandom(string seed)
            {
                state = 2166136261;
                foreach (char value in seed) state = unchecked((state ^ value) * 16777619);
                if (state == 0) state = 0x9e3779b9;
            }

            public double NextDouble()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0x00ffffff) / 16777216d;
            }

            public float NextFloat() => (float)NextDouble();
            public bool Roll(double probability) => NextDouble() < Math.Min(1d, probability);
        }

        public TreasureGachaSourceNavigation OpenTreasureGachaFromItemSource(int sourceItemId,
            int elementGachaType = 0, Action<int> returnToSource = null)
        {
            if (treasureGacha == null || hud == null || elementGachaType != 0
                && (elementGachaType < 8 || elementGachaType > 12)) return null;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            TreasureGachaSourceNavigation navigation = treasureGacha.ResolveItemSource(sourceItemId, now);
            if (navigation == null) return null;
            if (navigation.SelectedSeriesId > 0
                && treasureGacha.State.SelectedSeriesId != navigation.SelectedSeriesId)
            {
                TreasureGachaResult selected = treasureGacha.SelectSourceSeries(navigation.SelectedSeriesId,
                    now, "treasure-gacha:source:" + Guid.NewGuid().ToString("N"));
                if (!selected.IsApplied && selected.Code != TreasureGachaResultCode.Unchanged) return null;
            }
            if (elementGachaType >= 8)
            {
                TreasureGachaResult selected = treasureGacha.SelectElementType(elementGachaType,
                    "treasure-gacha:source-element:" + Guid.NewGuid().ToString("N"));
                if (!selected.IsApplied && selected.Code != TreasureGachaResultCode.Unchanged) return null;
            }
            hud.ShowTreasureGachaFromItemSource(sourceItemId, navigation.SelectedSeriesId,
                elementGachaType, returnToSource);
            return navigation;
        }

        public string CharacterName => profile?.Save.Player.CharacterName ?? string.Empty;
        public int Level => profile?.Save.Player.Level ?? 1;
        public int SubRank => profile?.Save.Player.SubRank ?? 1;
        public int Rank => profile?.Save.Player.Rank ?? 1;
        public string GuildName => profile?.Save.Player.GuildName ?? string.Empty;
        public int StoryCount(string name) => profile == null ? 0 : profile.StoryCount(name);
        public int StoryStatus(string name) => profile == null ? 0 : profile.StoryStatus(name);
        public bool FeatureModalOpen => cookingRuntime != null && cookingRuntime.gameObject.activeInHierarchy
            || homeShop != null && homeShop.IsOpen;

        public static HomeFeatureRuntime Begin(string selectedSlotId, string savesRoot, Transform uiParent,
            Action onClose = null, Action onOpenMainland = null)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("An existing profile slot is required.", nameof(selectedSlotId));
            var go = new GameObject("HomeGameRuntime");
            HomeFeatureRuntime runtime = go.AddComponent<HomeFeatureRuntime>();
            try {
                runtime.homeRenderPipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets")
                    .Enter();
                runtime.Open(selectedSlotId, string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot,
                    uiParent, onClose, onOpenMainland);
            } catch { Destroy(go); throw; }
            return runtime;
        }

        private void Open(string selected, string root, Transform uiParent, Action close, Action openMainland)
        {
            slotId = selected;
            savesRoot = root;
            this.uiParent = uiParent;
            onClose = close;
            onOpenMainland = openMainland;
            slots = new ProfileSlotCatalog(root);
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile does not exist.");
            homeSettings = new HomeSettingsProfile(Path.Combine(slots.GetSlotDirectory(slotId), "settings.json"));
            homeSettings.Load();
            string fantasyTowerSavePath = Path.Combine(slots.GetSlotDirectory(slotId), "fantasy-tower-save.json");
            profile = OfflineMainlineProgressSession.Open(slots, slotId,
                OfficialMainlineTaskCatalog.LoadDefault(), entryIndex =>
                    OfflineFantasyTowerSession.ReadClearedLayer(fantasyTowerSavePath, entryIndex));
            profile.Reload();
            dailyChallenges = new OfflineDailyChallengeService(
                Path.Combine(slots.GetSlotDirectory(slotId), "daily-challenges.json"));
            dailyDungeons = OfflineDailyDungeonSession.Open(slots, slotId);
            fantasyTower = OfflineFantasyTowerSession.Open(slots, slotId);
            npcAssistanceProgress = OfflineNpcAssistanceProgress.Open(slots, slotId);
            npcAssistanceCatalog = OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            arena = OfflineArenaSession.Open(slots, slotId);
            npcTravelCatalog = OfficialNpcTravelCatalog.LoadDefault();
            npcTravel = new OfflineNpcTravelService(profile, npcTravelCatalog);
            fruitTree = new OfflineFruitTreeService(profile);
            catalog = HomeAssetCatalog.LoadDefault();
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "TreasureGacha", "ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "PetGacha", "ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "PetGacha", "performance-ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "Home", "Settings", "settings-ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "Home", "Travel", "ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "TowerPush", "ui.json"));
            try
            {
                treasureGachaCatalog = OfficialTreasureGachaAssets.LoadDefault();
                treasureGacha = new OfflineTreasureGachaService(profile, treasureGachaCatalog);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home treasure gacha unavailable: " + error.Message);
            }
            try
            {
                petGachaCatalog = OfficialPetGachaAssets.LoadDefault();
                petGachaAssets = Resources.Load<OfficialPetGachaAssetCatalog>(
                    "PetGacha/official-pet-gacha-assets");
                if (petGachaAssets == null)
                    throw new InvalidOperationException("Official pet gacha native models are not installed.");
                petGachaAssets.Validate(petGachaCatalog);
                petGacha = new OfflinePetGachaService(profile, petGachaCatalog);
            }
            catch (Exception error)
            {
                petGacha = null;
                Debug.LogWarning("Home pet gacha unavailable: " + error.Message);
            }
            rules = OfficialHomeRules.LoadDefault();
            tutorials = HomeTutorialCatalog.LoadDefault();
            homeProgression = new OfflineHomeProgression(rules);
            BlessingLevel = 1;
            ReloadContext();
            long openedAt = Now();
            fruitTree.TryAutoRepairProduction(profile.Save.Player.Level, context.AnyGoddessActive,
                openedAt, "home:fruit-tree:auto-repair:" + slotId);
            AdvanceFruitTree(openedAt);
            session = OpenSession();
            lastAdvanceUnix = openedAt;
            scene = new HomeSceneView(catalog, transform);
            BuildHomePlayer();
            environment = new HomeEnvironmentView(scene, Now(), profile.Save.CreatedAtUnixSeconds);
            meteorView = new HomeMeteorView(scene, environment.MeteorRuntime);
            environment.MeteorTriggered += meteorView.Play;
            rewards = new HomeRewardView(this, catalog, transform);
            input = new HomeInputController(scene, () => hud != null && hud.AnyModalOpen
                || wishes != null && wishes.IsOpen);
            input.FurnitureClicked += OnFurnitureClicked;
            input.RewardClicked += OnRewardClicked;
            input.SetRewardHit(OnRewardHit);
            hud = new HomeHudView(this, uiParent);
            hud.Build();
            hud.ShowFantasyTowerPendingResult();
            fruitTreeTutorialVoiceSource = gameObject.AddComponent<AudioSource>();
            fruitTreeTutorialVoiceSource.playOnAwake = false;
            fruitTreeTutorialVoiceSource.loop = false;
            fruitTreeTutorialVoiceSource.spatialBlend = 0f;
            HomeAudioSettings.Register(fruitTreeTutorialVoiceSource, HomeAudioChannel.Voice);
            if (!string.IsNullOrEmpty(pendingFruitClockRollbackMessage))
            {
                hud.ShowNotice(pendingFruitClockRollbackMessage);
                pendingFruitClockRollbackMessage = null;
            }
            ApplyFurnitureStates();
            scene.ApplyHomeSkins(catalog.HomeSkins, session.Skins, rules);
            RefreshIncomeCatalog();
            DrainHomeInventory();
            hud.RefreshAll();
            tutorialHost = new HomeTutorialHost(this, hud);
            tutorialRunner = new HomeTutorialRunner(tutorials, tutorialHost,
                Path.Combine(slots.GetSlotDirectory(slotId), "home-tutorials.json"));
            tutorialHost.RecordOptionSelection = tutorialRunner.RecordOptionSelection;
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public string FormatTopBarCurrency(long count)
        {
            return homeSettings == null
                ? count.ToString(CultureInfo.InvariantCulture)
                : homeSettings.FormatNumber(count);
        }

        public void PlaySettingsClick()
        {
            if (settingsClickSource == null)
            {
                settingsClickSource = gameObject.AddComponent<AudioSource>();
                settingsClickSource.playOnAwake = false;
                settingsClickSource.loop = false;
                settingsClickSource.spatialBlend = 0f;
                HomeAudioSettings.Register(settingsClickSource, HomeAudioChannel.Sound);
            }
            if (settingsClickClip != null)
            {
                settingsClickSource.PlayOneShot(settingsClickClip);
                return;
            }
            if (!settingsClickLoading) StartCoroutine(LoadSettingsClick());
        }

        private IEnumerator LoadSettingsClick()
        {
            settingsClickLoading = true;
            string path = Path.Combine(Application.streamingAssetsPath, "Home", "Settings", "click.wav");
            if (!File.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue))
            {
                settingsClickLoading = false;
                Debug.LogWarning("Official Settings click audio is unavailable.");
                yield break;
            }
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri,
                AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    settingsClickLoading = false;
                    Debug.LogWarning("Official Settings click audio could not be loaded: " + request.error);
                    yield break;
                }
                settingsClickClip = DownloadHandlerAudioClip.GetContent(request);
            }
            settingsClickLoading = false;
            if (settingsClickClip != null && settingsClickSource != null)
                settingsClickSource.PlayOneShot(settingsClickClip);
        }

        public void PlayFruitTreeTutorialVoice(string eventKey)
        {
            StopFruitTreeTutorialVoice();
            string relativePath = HomeFruitTreeTutorialCatalog.VoiceFile(eventKey);
            StartCoroutine(LoadFruitTreeTutorialVoice(relativePath, eventKey,
                fruitTreeTutorialVoiceRequestVersion));
        }

        public void StopFruitTreeTutorialVoice()
        {
            fruitTreeTutorialVoiceRequestVersion++;
            if (fruitTreeTutorialVoiceSource != null)
            {
                fruitTreeTutorialVoiceSource.Stop();
                fruitTreeTutorialVoiceSource.clip = null;
            }
            if (currentFruitTreeTutorialVoice != null) Destroy(currentFruitTreeTutorialVoice);
            currentFruitTreeTutorialVoice = null;
        }

        private IEnumerator LoadFruitTreeTutorialVoice(string relativePath, string eventKey, int version)
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue))
            {
                Debug.LogWarning("Official fruit tree tutorial voice path is unavailable: " + eventKey);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri,
                AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (version != fruitTreeTutorialVoiceRequestVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Official fruit tree tutorial voice could not be loaded for "
                        + eventKey + ": " + request.error);
                    yield break;
                }

                currentFruitTreeTutorialVoice = DownloadHandlerAudioClip.GetContent(request);
                if (currentFruitTreeTutorialVoice == null)
                {
                    Debug.LogWarning("Official fruit tree tutorial voice decoded to an empty clip: " + eventKey);
                    yield break;
                }
                fruitTreeTutorialVoiceSource.clip = currentFruitTreeTutorialVoice;
                fruitTreeTutorialVoiceSource.Play();
            }

            while (version == fruitTreeTutorialVoiceRequestVersion
                && fruitTreeTutorialVoiceSource != null && fruitTreeTutorialVoiceSource.isPlaying)
                yield return null;
            if (version == fruitTreeTutorialVoiceRequestVersion) StopFruitTreeTutorialVoice();
        }

        private void ReloadContext()
        {
            OfflinePlayerProgress player = profile.Save.Player;
            EnsureProgression();
            BlessingLevel = new OfflineGoddessPropSource(progression.Catalog).BlessingLevel(profile.Save.World);
            context = new HomeConditionContext
            {
                Level = player.Level,
                SubRank = player.SubRank,
                Rank = player.Rank,
                MaxProfessionRank = player.MaxProfessionRank,
                InitialWeaponType = player.InitialWeaponType,
                Profession = player.Wardrobe.Profession,
                ItemCount = profile.InventoryCount,
                StoryCount = name => StoryCount(name),
                StoryStatus = name => StoryStatus(name),
                HasItemOfType = itemType => player.Inventory.Any(stack => stack != null && stack.Count > 0
                    && rules.Item(stack.ItemId)?.type == itemType),
                AnyGoddessActive = profile.Save.World.ActivatedGoddessRegions != null
                    && profile.Save.World.ActivatedGoddessRegions.Count > 0,
                HasManualMeetNpc = player.HasManualMeetNpc,
                UnlockedMapClassIds = new HashSet<int>(player.UnlockedMapClassIds ?? new List<int>())
            };
            rules.CurrentContext = context;
        }

        private OfflineHomeIncomeSession OpenSession()
        {
            var properties = PlayerProperties();
            session = OfflineHomeIncomeSession.Open(slots, slotId, new[] { PlaceholderDefinition() },
                properties, FloatPropertyIds(), Array.Empty<OfflineHomeRandomRewardDefinition>(), Now());
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(
                session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, properties, FloatPropertyIds(), rewards);
            return session;
        }

        private static OfflineHomeIncomeDefinition PlaceholderDefinition()
        {
            return new OfflineHomeIncomeDefinition
            {
                HangUpId = 1,
                OutputItemId = 50,
                Rule = new HangUpIncomeRule { HourlyOutput = 0, LevelNum = 1, MaxStoreHours = 36 }
            };
        }

        public IReadOnlyDictionary<int, long> PlayerProperties()
        {
            var merged = new Dictionary<int, long>();
            EnsureProgression();
            if (!equipmentPropertiesLoaded)
                throw new InvalidOperationException("Official equipment properties are required for home stats.");
            CharacterPropSnapshot snapshot = progression.PropertyCalculator.Evaluate(
                profile.Save.Player, profile.Save.World, Now());
            foreach (KeyValuePair<int, long> pair in snapshot.OutOfFightBattleProps)
                merged[pair.Key] = pair.Value;
            if (!string.IsNullOrEmpty(profile.Save.Player.CommerceStateJson))
            {
                OfflineCommerceSave commerce = JsonUtility.FromJson<OfflineCommerceSave>(
                    profile.Save.Player.CommerceStateJson);
                foreach (KeyValuePair<int, long> pair in OfflineCommerceSession.ActivePeriodicProperties(
                             OfficialCommerceCatalogView.LoadDefault(), commerce?.State))
                    merged[pair.Key] = merged.TryGetValue(pair.Key, out long current)
                        ? checked(current + pair.Value) : pair.Value;
            }
            return merged;
        }

        private static ISet<int> FloatPropertyIds()
        {
            return new HashSet<int> { 6004, 1034 };
        }

        private void EnsureProgression()
        {
            if (progression != null) return;
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            OfficialCharacterCatalog character = characterAssets.Load();
            progression = new OfflineCharacterProgressionService(profile, character);
            try
            {
                progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
                progression.AttachSkillRules(characterAssets.LoadSkillRules());
                equipmentPropertiesLoaded = true;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Official equipment properties are required for home stats.", error);
            }
        }

        private void BuildHomePlayer()
        {
            EnsureProgression();
            CharacterCreationAssetCatalog creationAssets = CharacterCreationAssetCatalog.LoadDefault();
            var creationService = new CharacterCreationOfflineService(slots, creationAssets,
                OfficialCharacterAssetCatalog.LoadDefault());
            homePlayer = new HomePlayerActorView(scene,
                Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName, "player-runtime.json"),
                profile.Save.Player.Wardrobe, progression, creationAssets, creationService);
        }

        public void RefreshIncomeCatalog()
        {
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, PlayerProperties(), FloatPropertyIds(), rewards);
        }

        private void ApplyFurnitureStates()
        {
            foreach (HomeFurnitureRecord record in rules.Furniture)
            {
                HomeFurnitureState state = FurnitureState(record.type);
                scene.SetFurnitureState(record.type, state);
            }
            scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                profile.Save.Player.FruitTree.StoredCount);
        }

        private HomeFurnitureState FurnitureState(int type)
        {
            if (type == OfflineFruitTreeService.ProductionFurnitureType
                && profile.Save.Player.FruitTree.ProductionRepaired) return HomeFurnitureState.Functional;
            if (type == OfflineFruitTreeService.PetFurnitureType
                && profile.Save.Player.FruitTree.PetFacilityRepaired) return HomeFurnitureState.Functional;
            if (type != OfflineFruitTreeService.ProductionFurnitureType
                && type != OfflineFruitTreeService.PetFurnitureType)
                return homeProgression.FurnitureState(session, type, context);
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return HomeFurnitureState.Locked;
            HomeFurnitureState state = rules.IsMet(record.unlock, context)
                ? HomeFurnitureState.CanRepair
                : !rules.IsMet(record.repairNotice, context)
                    ? HomeFurnitureState.Locked : HomeFurnitureState.RepairNotice;
            if (type == OfflineFruitTreeService.ProductionFurnitureType)
            {
                HomeFurnitureRecord pet = rules.FurnitureOf(OfflineFruitTreeService.PetFurnitureType);
                HomeFurnitureState petState = pet == null || !rules.IsMet(pet.repairNotice, context)
                    ? HomeFurnitureState.Locked
                    : rules.IsMet(pet.unlock, context)
                        ? HomeFurnitureState.CanRepair : HomeFurnitureState.RepairNotice;
                if (petState == HomeFurnitureState.CanRepair || petState == HomeFurnitureState.RepairNotice)
                    return HomeFurnitureState.Locked;
            }
            return state;
        }

        public bool CanRepairFurnitureForTutorial(int type)
        {
            HomeFurnitureRecord record = rules?.FurnitureOf(type);
            return record != null && session != null && !session.OwnsFurniture(type)
                && rules.IsMet(record.unlock, context)
                && (record.repairCost ?? Array.Empty<HomeCostEntry>())
                    .Where(cost => cost != null && cost.itemId > 0 && cost.count > 0)
                    .All(cost => profile.InventoryCount(cost.itemId) >= cost.count);
        }

        public bool CanUnlockAnyHangUpForFurniture(int furnitureType) => rules != null && session != null
            && rules.HangUpsOf(furnitureType).Any(record => record != null
                && !session.IsHangUpUnlocked(record.id) && rules.IsMet(record.unlock, context));

        public void SetTutorialCameraDragEnabled(bool enabled)
        {
            if (input != null) input.CameraDragEnabled = enabled;
        }

        public bool TutorialFastForwardReceiptApplied(string nodeReceipt) =>
            !string.IsNullOrEmpty(nodeReceipt) && session != null
            && session.Save.AppliedReceipts.Any(value => value != null
                && value.StartsWith(nodeReceipt + ":fast:", StringComparison.Ordinal));

        public bool TutorialCanGetFixedReward(int fixedRewardId)
        {
            WishFixedReward reward = TutorialFixedReward(fixedRewardId);
            return profile != null && reward != null && reward.CanGetTimes > 0
                && !profile.Save.Player.AppliedInventoryEventIds.Contains(HomeTutorialFixedRewardReceipt);
        }

        public bool TryClaimTutorialFixedReward(int fixedRewardId, string nodeReceipt, out string reason)
        {
            reason = null;
            WishFixedReward fixedReward = TutorialFixedReward(fixedRewardId);
            if (fixedReward == null)
            {
                reason = "No official guide fixed-reward rule is installed for reward " + fixedRewardId + ".";
                return false;
            }
            if (string.IsNullOrWhiteSpace(nodeReceipt))
            {
                reason = "The fixed-reward tutorial node has no durable execution receipt.";
                return false;
            }
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(HomeTutorialFixedRewardReceipt)) return true;

            WishAmount[] rewards = fixedReward.Reward ?? Array.Empty<WishAmount>();
            if (rewards.Length == 0)
            {
                reason = "Official fixed reward " + fixedRewardId + " has no reward items.";
                return false;
            }
            foreach (WishAmount reward in rewards)
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0 || rules.Item(reward.ItemId) == null)
                {
                    reason = "Official fixed reward " + fixedRewardId
                        + " references an invalid or missing local item " + (reward == null ? 0 : reward.ItemId) + ".";
                    return false;
                }

            ProgressionResult result = profile.ApplyInventoryTransaction(HomeTutorialFixedRewardReceipt, save =>
            {
                foreach (WishAmount reward in rewards)
                {
                    InventoryOperationResult grant = OfflineInventoryOperations.Grant(save.Player,
                        reward.ItemId, reward.Count, Now());
                    if (grant.Status != InventoryOperationStatus.Applied)
                        throw new InvalidOperationException("Official fixed reward item grant failed: " + reward.ItemId);
                }
            });
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
            {
                reason = "Official fixed reward could not be committed locally: " + result.Message;
                return false;
            }
            ReloadContext();
            hud?.RefreshAll();
            hud?.ShowTutorialFixedReward(rewards);
            return true;
        }

        private WishFixedReward TutorialFixedReward(int fixedRewardId)
        {
            if (tutorialRewardCatalog == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Wishes/wishes-runtime");
                if (source == null) throw new InvalidOperationException("The pinned official wish rules are missing.");
                tutorialRewardCatalog = WishRuntimeCatalog.Parse(source.text);
            }
            if (fixedRewardId != HomeTutorialFixedRewardId) return null;
            return tutorialRewardCatalog.FixedReward(fixedRewardId);
        }

        private int OnRewardHit(Vector2 world) => rewards == null ? -1 : rewards.HitTest(world);

        private void OnRewardClicked(int slot)
        {
            long gained = TryCollectRandomReward(slot);
            if (gained <= 0) hud?.ShowNotice("该奖励暂时无法领取");
        }

        public long TryCollectRandomReward(int slot)
        {
            string receipt = "home:reward:" + slotId + ":" + slot + ":" + Guid.NewGuid().ToString("N");
            bool collected = session.CollectRandomReward(slot, receipt);
            if (!collected) return 0;
            DrainHomeInventory();
            rewards?.Refresh();
            hud?.RefreshAll();
            return 1;
        }
        private void OnFurnitureClicked(int type)
        {
            FurnitureClickedForTutorial?.Invoke(type);
            if (type == 6)
            {
                if (rules.IsFunctionLimited(OfficialMailFunctionType))
                    hud.ShowNotice("邮箱暂不可用");
                else hud.ShowMail();
                return;
            }
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return;
            HomeFurnitureState state = FurnitureState(type);
            switch (state)
            {
                case HomeFurnitureState.Functional:
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        || type == OfflineFruitTreeService.PetFurnitureType)
                        OnFruitTreeClicked();
                    else if (!hud.IsClosedByNetworkPolicy(type)) OpenFurniture(type);
                    break;
                case HomeFurnitureState.CanRepair:
                {
                    if (pendingFruitTreeRepairTutorialType == type)
                    {
                        pendingFruitTreeRepairTutorialType = -1;
                        hud.HideFruitTreeTutorialMask();
                        hud.ShowRepair(type);
                        hud.ShowFruitTreeRepairButtonMask();
                        break;
                    }
                    OfflineFruitTreeProgress tutorialState = profile.Save.Player.FruitTree;
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        && !tutorialState.ProductionRepairStorySeen)
                        hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 0, () =>
                        {
                            pendingFruitTreeRepairTutorialType = type;
                            hud.ShowFruitTreeFurnitureMask(type);
                        });
                    else if (type == OfflineFruitTreeService.PetFurnitureType
                        && !tutorialState.PetRepairStorySeen)
                        hud.ShowFruitTreeTutorial("FruitTreeUnlock2", 0, () =>
                        {
                            pendingFruitTreeRepairTutorialType = type;
                            hud.ShowFruitTreeFurnitureMask(type);
                        });
                    else hud.ShowRepair(type);
                    break;
                }
                case HomeFurnitureState.RepairNotice:
                    hud.ShowNotice(rules.IsMet(record.repairNotice, context)
                        ? HomeNoticeText(type)
                        : "尚未满足解锁条件");
                    break;
                case HomeFurnitureState.Locked:
                    OfflineFruitTreeProgress fruitState = profile.Save.Player.FruitTree;
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        && !fruitState.ProductionRepaired && !fruitState.PetFacilityRepaired)
                        hud.ShowNotice(catalog.LocalizedText("HomeFruitTree.InitialStateTip", "该果树设施尚未开放"));
                    break;
            }
        }

        private string HomeNoticeText(int type)
        {
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return string.Empty;
            return "满足条件后可维修 " + rules.FurnitureNameForType(type);
        }

        private void OpenFurniture(int type)
        {
            if (type == 0) hud.ShowBed();
            else if (type == 10) hud.ShowCart();
            else if (type == 6) hud.ShowMail();
            else if (type == 7) hud.ShowStorage();
            else if (type == 9) hud.ShowTreasureGacha();
            else if (type == OfflineFruitTreeService.PetFurnitureType) hud.ShowPetGacha();
            else if (type == 5) OpenWishes();
            else if (type == 3) OpenCooking();
            else if (type == 8) OpenHomeShop();
            else if (type == 12)
            {
                OfflineAstrologicalService service = Astrological;
                int rank = profile.Save.Player.Rank;
                if (service.SeasonForRank(rank) < service.MinimumSeason)
                    hud.ShowNotice("当前位阶尚未开放命运星象");
                else hud.ShowAstrologicalDestiny();
            }
            else hud.ShowFurniturePlaceholder(type);
        }

        public bool BeginAstrologicalCharge()
        {
            EnsureAstrological();
            float now = Time.time;
            if (!astrological.BeginCharge(now))
            {
                hud?.ShowNotice(astrological.CurrentCost >= astrological.MaximumCost
                    ? "星命契约已达上限" : "原初之星不足");
                return false;
            }
            astrologicalChargeStartTime = now;
            astrologicalNextChargeTick = now;
            hud?.RefreshAstrologicalDestiny();
            return true;
        }

        public void EndAstrologicalCharge()
        {
            if (astrological == null || !astrological.IsCharging) return;
            ProgressionResult result = astrological.EndCharge("home:astrological:" + slotId + ":"
                + Guid.NewGuid().ToString("N"));
            if (result.Status == ProgressionResultStatus.InvalidState)
                hud?.ShowNotice(result.Message ?? "星命契约灌注失败");
            else if (result.IsApplied)
                hud?.RefreshAll();
            hud?.RefreshAstrologicalDestiny();
        }

        private void EnsureAstrological()
        {
            EnsureProgression();
            astrological ??= new OfflineAstrologicalService(profile, progression.Catalog);
        }

        private void TickAstrologicalCharge()
        {
            if (astrological == null || !astrological.IsCharging || Time.time < astrologicalNextChargeTick)
                return;
            float now = Time.time;
            float damping = Mathf.Clamp01(1f - ((astrologicalChargeStartTime + 1.5f) - now) / 1.5f);
            astrological.StepCharge(now, damping);
            astrologicalNextChargeTick += 0.05f;
            hud?.RefreshAstrologicalDestiny();
        }

        private void OpenCooking()
        {
            if (cookingRuntime != null && cookingRuntime.gameObject != null) return;
            try
            {
                cookingRuntime = CookingFeatureRuntime.Open(profile, hud.CanvasTransform, CookingMaxHp);
            }
            catch (Exception error)
            {
                cookingRuntime = null;
                Debug.LogWarning("Home cooking unavailable: " + error.Message);
                hud.ShowNotice("料理页面打开失败：" + error.Message);
            }
        }

        private void OpenHomeShop()
        {
            if (homeShop != null && homeShop.IsOpen) return;
            try
            {
                homeShop = HomeShopScreenController.Open(this, hud.CanvasTransform);
            }
            catch (Exception error)
            {
                homeShop = null;
                Debug.LogWarning("Home shop unavailable: " + error.Message);
                hud.ShowNotice("售货机页面打开失败：" + error.Message);
            }
        }

        public void OpenSkillGachaFromSource(Action onClosed)
        {
            profile.Reload();
            OpenWishes(onClosed);
        }

        private void OpenWishes(Action onClosed = null)
        {
            if (wishes != null && wishes.IsOpen) return;
            Action refreshWishCurrency = null;
            wishes = WishScreenController.Open(profile, slotId, uiParent,
                slot => refreshWishCurrency = hud.BuildWishCurrencyBar(slot),
                () => refreshWishCurrency?.Invoke(),
                () =>
                {
                    wishes = null;
                    refreshWishCurrency = null;
                    onClosed?.Invoke();
                });
        }

        private void OnFruitTreeClicked()
        {
            if (!AdvanceFruitTree(Now()))
            {
                hud.HideFruitTreeTutorialMask();
                return;
            }
            OfflineFruitTreeProgress state = profile.Save.Player.FruitTree;
            if (state.ProductionRepaired && !state.ProductionRepairStorySeen)
            {
                hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 1, CompleteProductionRepairTutorial);
                return;
            }
            FruitTreeResult collected = fruitTree.Collect("home:fruit-tree:collect:" + slotId + ":"
                + Guid.NewGuid().ToString("N"));
            if (collected.IsApplied)
            {
                ReloadContext();
                hud.RefreshAll();
                ApplyFurnitureStates();
                Action showReward = () => hud.ShowNotice("收取 " + collected.CollectedCount + " 个果实，获得 "
                    + rules.ItemName(OfflineFruitTreeService.RewardItemId) + " × " + collected.CollectedCount);
                bool showCollectionTutorial = pendingFruitTreeCollectionTutorial
                    || !profile.Save.Player.FruitTree.CollectionTutorialSeen;
                pendingFruitTreeCollectionTutorial = false;
                hud.HideFruitTreeTutorialMask();
                if (showCollectionTutorial)
                    hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 2, () =>
                    {
                        fruitTree.MarkCollectionTutorialSeen();
                        showReward();
                    });
                else showReward();
                return;
            }

            if (profile.Save.Player.FruitTree.PetFacilityRepaired)
            {
                OpenFurniture(OfflineFruitTreeService.PetFurnitureType);
                return;
            }
            long seconds = fruitTree.SecondsUntilNextFruit();
            hud.ShowNotice(seconds == long.MaxValue ? "果树暂时没有成熟果实"
                : "下一颗果实将在 " + FormatFruitDuration(seconds) + " 后成熟");
        }

        private static string FormatFruitDuration(long seconds)
        {
            if (seconds < 60) return seconds + " 秒";
            if (seconds < 3600) return (seconds / 60) + " 分钟";
            return (seconds / 3600) + " 小时 " + (seconds % 3600 / 60) + " 分钟";
        }

        private bool AdvanceFruitTree(long now)
        {
            if (fruitTree == null) return true;
            FruitTreeResult result = fruitTree.AdvanceTo(now);
            if (result.Code == FruitTreeResultCode.ClockRollback)
            {
                if (!fruitClockRollbackWarned)
                {
                    if (hud == null) pendingFruitClockRollbackMessage = result.Message;
                    else hud.ShowNotice(result.Message);
                }
                fruitClockRollbackWarned = true;
                if (scene != null)
                    scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                        FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                        profile.Save.Player.FruitTree.StoredCount);
                return false;
            }
            else if (profile.Save.Player.FruitTree.LastUpdateUnixSeconds <= now)
            {
                fruitClockRollbackWarned = false;
                pendingFruitClockRollbackMessage = null;
            }
            if (scene != null)
                scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                    FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                    profile.Save.Player.FruitTree.StoredCount);
            return true;
        }

        public bool TryRepair(int type)
        {
            string receipt = "home:repair:" + slotId + ":" + type;
            bool repaired;
            if (type == OfflineFruitTreeService.ProductionFurnitureType
                || type == OfflineFruitTreeService.PetFurnitureType)
            {
                long now = Now();
                profile.Reload();
                ReloadContext();
                if (!AdvanceFruitTree(now)) return false;
                profile.Reload();
                ReloadContext();
                FruitTreeResult result = fruitTree.TryRepair(type, profile.Save.Player.Level,
                    context.AnyGoddessActive, now, receipt);
                repaired = result.IsApplied;
                if (!repaired) hud?.ShowNotice(result.Code == FruitTreeResultCode.ConditionNotMet
                    ? "尚未满足维修条件" : result.Message);
                if (repaired)
                {
                    ReloadContext();
                    scene?.PlayFruitTreeRepairEffect(type);
                }
            }
            else
            {
                string costReceipt = receipt + ":cost:" + System.Guid.NewGuid().ToString("N");
                repaired = homeProgression.RepairFurniture(session, type, context, profile.InventoryCount,
                    costs => ConsumeItems(costs, costReceipt), costs => RestoreItems(costs, costReceipt + ":refund"), receipt);
            }
            if (repaired)
            {
                ApplyFurnitureStates();
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
                FurnitureRepairedForTutorial?.Invoke(type);
            }
            return repaired;
        }

        public void OnFruitTreeRepairDialogClosed(int type)
        {
            OfflineFruitTreeProgress state = profile.Save.Player.FruitTree;
            if (type == OfflineFruitTreeService.ProductionFurnitureType && !state.ProductionRepairStorySeen)
                hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 1,
                    CompleteProductionRepairTutorial);
            else if (type == OfflineFruitTreeService.PetFurnitureType && !state.PetRepairStorySeen)
                hud.ShowFruitTreeTutorial("FruitTreeUnlock2", 1,
                    () => fruitTree.MarkRepairStorySeen(type));
        }

        private void CompleteProductionRepairTutorial()
        {
            FruitTreeResult result = fruitTree.MarkRepairStorySeen(OfflineFruitTreeService.ProductionFurnitureType);
            if (result.Code != FruitTreeResultCode.Applied && result.Code != FruitTreeResultCode.Unchanged)
            {
                hud.ShowNotice("果树教学状态未能保存");
                return;
            }
            pendingFruitTreeCollectionTutorial = true;
            hud.ShowFruitTreeCollectionMask();
        }

        public long TryCollect(int hangUpId)
        {
            string receipt = tutorialHost?.CollectionReceiptFor(hangUpId)
                ?? ("home:collect:" + slotId + ":" + hangUpId + ":" + Guid.NewGuid().ToString("N"));
            long count = session.Collect(hangUpId, receipt);
            if (count > 0)
            {
                HangUpCollectedForTutorial?.Invoke(hangUpId, count, receipt);
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return count;
        }

        public long TryFastForward(int hangUpId)
        {
            OfflineHomeIncomeDefinition definition = session.Definitions.FirstOrDefault(value => value.HangUpId == hangUpId);
            if (definition == null || definition.FastHangUpId <= 0) return 0;
            HomeFastHangUpRecord fast = rules.FastHangUp(definition.FastHangUpId);
            if (fast == null) return 0;
            string receipt = tutorialHost?.FastForwardReceiptFor(hangUpId, fast.fastHangUpId)
                ?? ("home:fast:" + slotId + ":" + fast.fastHangUpId + ":" + Guid.NewGuid().ToString("N"));
            long total = session.ApplyFreeFastForwardGroup(fast.fastHangUpId,
                fast.hangUpIdList ?? Array.Empty<int>(), fast.freeHours, receipt);
            if (total > 0)
            {
                FastForwardAppliedForTutorial?.Invoke(receipt);
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return total;
        }

        public long TryFastForwardItems(int fastHangUpId, int itemCount)
        {
            HomeFastHangUpRecord fast = rules.FastHangUp(fastHangUpId);
            HomeFastHangUpItem item = fast?.items?.FirstOrDefault(value => value != null && value.itemId > 0);
            if (fast == null || item == null || itemCount <= 0
                || itemCount > Math.Min(100, fast.maxUseItemCount)
                || profile.InventoryCount(item.itemId) < itemCount) return 0;
            int[] unlocked = (fast.hangUpIdList ?? Array.Empty<int>())
                .Where(id => session.Definitions.Any(definition => definition.HangUpId == id && definition.Unlocked))
                .Distinct().ToArray();
            if (unlocked.Length == 0) return 0;
            string receipt = "home:fast-item:" + slotId + ":" + Guid.NewGuid().ToString("N");
            if (!ConsumeItems(item.itemId, itemCount, receipt + ":cost")) return 0;
            long total = session.ApplyFastForwardBatch(unlocked, itemCount * item.hours, receipt + ":income");
            if (total > 0)
            {
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return total;
        }


        public void TryOpenMail() => hud.ShowMail();

        public bool TryUnlockHangUp(int hangUpId)
        {
            bool unlocked = homeProgression.UnlockHangUp(session, hangUpId, context,
                "home:unlock:" + slotId + ":" + hangUpId);
            if (unlocked)
            {
                RefreshIncomeCatalog();
                hud.RefreshAll();
            }
            return unlocked;
        }

        public HomeLevelUpResult TryLevelUpHangUp(int hangUpId)
        {
            HomeLevelUpResult check = homeProgression.CheckLevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount);
            if (!check.CanApply) return check;
            long now = Now();
            if (now < session.Save.LastLocalUnixSeconds)
            {
                check.CanApply = false;
                check.Error = "clock_rollback";
                return check;
            }
            session.AdvanceTo(now);
            long settled = session.Collect(hangUpId, "home:levelup-settle:" + slotId + ":" + hangUpId
                + ":" + session.Save.LastLocalUnixSeconds);
            if (settled > 0) DrainHomeInventory();
            string costReceipt = "home:levelup:" + slotId + ":" + hangUpId + ":cost:"
                + System.Guid.NewGuid().ToString("N");
            HomeLevelUpResult result = homeProgression.LevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount, costs => ConsumeItems(costs, costReceipt),
                costs => RestoreItems(costs, costReceipt + ":refund"),
                "home:levelup:" + slotId + ":" + hangUpId + ":" + check.Level);
            if (result.Success)
            {
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return result;
        }

        private bool ConsumeItems(int itemId, long count, string receipt)
        {
            if (itemId <= 0 || count <= 0) return true;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
                if (stack == null || stack.Count < count) throw new InvalidOperationException("Not enough items.");
                stack.Count -= count;
                if (stack.UnitRecords != null && stack.UnitRecords.Count > stack.Count)
                    stack.UnitRecords.RemoveRange((int)stack.Count, stack.UnitRecords.Count - (int)stack.Count);
                if (stack.Count == 0) save.Player.Inventory.Remove(stack);
            });
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged && result.Status != ProgressionResultStatus.Ready)
                return false;
            ReloadContext();
            return true;
        }

        private bool ConsumeItems(IReadOnlyList<HomeCostEntry> costs, string receipt)
        {
            if (costs == null || costs.Count == 0) return true;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (IGrouping<int, HomeCostEntry> group in costs
                    .Where(value => value != null && value.itemId > 0 && value.count > 0)
                    .GroupBy(value => value.itemId))
                {
                    long count = group.Aggregate(0L, (total, value) => checked(total + value.count));
                    OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == group.Key);
                    if (stack == null || stack.Count < count) throw new InvalidOperationException("Not enough items.");
                    stack.Count -= count;
                    if (stack.UnitRecords != null && stack.UnitRecords.Count > stack.Count)
                        stack.UnitRecords.RemoveRange((int)stack.Count, stack.UnitRecords.Count - (int)stack.Count);
                    if (stack.Count == 0) save.Player.Inventory.Remove(stack);
                }
            });
            if (!InventoryTransactionSucceeded(result.Status)) return false;
            ReloadContext();
            return true;
        }

        private void RestoreItems(IReadOnlyList<HomeCostEntry> costs, string receipt)
        {
            if (costs == null || costs.Count == 0) return;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (IGrouping<int, HomeCostEntry> group in costs
                    .Where(value => value != null && value.itemId > 0 && value.count > 0)
                    .GroupBy(value => value.itemId))
                {
                    long count = group.Aggregate(0L, (total, value) => checked(total + value.count));
                    OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == group.Key);
                    if (stack == null)
                    {
                        stack = new OfflineItemStack { ItemId = group.Key };
                        save.Player.Inventory.Add(stack);
                    }
                    stack.Count = checked(stack.Count + count);
                }
            });
            if (!InventoryTransactionSucceeded(result.Status))
                Debug.LogError("Home upgrade cost compensation failed: " + result.Status);
            else ReloadContext();
        }

        private static bool InventoryTransactionSucceeded(ProgressionResultStatus status)
        {
            return status == ProgressionResultStatus.Applied || status == ProgressionResultStatus.Unchanged
                || status == ProgressionResultStatus.Ready;
        }

        /// <summary>Moves claimed home rewards into the real bag or character experience.</summary>
        public void DrainHomeInventory()
        {
            foreach (OfflineHomeItemStack stack in session.Save.Inventory.ToArray())
            {
                if (stack == null || stack.ItemId <= 0 || stack.Count <= 0) continue;
                string receipt = "home:grant:" + slotId + ":" + stack.Serial;
                if (stack.ItemId == 50)
                {
                    try
                    {
                        EnsureProgression();
                        progression.AwardExperience(stack.Count, receipt);
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("Home experience could not be granted: " + error.Message);
                        continue;
                    }
                }
                else
                {
                    ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
                    {
                        OfflineItemStack target = save.Player.Inventory.FirstOrDefault(value => value.ItemId == stack.ItemId);
                        if (target == null)
                        {
                            target = new OfflineItemStack { ItemId = stack.ItemId };
                            save.Player.Inventory.Add(target);
                        }
                        target.Count += stack.Count;
                    });
                    if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged && result.Status != ProgressionResultStatus.Ready)
                        continue;
                }
                session.AcknowledgeInventory(stack.Serial, receipt + ":ack");
            }
            ReloadContext();
        }

        public long ItemCount(int itemId) => profile == null ? 0 : profile.InventoryCount(itemId);

        public string ItemName(int itemId) => rules.ItemName(itemId);

        public string ItemNameForHangUp(int hangUpId)
        {
            HomeHangUpRecord record = rules.HangUp(hangUpId);
            HomeHangUpRewardRecord reward = record == null ? null : rules.HangUpReward(record.rewardId);
            HomeRewardRuleRecord rule = reward == null ? null : rules.SelectRewardRule(reward.rewardRuleId, context);
            int itemId = rule != null && rule.ids != null && rule.ids.Length > 0 ? rule.ids[0] : 0;
            string name = rules.ItemName(itemId);
            return string.IsNullOrEmpty(name) ? "挂机产出 " + hangUpId : name;
        }

        public Sprite ItemIcon(int itemId)
        {
            string file = rules.ItemIcon(itemId);
            return string.IsNullOrEmpty(file) ? null : catalog.FindSprite(file);
        }

        public IReadOnlyList<int> TreasureGachaSourceTypes(int itemId)
        {
            if (treasureGacha == null) return Array.Empty<int>();
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var types = new List<int>(2);
            if (treasureGacha.ResolveItemSource(itemId, now) != null) types.Add(0);
            TreasureItemConfig sourceItem = treasureGacha.Catalog.Item(itemId);
            TreasureGachaSourceNavigation elementSource = sourceItem == null ? null
                : treasureGacha.ResolveElementItemSource(itemId, sourceItem.ElementType, now);
            if (elementSource != null) types.Add(elementSource.SelectedElementGachaType);
            return types.Distinct().ToArray();
        }

        public bool OpenTreasureGachaSource(int itemId, int gachaType, Action<int> returnToSource)
        {
            if (gachaType == 0)
                return OpenTreasureGachaFromItemSource(itemId, 0, returnToSource) != null;
            TreasureItemConfig sourceItem = treasureGacha?.Catalog.Item(itemId);
            return sourceItem != null && this.OpenTreasureGachaFromElementItemSource(itemId,
                sourceItem.ElementType, returnToSource) != null;
        }

        public void ShowInventoryNotice(string message) => hud?.ShowNotice(message);

        public bool EnterMainland()
        {
            if (onOpenMainland == null)
            {
                hud?.ShowNotice("大陆入口当前不可用");
                return false;
            }
            onOpenMainland();
            return true;
        }

        public void Close()
        {
            Close(true);
        }

        public void CloseForNavigation()
        {
            Close(false);
        }

        private void Close(bool invokeCallback)
        {
            tutorialRunner?.Suspend();
            tutorialRunner = null;
            tutorialHost?.Dispose();
            tutorialHost = null;
            WishScreenController activeWishes = wishes;
            wishes = null;
            if (activeWishes != null) activeWishes.Close();
            if (rewards != null) rewards.Dispose();
            homePlayer?.Dispose();
            homePlayer = null;
            if (environment != null && meteorView != null)
                environment.MeteorTriggered -= meteorView.Play;
            meteorView?.Dispose();
            meteorView = null;
            environment?.Dispose();
            environment = null;
            if (scene != null) scene.Dispose();
            scene = null;
            if (catalog != null) catalog.Release();
            catalog = null;
            if (hud != null) hud.Dispose();
            hud = null;
            homeRenderPipeline?.Dispose();
            homeRenderPipeline = null;
            if (invokeCallback) onClose?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            // The damage-test scene owns input and presentation until its battle ends.
            // Home income catches up from its existing timestamp on return.
            if (hud != null && hud.SkillDamageTestRunning) return;
            input?.Update();
            TickAstrologicalCharge();
            environment?.Tick(Time.unscaledDeltaTime);
            scene?.Tick(Time.unscaledDeltaTime);
            hud?.Tick();
            wishes?.Tick();
            if (tutorialAutoStartEnabled && tutorialRunner != null)
            {
                if (!tutorialRunner.IsBlocked && !tutorialRunner.HasActiveGuide && !tutorialRunner.HasQueuedGuide)
                {
                    HomeTutorialGuide next = tutorials.Guides.OrderBy(value => value.order)
                        .FirstOrDefault(value => !tutorialRunner.IsGuideCompleted(value.name));
                    if (next != null) tutorialRunner.QueueGuide(next.name);
                }
                tutorialRunner.Tick(Time.unscaledDeltaTime);
            }
            advanceTimer += Time.unscaledDeltaTime;
            if (advanceTimer < 5f) return;
            advanceTimer = 0;
            long now = Now();
            if (now == lastAdvanceUnix) return;
            lastAdvanceUnix = now;
            try
            {
                if (!AdvanceFruitTree(now)) return;
                session.AdvanceTo(now);
                rewards?.Refresh();
                DrainHomeInventory();
                hud?.RefreshAll();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home income advance failed: " + error.Message);
            }
        }

        private void OnDestroy()
        {
            EndAstrologicalCharge();
            tutorialRunner?.Suspend();
            tutorialRunner = null;
            tutorialHost?.Dispose();
            tutorialHost = null;
            if (input != null) input.FurnitureClicked -= OnFurnitureClicked;
            if (environment != null && meteorView != null)
                environment.MeteorTriggered -= meteorView.Play;
            meteorView?.Dispose();
            meteorView = null;
            environment?.Dispose();
            environment = null;
            homePlayer?.Dispose();
            homePlayer = null;
            StopFruitTreeTutorialVoice();
            HomeAudioSettings.Unregister(fruitTreeTutorialVoiceSource);
            HomeAudioSettings.Unregister(settingsClickSource);
            if (settingsClickClip != null) Destroy(settingsClickClip);
            settingsClickClip = null;
            homeRenderPipeline?.Dispose();
            homeRenderPipeline = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) tutorialRunner?.Suspend();
        }

        private void OnApplicationQuit() => tutorialRunner?.Suspend();
    }
}
