using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zjcs.Official.PlayerPlay;

public static class PlayerPlayHarness
{
    private sealed class State : IPlayerPlayState
    {
        public PlayerPlayStateType Type { get; set; }
        public IPlayerPlayParam Param { get; set; }
    }
    private sealed class ActionValue : IPlayerPlayAction
    {
        public PlayerPlayActionType Type { get; set; }
        public IPlayerPlayParam Param { get; set; }
    }
    private sealed class Services : IPlayerPlayServices, IPlayerPlayStateSource
    {
        public bool Present = true;
        public readonly List<IPlayerPlayState> States = new List<IPlayerPlayState>();
        public readonly List<string> Events = new List<string>();
        public Func<IPlayerPlayState, PlayerPlayStateExitHandleBase> Factory;
        public IPlayerPlayStateSource MainPlayer => Present ? this : null;
        public IEnumerable<IPlayerPlayState> GetStates() => States;
        public PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState s, IPlayerPlayAction a,
            IPlayerPlayActionEnv e) => Factory == null ? null : Factory(s);
        public void Notify(PlayerPlayText text) => Events.Add("notify:" + text.Key);
        public void Error(string message) => Events.Add("error");
    }
    private sealed class Handle : PlayerPlayStateExitHandleBase
    {
        private readonly Func<Task<bool>> exit;
        public Handle(IPlayerPlayState state, Func<Task<bool>> exit) : base(state, null, null) { this.exit = exit; }
        public override PlayerPlayText GetHardMutexText() => new PlayerPlayText("specific");
        public override Task<bool> Exit() => exit();
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static IPlayerPlayState Match(bool done = false) => new State
        { Type = PlayerPlayStateType.GameMatch, Param = new PlayerPlayMatchStateParam(done) };
    private static IPlayerPlayState Chat() => new State
        { Type = PlayerPlayStateType.ChatAction, Param = new PlayerPlayActionNoneParam() };
    private static IPlayerPlayAction Fight(int flag = 0) => new ActionValue
        { Type = PlayerPlayActionType.Fight, Param = new PlayerPlayFightStateParam(flag) };
    private static PlayerPlayStateMutexType Mutex(IPlayerPlayAction action, params IPlayerPlayState[] states) =>
        PlayerPlayStateMutexConfig.GetMutexType(action, states);

    private static void CheckFlagsAndMutex()
    {
        int lookups = 0;
        var action = new PlayerPlayFightAction(931, id => {
            Require(id == 931, "Stage parser receives exact stage ID"); lookups++;
            return new PlayerPlayStageInfo(false, true, true, true, false);
        }, true, GameFastType.FastKill);
        Require(lookups == 1 && ((PlayerPlayFightStateParam)action.Param).Flag == 23,
            "Join-in-middle sets team bit; leave requires both phases; background/fast are independent");
        Require(PlayerPlayUtil.StageInfo2Param(new PlayerPlayStageInfo(true, false, false, true, true),
            false, false).Flag == 9, "Team + both leave phases");
        Require(Mutex(Fight(), Match()) == PlayerPlayStateMutexType.SoftMutex, "Foreground match exits first");
        Require(Mutex(Fight(8), Match()) == PlayerPlayStateMutexType.NotMutex, "Original leave flag exception");
        Require(Mutex(Fight(), Match(true)) == PlayerPlayStateMutexType.HardMutex, "Matched game blocks foreground fight");
        Require(Mutex(Fight(4), Match(true)) == PlayerPlayStateMutexType.NotMutex, "Background fight exception");
        Require(Mutex(Fight(16), Match(true)) == PlayerPlayStateMutexType.NotMutex, "Fast fight exception");
        for (int flag = 0; flag <= 31; flag++)
            Require(Mutex(Fight(flag), Chat()) == PlayerPlayStateMutexType.HardMutex, "Chat blocks every fight flag");
        Require(Mutex(Fight(), Match(), Chat()) == PlayerPlayStateMutexType.HardMutex, "Hard wins over soft");
        Require(PlayerPlayStateMutexConfig.FilterStateByMutexType(PlayerPlayStateMutexType.SoftMutex,
            Fight(), new[] { Match(), Chat(), Match() }).Count() == 2, "Filter preserves matching states");
    }

    private static async Task CheckGate()
    {
        var s = new Services { Present = false };
        var gate = new PlayerPlayStateModule(s);
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(s.Events.Count == 0, "No player cannot start");
        s.Present = true;
        await gate.DoAction(Fight(), () => { s.Events.Add("start"); s.Events.Add("close"); });
        Require(string.Join(",", s.Events) == "start,close", "Free action runs callback in caller order");
        s.Events.Clear(); s.States.Add(Chat());
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(string.Join(",", s.Events) == "notify:ui_play_state_hard_mutex", "Generic hard notice, no callback");
        s.Factory = state => new Handle(state, () => throw new Exception("Must not exit hard state"));
        s.Events.Clear();
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(string.Join(",", s.Events) == "notify:specific", "Hard notice uses first available original handler");

        s.Events.Clear(); s.States.Clear(); s.States.Add(Match());
        var pending = new TaskCompletionSource<bool>();
        s.Factory = state => new Handle(state, () => { s.Events.Add("exit"); return pending.Task; });
        Task operation = gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(!operation.IsCompleted && string.Join(",", s.Events) == "exit", "Wait for actual exit result");
        pending.SetResult(false); await operation;
        Require(string.Join(",", s.Events) == "exit", "Cancelled exit never starts fight");

        s.Events.Clear();
        s.Factory = state => new Handle(state, () => {
            s.Events.Add("exit"); s.States.Clear(); s.States.Add(Chat()); return Task.FromResult(true);
        });
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(string.Join(",", s.Events) == "exit,notify:specific", "Recheck newly introduced hard mutex after exit");

        s.Events.Clear(); s.States.Clear(); s.States.Add(Match());
        s.Factory = state => new Handle(state, () => {
            s.Events.Add("exit"); return Task.FromResult(true);
        });
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(s.Events.Count(x => x == "exit") == 10 && s.Events.Last() == "error" &&
            !s.Events.Contains("start"), "Unchanged soft state stops after original ten attempts");

        s.Events.Clear();
        int exits = 0;
        s.Factory = state => new Handle(state, () => {
            exits++; if (exits == 10) s.States.Clear(); return Task.FromResult(true);
        });
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(exits == 10 && string.Join(",", s.Events) == "error",
            "Clearing on final allowed exit still preserves original nonconvergence boundary");

        s.Events.Clear(); s.States.Add(Match());
        s.Factory = state => new Handle(state, () => {
            s.Events.Add("exit"); s.States.Clear(); return Task.FromResult(true);
        });
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(string.Join(",", s.Events) == "exit,start", "Successful exit precedes callback exactly once");

        s.Events.Clear(); s.States.Add(Match()); s.Factory = state => null;
        await gate.DoAction(Fight(), () => s.Events.Add("start"));
        Require(string.Join(",", s.Events) == "start", "Original no-handler soft branch is preserved");
        s.Events.Clear();
        s.Factory = state => new Handle(state, () => Task.FromException<bool>(new InvalidOperationException("exit failed")));
        bool failed = false;
        try { await gate.DoAction(Fight(), () => s.Events.Add("start")); }
        catch (InvalidOperationException) { failed = true; }
        Require(failed && s.Events.Count == 0, "Failed exit propagates and never starts callback");
    }

    public static int Main()
    {
        CheckFlagsAndMutex(); CheckGate().GetAwaiter().GetResult();
        Console.WriteLine("Player-play rules passed: stage flags, mutex priority, async cancellation, recheck, ten-loop limit and callback order.");
        return 0;
    }
}
