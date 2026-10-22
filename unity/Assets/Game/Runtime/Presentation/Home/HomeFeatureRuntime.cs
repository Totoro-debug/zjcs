using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Entry point of the offline home module.  It opens the character save and
    /// the home save, rebuilds the official scene and HUD, and routes every
    /// furniture action through the offline rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeFeatureRuntime : MonoBehaviour
    {
        private ProfileSlotCatalog slots;
        private OfflineMainlineProgressSession profile;
        private OfflineCharacterProgressionService progression;
        private OfficialHomeRules rules;
        private OfflineHomeProgression homeProgression;
        private OfflineHomeIncomeSession session;
        private HomeAssetCatalog catalog;
        private HomeSceneView scene;
        private HomeInputController input;
        private HomeEnvironmentView environment;
        private HomeRewardView rewards;
        private HomeHudView hud;
        private HomeConditionContext context;
        private Action onClose;
        private string slotId;
        private float advanceTimer;
        private long lastAdvanceUnix;

        public string ActiveProfileSlotId => slotId;
        public OfflineHomeIncomeSession Session => session;
        public OfflineHomeProgression HomeProgression => homeProgression;
        public HomeAssetCatalog Catalog => catalog;
        public HomeHudView Hud => hud;
        public OfficialHomeRules Rules => rules;
        public OfflineMainlineProgressSession Profile => profile;
        public HomeConditionContext Context => context;
        public int BlessingLevel { get; private set; } = 1;
        public HomeEnvironmentView Environment => environment;

        public string CharacterName => profile?.Save.Player.CharacterName ?? string.Empty;
        public int Level => profile?.Save.Player.Level ?? 1;
        public int SubRank => profile?.Save.Player.SubRank ?? 1;
        public int Rank => profile?.Save.Player.Rank ?? 1;
        public string GuildName => profile?.Save.Player.GuildName ?? string.Empty;

        public static HomeFeatureRuntime Begin(string selectedSlotId, string savesRoot, Transform uiParent,
            Action onClose = null)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("An existing profile slot is required.", nameof(selectedSlotId));
            var go = new GameObject("HomeGameRuntime");
            HomeFeatureRuntime runtime = go.AddComponent<HomeFeatureRuntime>();
            runtime.Open(selectedSlotId, string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot,
                uiParent, onClose);
            return runtime;
        }

        private void Open(string selected, string root, Transform uiParent, Action close)
        {
            slotId = selected;
            onClose = close;
            slots = new ProfileSlotCatalog(root);
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile does not exist.");
            profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            profile.Reload();
            catalog = HomeAssetCatalog.LoadDefault();
            rules = OfficialHomeRules.LoadDefault();
            homeProgression = new OfflineHomeProgression(rules);
            BlessingLevel = 1;
            ReloadContext();
            session = OpenSession();
            lastAdvanceUnix = Now();
            scene = new HomeSceneView(catalog, transform);
            environment = new HomeEnvironmentView(scene, Now());
            rewards = new HomeRewardView(this, catalog, transform);
            input = new HomeInputController(scene, () => hud != null && hud.AnyModalOpen);
            input.FurnitureClicked += OnFurnitureClicked;
            input.RewardClicked += OnRewardClicked;
            input.SetRewardHit(OnRewardHit);
            hud = new HomeHudView(this, uiParent);
            hud.Build();
            ApplyFurnitureStates();
            RefreshIncomeCatalog();
            DrainHomeInventory();
            hud.RefreshAll();
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private void ReloadContext()
        {
            OfflinePlayerProgress player = profile.Save.Player;
            context = new HomeConditionContext
            {
                Level = player.Level,
                SubRank = player.SubRank,
                Rank = player.Rank,
                MaxProfessionRank = player.MaxProfessionRank,
                InitialWeaponType = player.InitialWeaponType,
                Profession = player.Wardrobe.Profession,
                ItemCount = profile.InventoryCount,
                StoryCount = _ => 0,
                StoryPlayed = _ => false,
                HasItemOfType = _ => false,
                UnlockedMapClassIds = new HashSet<int>(player.UnlockedMapClassIds ?? new List<int>())
            };
            rules.CurrentContext = context;
        }

        private OfflineHomeIncomeSession OpenSession()
        {
            var properties = PlayerProperties();
            session = OfflineHomeIncomeSession.Open(slots, slotId, new[] { PlaceholderDefinition() },
                properties, new HashSet<int>(), Array.Empty<OfflineHomeRandomRewardDefinition>(), Now());
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(
                session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, properties, new HashSet<int>(), rewards);
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
            try
            {
                EnsureProgression();
                foreach (KeyValuePair<int, long> pair in progression.BaseBattleProperties()) merged[pair.Key] = pair.Value;
                foreach (KeyValuePair<int, long> pair in progression.AppearanceProperties(Now()))
                    merged[pair.Key] = merged.TryGetValue(pair.Key, out long existing) ? existing + pair.Value : pair.Value;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home properties unavailable: " + error.Message);
            }
            return merged;
        }

        private void EnsureProgression()
        {
            if (progression != null) return;
            OfficialCharacterCatalog character = OfficialCharacterAssetCatalog.LoadDefault().Load();
            progression = new OfflineCharacterProgressionService(profile, character);
        }

        public void RefreshIncomeCatalog()
        {
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, PlayerProperties(), new HashSet<int>(), rewards);
        }

        private void ApplyFurnitureStates()
        {
            foreach (HomeFurnitureRecord record in rules.Furniture)
            {
                HomeFurnitureState state = homeProgression.FurnitureState(session, record.type, context);
                scene.SetFurnitureState(record.type, state);
            }
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
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return;
            HomeFurnitureState state = homeProgression.FurnitureState(session, type, context);
            switch (state)
            {
                case HomeFurnitureState.Functional:
                    if (!hud.IsClosedByNetworkPolicy(type)) OpenFurniture(type);
                    break;
                case HomeFurnitureState.CanRepair:
                    hud.ShowRepair(type);
                    break;
                case HomeFurnitureState.RepairNotice:
                    hud.ShowNotice(rules.IsMet(record.repairNotice, context)
                        ? HomeNoticeText(type)
                        : "尚未满足解锁条件");
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
            else hud.ShowFurniturePlaceholder(type);
        }

        public bool TryRepair(int type)
        {
            string receipt = "home:repair:" + slotId + ":" + type;
            bool repaired = homeProgression.RepairFurniture(session, type, context, profile.InventoryCount,
                (item, count) => ConsumeItems(item, count, receipt + ":cost"), receipt);
            if (repaired)
            {
                ApplyFurnitureStates();
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return repaired;
        }

        public long TryCollect(int hangUpId)
        {
            string receipt = "home:collect:" + slotId + ":" + hangUpId + ":" + Guid.NewGuid().ToString("N");
            long count = session.Collect(hangUpId, receipt);
            if (count > 0)
            {
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
            long total = 0;
            foreach (int id in fast.hangUpIdList ?? Array.Empty<int>())
            {
                string receipt = "home:fast:" + slotId + ":" + id + ":" + Guid.NewGuid().ToString("N");
                total += session.ApplyFastForward(id, fast.freeHours, receipt);
            }
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
            HomeLevelUpResult result = homeProgression.LevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount, (item, count) => ConsumeItems(item, count,
                    "home:levelup:" + slotId + ":" + hangUpId + ":" + item), "home:levelup:" + slotId + ":" + hangUpId);
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

        public void Close()
        {
            if (rewards != null) rewards.Dispose();
            if (scene != null) scene.Dispose();
            scene = null;
            if (catalog != null) catalog.Release();
            catalog = null;
            if (hud != null) hud.Dispose();
            hud = null;
            onClose?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            input?.Update();
            environment?.Tick(Time.unscaledDeltaTime);
            hud?.Tick();
            advanceTimer += Time.unscaledDeltaTime;
            if (advanceTimer < 5f) return;
            advanceTimer = 0;
            long now = Now();
            if (now == lastAdvanceUnix) return;
            lastAdvanceUnix = now;
            try
            {
                session.AdvanceTo(now);
                DrainHomeInventory();
                hud?.RefreshCounts();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home income advance failed: " + error.Message);
            }
        }

        private void OnDestroy()
        {
            if (input != null) input.FurnitureClicked -= OnFurnitureClicked;
        }
    }
}
