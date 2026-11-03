using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        private sealed partial class Scope
        {
            private GameObject ViewTemplate(string name, bool stack = false, bool creator = false)
            {
                GameObject result = Template(name, -1);
                if (stack) result.AddComponent<ApplyByStatusStackCount>();
                if (creator) result.AddComponent<ApplyOverrideToStatusSource>();
                return result;
            }

            private MyFightStatus View(State state, MyFightStatusAgent agent, string name,
                int? share = null, bool full = false, bool creator = false)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var view = go.AddComponent<MyFightStatus>();
                view.Bind(new Context { State = state, Owner = sourceA, Creator = sourceB,
                    Master = sourceB, Agent = agent, ShareId = share, HasIcon = true }, manager);
                view.StackPlayableMap = new List<StackCountPlayableMap> {
                    new StackCountPlayableMap { StackCount = 1, MaxStackCount = 3,
                        BeginPlayable = Template(name + "-begin-low", .01f),
                        PlayablePrefab = ViewTemplate(name + "-stay-low"),
                        EndPlayable = Template(name + "-end-low", .01f) },
                    new StackCountPlayableMap { StackCount = 4, MaxStackCount = 10,
                        BeginPlayable = Template(name + "-begin-high", .01f),
                        PlayablePrefab = ViewTemplate(name + "-stay-high"),
                        EndPlayable = Template(name + "-end-high", .01f) }
                };
                if (full)
                {
                    view.myPlayablePrefab = ViewTemplate(name + "-start", creator: creator);
                    view.StackedPlayablePrefab = Template(name + "-stacked", .03f);
                    view.StackPlayable = ViewTemplate(name + "-param", stack: true);
                    view.MasterCastPlayable = ViewTemplate(name + "-master");
                    view.InvokePlayable = Template(name + "-invoke", .01f);
                }
                return view;
            }

            private ProbePlayable Live(string name) => instances.Last(value => value != null
                && value.Id == name && value.gameObject.activeSelf);
            private string[] Plays() => events.Where(value => value.StartsWith("play:")).ToArray();

            private IEnumerator StatusViewSteps()
            {
                var agent = sourceA.AddComponent<MyFightStatusAgent>();
                var state = new State(1);
                var view = View(state, agent, "view", full: true);
                var notifications = new List<string>();
                agent.OnStatusApply += value => { Require(ReferenceEquals(value, view), "Wrong applied view."); notifications.Add("apply"); };
                // Use separate listeners for this first view; later views share the same agent.
                Action<MyFightStatus> ended = value => notifications.Add("end");
                agent.OnStatusEnd += ended;
                events.Clear(); view.OnStart(true);
                Require(Plays().SequenceEqual(new[] { "play:view-start", "play:view-stacked",
                    "play:view-begin-low", "play:view-stay-low", "play:view-param", "play:view-master" }),
                    "Status start lost original slot order.");
                Require(agent.MyStatuses.Contains(view) && notifications.SequenceEqual(new[] { "apply" }),
                    "Status agent membership/notification failed.");
                Require(ReferenceEquals(Live("view-start").TryGetParam("Status"), view)
                    && ReferenceEquals(Live("view-param").TryGetParam("Status"), view), "Status parameters were omitted.");
                Require(Live("view-master").mySource == sourceB && Live("view-master").myTarget == sourceA,
                    "Master cast did not use master -> owner.");
                var parameterPlayable = Live("view-param");
                Require(parameterPlayable.StatusStack == 1 && state.StackListeners == 2, "Stack applicator did not subscribe or initialize.");
                events.Clear(); state.Stack(3);
                Require(Plays().Length == 0 && parameterPlayable.StatusStack == 3, "Same range restarted visuals or failed live stack update.");
                state.Stack(5);
                Require(Plays().SequenceEqual(new[] { "play:view-begin-high", "play:view-stay-high" }), "Increasing stack chose wrong range transitions.");
                yield return null; Flush();
                Require(!instances.Any(value => value != null && value.Id == "view-stay-low" && value.gameObject.activeSelf),
                    "Prior range stay survived deferred stop.");
                events.Clear(); state.Stack(2);
                Require(Plays().Take(2).SequenceEqual(new[] { "play:view-end-high", "play:view-stay-low" }),
                    "Decreasing range did not play the previous end before the new stay.");
                yield return null; Flush(); yield return null; Flush();
                events.Clear(); state.Stack(3);
                Require(Plays().SequenceEqual(new[] { "play:view-stacked" }), "Finished stacked slot did not replay on the next actual stack update.");
                state.Invoke();
                Require(Live("view-invoke").mySource == sourceA && Live("view-invoke").myTarget == sourceA,
                    "Invoke slot did not use owner -> owner.");
                events.Clear(); state.Forbid(2, true);
                Require(Plays().SequenceEqual(new[] { "play:view-end-low" }), "Forbidden transition failed to end its stack range.");
                yield return null; Flush();
                Require(state.StackListeners == 1, "Stopped stack applicator retained its event subscription.");
                events.Clear(); state.Forbid(4, true); state.Forbid(2, false);
                Require(Plays().Length == 0, "Partial forbidden removal resumed a status.");
                state.Forbid(4, false);
                Require(Plays().Contains("play:view-start") && Plays().Contains("play:view-param"), "Full forbidden removal did not restore slots.");
                Require(state.StackListeners == 2 && Live("view-param").StatusStack == 3,
                    "Pooled stack applicator did not rebind current status.");
                yield return new WaitForSeconds(.08f); Capture("status-view-resumed");
                int oldCount = 0, newCount = 0;
                Action<MyFightStatus, int, int> count = (value, before, after) => { oldCount = before; newCount = after; };
                agent.OnStatusShowCustomCountUpdate += count;
                int lateCalls = 0; bool added = false;
                Action<MyFightStatus, int, int> late = (value, before, after) => lateCalls++;
                Action<MyFightStatus, int, int> addDuringNotify = (value, before, after) => {
                    if (!added) { added = true; agent.OnStatusShowCustomCountUpdate += late; }
                };
                agent.OnStatusShowCustomCountUpdate += addDuringNotify;
                view.IconShowCustomCount = 7;
                Require(oldCount == 0 && newCount == 7 && view.CanShowSpriteIcon(), "Custom count/icon metadata failed.");
                Require(lateCalls == 0, "Custom count invoked a newly subscribed observer within the current notification.");
                view.IconShowCustomCount = 8;
                Require(oldCount == 7 && newCount == 8 && lateCalls == 1, "Custom count observer version did not advance on the next notification.");
                agent.OnStatusShowCustomCountUpdate -= addDuringNotify;
                agent.OnStatusShowCustomCountUpdate -= late;
                view.ShowSpriteAbovePlayer = false;
                Require(!view.CanShowSpriteIcon(), "Icon visibility flag was ignored.");
                var assets = new HashSet<GameObject>(); view.TryGetPlayables(ref assets);
                Require(assets.Count == 11, "Playable dependency enumeration omitted a slot or range transition.");
                view.OnEnd(); yield return null; Flush();
                Require(!agent.MyStatuses.Contains(view) && notifications.Last() == "end" && state.Listeners == 0,
                    "End failed to unregister the view and component notifications.");
                events.Clear(); state.Stack(2); state.Invoke(); state.Forbid(2, true); state.Forbid(2, false);
                Require(Plays().Length == 0, "Ended status still responded to component events.");
                var rebound = new State(7);
                view.Bind(new Context { State = rebound, Owner = sourceA, Creator = sourceB,
                    Agent = agent, HasIcon = true }, manager);
                view.OnStart(false);
                Require(ReferenceEquals(parameterPlayable, Live("view-param"))
                    && Live("view-param").StatusStack == 7 && state.Listeners == 0 && rebound.StackListeners == 2,
                    "Pooled status view/applicator failed to switch to a different status component.");
                Require(!instances.Any(value => value != null && value.Id == "view-master" && value.gameObject.activeSelf),
                    "A status without a master replayed a previous master's slot.");
                view.OnEnd(); yield return null; Flush();
                Require(rebound.Listeners == 0, "Rebound status retained a component subscription.");
                agent.OnStatusEnd -= ended; agent.OnStatusShowCustomCountUpdate -= count;
                // Remove the first-view-specific apply listener by replacing the agent.
                UnityEngine.Object.Destroy(agent); yield return null;
                agent = sourceA.AddComponent<MyFightStatusAgent>();
                manager.RemoveAllPlayable(true); center.RemoveAll();

                // A source override changes only the configured side, retaining the owner target.
                var sourceState = new State(1);
                var sourceView = View(sourceState, agent, "creator", full: true, creator: true);
                sourceView.OnStart(true);
                Require(Live("creator-start").mySource == sourceB && Live("creator-start").myTarget == sourceA,
                    "Status source override did not retain the original creator.");
                sourceView.OnEnd(); yield return null; Flush();
                manager.RemoveAllPlayable(true); center.RemoveAll();

                // The display winner uses individual layers; range selection uses the supplied
                // shared count. These deliberately differ, so max/sum substitutions fail.
                var first = new State(7); var second = new State(2);
                agent.StatusToShareStack[42] = 3;
                var a = View(first, agent, "shared-A", 42);
                var b = View(second, agent, "shared-B", 42);
                a.OnStart(true); b.OnStart(true);
                Require(agent.MyDisplayFightStatusEachShareStatus[42] == a
                    && Live("shared-A-stay-low") != null
                    && !instances.Any(value => value != null && value.Id == "shared-B-stay-low" && value.gameObject.activeSelf),
                    "Shared display confused individual winner and shared range.");
                agent.StatusToShareStack[42] = 6; events.Clear(); second.Stack(8);
                yield return null; Flush();
                Require(agent.MyDisplayFightStatusEachShareStatus[42] == b && Live("shared-B-stay-high") != null,
                    "Higher individual count failed to take over the shared display.");
                Require(!instances.Any(value => value != null && value.Id == "shared-A-stay-low" && value.gameObject.activeSelf),
                    "Prior shared display was not stopped.");
                first.Stack(9); second.Stack(9);
                Require(agent.MyDisplayFightStatusEachShareStatus[42] == b, "Equal individual counts did not preserve the original calling-status tie behavior.");
                first.Stack(0); agent.StatusToShareStack[42] = 0; second.Stack(0);
                Require(!agent.MyDisplayFightStatusEachShareStatus.ContainsKey(42), "Zero individual stacks retained a shared display owner.");
                a.OnEnd(); b.OnEnd(); yield return null; Flush();
                Require(first.Listeners == 0 && second.Listeners == 0, "Shared views leaked component listeners.");

                // Non-stackable states omit both stack-only slots; an initially forbidden
                // status registers without playing until its last forbidden bit is cleared.
                var disabled = new State(1, false); disabled.Forbid(2, true);
                var d = View(disabled, agent, "disabled", full: true);
                events.Clear(); d.OnStart(true);
                Require(Plays().Length == 0 && agent.MyStatuses.Contains(d), "Initially forbidden status played or failed to register.");
                disabled.Forbid(2, false);
                Require(Plays().SequenceEqual(new[] { "play:disabled-start", "play:disabled-param", "play:disabled-master" }),
                    "Non-stackable status played stack-only slots or lost ordinary slots.");
                d.OnEnd(); yield return null; Flush();
                manager.RemoveAllPlayable(true); center.RemoveAll(); yield return null;
                Require(agent.MyStatuses.Count == 0 && disabled.Listeners == 0
                    && instances.All(value => value == null || !value.gameObject.activeInHierarchy), "Final lifecycle cleanup left active views, listeners or pooled effects.");
                Debug.Log("Original status view lifecycle passed: slot order, literal ranges, live stack parameters, actual forbidden-bit transitions, pool rebind, invoke, creator/master, shared winner/range/ties, dependencies, initially forbidden and complete unsubscribe.");
            }

            private sealed class Context : IFightStatusPlayableContext
            {
                public IFightStatusPlayableState State { get; set; }
                public GameObject Owner { get; set; }
                public GameObject Creator { get; set; }
                public GameObject Master { get; set; }
                public MyFightStatusAgent Agent { get; set; }
                public int? ShareId { get; set; }
                public bool HasIcon { get; set; }
            }

            private sealed class State : IFightStatusPlayableState
            {
                private readonly FightBattleUnitState owner = new FightBattleUnitState { Id = 1 };
                private readonly FightBattleStatusState data;
                public State(int stack, bool canStack = true)
                {
                    data = new FightBattleStatusState { StackCount = stack, IsOpenStack = canStack, MaxStackedCount = 100 };
                    owner.Statuses.Add(data);
                    typeof(FightBattleUnitState).GetField("StatusForbiddenChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(owner, new Action<FightBattleUnitState, FightBattleStatusState, bool>((unit, status, before)
                            => OnForbiddenChanged?.Invoke(this, before)));
                }
                public State(FightBattleUnitState owner, FightBattleStatusState data)
                { this.owner = owner; this.data = data; }
                public void ObserveExecutorStep()
                {
                    var field = typeof(FightBattleUnitState).GetField("StatusForbiddenChanged",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var original = (Action<FightBattleUnitState, FightBattleStatusState, bool>)field.GetValue(owner);
                    field.SetValue(owner, new Action<FightBattleUnitState, FightBattleStatusState, bool>((unit, status, before) =>
                    {
                        original?.Invoke(unit, status, before);
                        if (status == data) OnForbiddenChanged?.Invoke(this, before);
                    }));
                }
                public int StackedCount => data.StackCount;
                public bool CanStackCount => data.IsOpenStack;
                public bool Forbidden => data.ForbiddenType != 0;
                public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
                public event Action<IFightStatusPlayableState> OnInvoked;
                public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
                public int StackListeners => OnStatusStackUpdate?.GetInvocationList().Length ?? 0;
                public int Listeners => StackListeners + (OnInvoked?.GetInvocationList().Length ?? 0)
                    + (OnForbiddenChanged?.GetInvocationList().Length ?? 0);
                public void Stack(int value)
                {
                    int previous = data.StackCount;
                    FightStatusLifecycle.AddStack(data, value - previous);
                    OnStatusStackUpdate?.Invoke(this, previous, data.StackCount);
                }
                public void Invoke() => OnInvoked?.Invoke(this);
                public void Forbid(int type, bool value) => FightStatusLifecycle.SetForbidden(owner, data, type, value);
            }
        }

        public sealed partial class ProbePlayable : IChangePlayableStatusStackCount
        {
            public int StatusStack { get; private set; } = -1;
            public void SetPlayableStatusStackCount(int count) => StatusStack = count;
        }
    }
}
