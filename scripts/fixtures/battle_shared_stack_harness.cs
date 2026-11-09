using System;
using System.Linq;
using Zjcs.Combat.Official;
public static class BattleSharedStackHarness
{
 static void Check(bool value, string message) { if (!value) throw new Exception(message); }
 public static void Run(FightOfficialCombatCatalog catalog)
 {
  var owner = new FightBattleUnitState { Id = 1, Hp = 100, MaxHp = 100 };
  int starts = 0; owner.StatusStarted = (unit, status) => starts++;
  FightBattleStatusState Add(int id, int creator, int count) {
   var template = catalog.CreateStatus(id, owner.Id, creator, creator, 10000 + creator);
   template.StackCount = count;
   return FightStatusLifecycle.Apply(owner, template, 0, creator);
  }
  var a = Add(24022, 1, 99); var b = Add(24023, 2, 2); var c = Add(24024, 3, 2);
  Check(a.StackCount == 2 && b.StackCount == 2 && c.StackCount == 1, "Partial first allocation");
  Check(FightStatusSharedStacks.Total(owner, 25300) == 5, "Original shared limit 5");
  Check(Add(24025, 4, 2) == null && owner.Statuses.Count == 3 && starts == 3
    && owner.PendingStatusApplications.Count == 3, "Zero quota emits no creation or application events");
  Check(FightStatusLifecycle.AddStack(owner, c, 1) == 0, "Existing stack capped by pool");
  Check(FightStatusLifecycle.AddStack(owner, a, -1) == -1, "Consume releases quota");
  Check(FightStatusLifecycle.AddStack(owner, c, 1) == 1, "Existing status takes free quota");
  Check(FightStatusLifecycle.AddStack(owner, c, 1) == 0 && c.StackCount == 2, "Individual cap retained");
  Check(FightStatusLifecycle.End(owner, b), "End releases quota");
  Check(!FightStatusLifecycle.End(owner, b), "Repeated removal does not release twice");
  var d = Add(24025, 4, 2); Check(d.StackCount == 2, "Reallocate after end");
  var independent = new FightBattleUnitState { Id = 2 };
  var independentTemplate = catalog.CreateStatus(24022, 2, 1, 1); independentTemplate.StackCount = 2;
  Check(FightStatusLifecycle.Apply(independent, independentTemplate, 0, 1).StackCount == 2, "Pools are per owner");
  var e = Add(24166, 5, 7); var f = Add(24385, 6, 7);
  Check(e.StackCount == 7 && f.StackCount == 3 && FightStatusSharedStacks.Total(owner, 24029) == 10, "Distinct original group and limit 10");
  owner.Turn = new FightTurnUnit { Id = owner.Id };
  var checkpoint = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 31, Grid = new FightBattleGrid { Width = 20, Height = 20 }, ResultRules = new FightBattleResultRules() }; checkpoint.Units.Add(owner);
  string json = FightBattleCheckpointSerializer.Save(checkpoint);
  var restored = FightBattleCheckpointSerializer.Load(json).Units[0];
  Check(FightStatusSharedStacks.Total(restored, 25300) == 5 && FightStatusSharedStacks.Total(restored, 24029) == 10, "Checkpoint preserves occupied quota");
  var restoredC = restored.Statuses.Single(x => x.ClassId == 24024);
  Check(restoredC.SharedStackId == 25300 && restoredC.SharedStackLimit == 5, "Current checkpoint preserves selected original metadata");
  Check(FightStatusLifecycle.AddStack(restored, restoredC, -1) == -1, "Restore can consume");
  Check(FightStatusLifecycle.AddStack(restored, restoredC, 1) == 1, "Restore can allocate");
  bool guarded = false; try { FightStatusLifecycle.AddStack(restoredC, 1); } catch (InvalidOperationException) { guarded = true; }
  Check(guarded, "Shared status cannot bypass owner constraint");
 }
}
