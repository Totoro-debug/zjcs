#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Offline.HomeEnvironmentTime;
using Zjcs.Presentation.Home;

public sealed partial class HomeIncomeFormulaHostRunner
{
    IEnumerator CheckNaturalTimeUnlock()
    {
        Require(home.Profile.Save.Player.Level == 3 && home.Profile.StoryCount("FirstBattle") == 0
            && ActualEnvironment.Hour == 12 && ActualEnvironment.Minute == 0,
            "actual low-level home uses the original fixed noon before natural-time unlock");
        Capture("home-environment-locked-noon.png");
        long experience = home.Profile.Save.Player.Experience;
        var character = Zjcs.Offline.Progression.OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(home.Profile.ApplyCharacterMutation(player => {
            player.Level = 10; player.Experience = character.CumulativeLevelExperience[10];
        }, "environment:level-unlock").IsApplied, "commit original natural-time level branch");
        yield return null; yield return null;
        var state = (HomeEnvironmentTimeState)typeof(HomeEnvironmentView)
            .GetField("timeState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ActualEnvironment);
        Require(state.StageHomeDayTime.CompareTo(state.HomeDayTime) == 0,
            "actual level notification releases the fixed-noon handler into natural time");
        Require(home.Profile.ApplyCharacterMutation(player => {
            player.Level = 3; player.Experience = experience;
        }, "environment:restore-level").IsApplied, "restore isolated fixture level");
        home.Close(); CloseCapture(); yield return null; Open(); yield return new WaitForSeconds(1);
        Require(ActualEnvironment.Hour == 12 && ActualEnvironment.Minute == 0,
            "reopening a still-limited fixture reinstates original fixed noon");
        Require(home.Profile.SetStoryProgress("FirstBattle", 1, 2, "environment:story-unlock").IsApplied,
            "commit original natural-time story branch");
        yield return null; yield return null;
        state = (HomeEnvironmentTimeState)typeof(HomeEnvironmentView)
            .GetField("timeState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ActualEnvironment);
        Require(home.Profile.Save.Player.Level == 3 && state.StageHomeDayTime.CompareTo(state.HomeDayTime) == 0,
            "actual story notification independently releases fixed noon without a level change");
        Require(home.Profile.SetStoryProgress("FirstBattle", 0, 0, "environment:restore-story").IsApplied,
            "restore isolated fixture story");
        home.Close(); CloseCapture(); yield return null; Open(); yield return new WaitForSeconds(1);
        Debug.Log("HOME_NATURAL_TIME_UNLOCK_PASSED: actual noon, selected level and story branches, notifications and reopen.");
    }

    sealed class NaturalTimeGate : IHomeNaturalTimeLimit
    {
        public bool IsLimited { get; set; } = true;
        public Action Changed;
        public void Register(Action callback) { Changed += callback; }
        public void Deregister(Action callback) { Changed -= callback; }
    }
    HomeEnvironmentView ActualEnvironment => (HomeEnvironmentView)typeof(HomeFeatureRuntime)
        .GetField("environment", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home);

    void CheckFastEnvironmentRules()
    {
        var config = OfflineHomeEnvironmentConfiguration.Load();
        Require(config.HomeTimeScale == 30 && config.FastCycleDays == 1 && config.FastCycleDaySeconds == 2.5f
            && config.FastCycleCurve.keys.Length == 2 && config.FastCycleCurve.keys[0].outTangent == .3046025037765503f
            && config.FastCycleCurve.keys[1].inTangent == 0 && config.FastCycleCurve.keys[1].value == 1,
            "selected complete curve and fast-cycle timing match the actual original configuration");
        var date = new HomeDateTime(60, 60, 24, 30, 12, 360 * 86400.0 - .25);
        date.Update(.5f);
        Require(date.Year == 1 && date.Month == 0 && date.Day == 0
            && Math.Abs(date.TotalSeconds - (360 * 86400.0 + .25)) < .000001,
            "original calendar carries subsecond time through day, month and year");
        var gate = new NaturalTimeGate();
        using (var time = new HomeEnvironmentTimeState(config, 11 * 3600, gate))
        {
            time.Update(.1f); time.FastHangUp(); time.Update(.1f);
            Require(time.StageHomeDayTime.Hour == 12 && !time.StageHandleProxy.InFasting
                && Math.Abs(time.PlayableDeltaTime - .1f) < .000001 && gate.Changed != null,
                "fixed noon keeps natural delta and queues fast time below its noninterruptible original handler");
            gate.IsLimited = false; gate.Changed();
            Require(gate.Changed == null, "original unlock callback deregisters before the handler is popped");
            time.Update(.1f);
            Require(time.StageHandleProxy.InFasting, "the original queued fast handler becomes active after noon unlock");
            time.Dispose();
        }
        gate = new NaturalTimeGate();
        using (var time = new HomeEnvironmentTimeState(config, 0, gate))
        {
            time.Update(.1f); time.Dispose();
            Require(gate.Changed == null, "closing a locked home deregisters the original condition callback");
        }
        using (var time = new HomeEnvironmentTimeState(config, 19 * 3600 + 59 * 60 + 30))
        {
            int calls = 0;
            time.HomeClock.AddClock(new HomeDayTime { Hour = 20 }, _ => calls++);
            time.Update(1);
            Require(calls == 0, "original clock excludes an event exactly at its new endpoint");
            time.Update(1f / 30);
            Require(calls == 1, "original clock invokes the crossed endpoint on the following interval");
        }
        using (var time = new HomeEnvironmentTimeState(config, 11 * 3600))
        {
            time.FastHangUp(); time.FastHangUp();
            time.Update(.1f);
            Require(time.StageHandleProxy.InFasting && time.PlayableDeltaTime > .1f
                && time.StageHomeDayTime.CompareTo(time.HomeDayTime) != 0
                && time.HomeDateTime.TotalDays == 0,
                "visual fast time leaves the natural calendar independent");
            for (int i = 1; i < 25; i++) time.Update(.1f);
            Require(time.StageHandleProxy.InFasting, "a second fast handler queues below the noninterruptible original");
            for (int i = 0; i < 25; i++) time.Update(.1f);
            Require(!time.StageHandleProxy.InFasting && time.StageHandleProxy.HasHandler
                && time.HomeDateTime.TotalSeconds == 11 * 3600 + 150
                && time.StageHomeDayTime.GetSecondsSinceStartOfDay(config) == 11 * 3600,
                "original completion pops before reading the underlying natural handler on that frame");
            time.Update(.1f);
            Require(time.StageHomeDayTime.CompareTo(time.HomeDayTime) == 0 && time.HomeRuntimeSeconds == 153,
                "the following frame refreshes the natural handler without adding either visual day to its calendar");
            time.FastHangUp(); time.Dispose();
            Require(!time.StageHandleProxy.HasHandler, "teardown clears queued and active original stage handlers");
        }
        using (var time = new HomeEnvironmentTimeState(config, 19 * 3600 + 59 * 60 + 30))
        {
            int played = 0, randomCalls = 0;
            var meteor = new HomeMeteorSchedule(time, (start, end) => { randomCalls++; return start; },
                index => { Require(index == 0, "selected meteor template index"); played++; });
            time.Update(1); meteor.Update();
            time.Update(19); meteor.Update();
            Require(played == 0, "entering the original night window schedules 600 natural home seconds ahead");
            time.Update(1); meteor.Update();
            Require(played == 1 && randomCalls == 2, "meteor probability and template selection run at the scheduled stage time");
            config.MeteorConfig.Probability = -1;
            time.Update(20); meteor.Update();
            time.Update(.1f); meteor.Update();
            Require(played == 1 && randomCalls == 4, "original failed probability retries without rescheduling");
            config.MeteorConfig.Probability = 1;
            meteor.Update();
            Require(played == 2 && randomCalls == 6, "successful retry alone chooses a template and schedules again");
        }
        using (var time = new HomeEnvironmentTimeState(config, 23 * 3600 + 59 * 60 + 50))
        {
            int played = 0;
            var meteor = new HomeMeteorSchedule(time, (start, end) => start, _ => played++);
            meteor.Update();
            Require(played == 1, "original first-update branch compares the wrapped next time against the current stage");
            time.Update(1); meteor.Update();
            Require(played == 1 && time.HomeDateTime.TotalDays == 1,
                "original midnight-wrap branch preserves the next scheduled meteor without an extra immediate play");
        }
    }
}
#endif
