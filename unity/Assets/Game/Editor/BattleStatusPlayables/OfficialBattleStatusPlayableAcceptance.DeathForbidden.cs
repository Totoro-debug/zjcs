using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        public static void RunDeathForbidden()
        {
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            Run();
        }

        private sealed partial class Scope
        {
            private static string DeathRule(string name) => File.ReadAllText(Path.Combine(
                Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets/Official/Combat/" + name + ".json"));
            private static string DeathHash(string text)
            {
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
            }

            private IEnumerator DeathForbiddenSteps()
            {
                string skills = DeathRule("skill-rules"), battle = DeathRule("battle-rules"), rules = DeathRule("rules");
                var catalog = FightOfficialCombatCatalog.Load(skills, DeathHash(skills), "854_156383",
                    battle, DeathHash(battle), "854_156383");
                var formula = catalog.CreateDamageFormula(rules, DeathHash(rules), "854_156383");
                var agent = sourceA.GetComponent<MyFightStatusAgent>();
                var text = Resources.Load<TextAsset>("Combat/status-orbits");
                var entry = JsonUtility.FromJson<OrbitCatalog>(text.text).graphs.Single(value => value.id == 24477);
                var prefab = Resources.Load<GameObject>(entry.resource);
                foreach (bool instant in new[] { false, true })
                {
                    var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 10, RandomSeed = 23,
                        Grid = new FightBattleGrid { Width = 12, Height = 8, DefaultGroundType = 2 } };
                    for (int id = 1; id <= 3; id++)
                    {
                        var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
                            CharacterType = 1, X = id + 2, Y = 2, Hp = id == 2 ? 10 : 1000,
                            MaxHp = 1000, LastMaxHp = 1000, Level = 1, SubRank = 1 };
                        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 1100 - id * 100;
                        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = 1000 });
                        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
                        var skill = new FightBattleSkillDefinition { EntityId = 990800 + id, Slot = 0,
                            TargetType = 1, CanTriggerChild = true, Duration = 1, ActionDuration = 1 };
                        for (int x = -4; x <= 4; x++) skill.Range.Add(new FightGridPoint(x, 0));
                        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990900 + id, DamageId = 990900 + id,
                            DamageType = 8, HasCustomDamage = true, CustomDamage = 20 });
                        unit.Skills.Add(skill); unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
                        cp.Units.Add(unit);
                    }
                    var wave = new FightBattleWaveState { Index = 0 }; wave.UnitIds.Add(2); wave.UnitIds.Add(3); cp.Waves.Add(wave);
                    var victim = cp.Units[1];
                    var status = catalog.CreateStatus(24477, 2, 2, 9924477, 10110); status.StackCount = 3;
                    victim.Statuses.Add(status);
                    if (instant)
                    {
                        var hp = catalog.CreateStatus(60410, 2, 2, 9960410, 10110);
                        hp.ForbiddenOnApplicatorDie = true; hp.HpLessThanMaxCount = 1;
                        hp.HpLessThanStatuses.Clear();
                        hp.HpLessThanStatuses.Add(new FightBattleStatusStateTrigger { EntityId = 24233, ApplyTarget = 2, BasePercent = 1 });
                        victim.Statuses.Add(hp);
                    }
                    var state = new State(victim, status);
                    var go = new GameObject("Settled death original status view"); go.transform.SetParent(root.transform, false);
                    var view = go.AddComponent<MyFightStatus>(); view.StackPlayable = prefab;
                    view.Bind(new Context { State = state, Owner = sourceA, Creator = sourceB, Agent = agent }, manager);
                    var settlement = new FightDamageSettlement();
                    bool observed = false;
                    settlement.SourceHit += _ => { if (!observed) { observed = true; state.ObserveExecutorStep(); } };
                    var runner = FightBattleExecutor.Begin(cp, formula, settlement, catalog);
                    view.OnStart(true);
                    for (int frame = 0; frame < 10; frame++) { yield return null; Flush(); }
                    var graph = RootGraph(view);
                    var orbit = graph.GetComponentInChildren<MyPlayableOrbitMovement>(true);
                    Require(Orbiters(orbit).Count == 3, "Actual native graph did not start from the preserved three stacks.");
                    Capture("death-" + instant + "-before");
                    runner.Step(0, 1);
                    for (int frame = 0; frame < 10; frame++) { yield return null; Flush(); }
                    Require(observed && victim.Hp == (instant ? 1 : 0) && state.Forbidden == !instant,
                        "Actual executor death/instant revival did not drive the status view.");
                    if (instant)
                    {
                        Require(RootGraph(view) == graph && Orbiters(orbit).Count == 3,
                            "Same-hit revival interrupted or replaced the native status graph.");
                        Capture("death-true-instant-revive");
                    }
                    else
                    {
                        Require(Orbiters(orbit).Count == 0 && !graph.gameObject.activeInHierarchy,
                            "Confirmed death left original orbit graphics running.");
                        Capture("death-false-forbidden", false);
                        settlement.SourceHit += _ =>
                        {
                            state.ObserveExecutorStep();
                            FightStatusLifecycle.Apply(victim, catalog.CreateStatus(24233, 2, 1, 9924233, 10120), 0, 9924233);
                        };
                        runner.Step(0, 2);
                        for (int frame = 0; frame < 10; frame++) { yield return null; Flush(); }
                        orbit = RootGraph(view).GetComponentInChildren<MyPlayableOrbitMovement>(true);
                        Require(victim.Hp == 1 && !state.Forbidden && state.StackedCount == 3 && Orbiters(orbit).Count == 3,
                            "Positive HP failed to resume the native graph with preserved stacks.");
                        Capture("death-false-later-revive");
                    }
                    view.OnEnd(); yield return null; Flush();
                    Require(state.Listeners == 0, "Death-linked view retained subscriptions after end.");
                    manager.RemoveAllPlayable(true); center.RemoveAll();
                }
                Debug.Log("Settled death native view passed: real fatal hits, official HP-triggered BackToLife, no same-hit graph interruption, confirmed-death pool return and later revival with three preserved original orbiters. Step-scoped notification binding is a fixture adapter.");
            }
        }
    }
}
