using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

public sealed class HomeFurnitureNativeState
{
    private readonly ProfileSlotCatalog slots;
    private readonly string slot;
    private readonly OfflineMainlineSave saved = new OfflineMainlineSave();
    private readonly OfficialHomeRules rules;
    private readonly OfflineHomeProgression progression;
    private readonly OfflineHomeConditionPlayer player;
    private OfflineHomeIncomeSession home;
    public HomeFurnitureNativeState()
    {
        slots = new ProfileSlotCatalog(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
            "native-furniture-saves-" + Guid.NewGuid().ToString("N")));
        slot = slots.CreateSlot(); home = Open();
        var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        rules = OfficialHomeRules.LoadDefault();
        player = new OfflineHomeConditionPlayer(configuration, () => saved, type => home.OwnsFurniture(type));
        new OfflineCurrentHomeFurniture(configuration, OfficialPartnerAssetCatalog.LoadDefault().Conditions, () => player).Bind(rules);
        progression = new OfflineHomeProgression(rules);
        saved.Player.Level = 3;
    }
    public bool CanRepairBed => progression.FurnitureState(home, 0, new HomeConditionContext()) == HomeFurnitureState.CanRepair;
    public bool HasCost(int furniture) => rules.FurnitureOf(furniture).repairCost.All(cost => player.GetItemAmount(cost.itemId) >= cost.count);
    public void Repair(int furniture)
    {
        if (!progression.RepairFurniture(home, furniture, new HomeConditionContext(), player.GetItemAmount,
            null, null, "native-repair-" + furniture)) throw new InvalidOperationException("Real furniture repair did not commit.");
    }
    public void VerifyReopen()
    {
        home = Open();
        if (!home.OwnsFurniture(0) || player.GetItemAmount(4000) != 1)
            throw new InvalidOperationException("Pointer repair did not survive reopening the save.");
    }
    private OfflineHomeIncomeSession Open() => OfflineHomeIncomeSession.Open(slots, slot,
        new[] { new OfflineHomeIncomeDefinition { HangUpId = 1, OutputItemId = 50,
            Rule = new HangUpIncomeRule { LevelNum = 1, MaxStoreHours = 36 } } },
        new Dictionary<int, long>(), new HashSet<int>(), Array.Empty<OfflineHomeRandomRewardDefinition>(), 1000000);
}
