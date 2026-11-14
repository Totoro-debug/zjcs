using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeExperienceRewards;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeExperienceProfileValidation
{
    const string Key = "Zjcs.HomeExperienceProfile.Active";
    static HomeExperienceProfileValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("ActualHomeExperienceFixture").AddComponent<HomeExperienceProfileRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Actual home experience validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error == null) Debug.Log("HOME_EXPERIENCE_PROFILE_PASSED: selected 884 table boundaries, real saved XP, original native arc/reward sequence, pending awards, storage, blessing, disposal and reopen. Explicit controlled season/function/property context; formal HomeFeatureRuntime not under test.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeExperienceProfileRunner : MonoBehaviour
{
    OfflineHomeExperienceServices services; OfflineHomeExperiencePlayer player; GameObject widget;
    Camera cameraUi; RenderTexture target; HomePlayerExpSlider exp; Slider slider;
    int errors; string work; HomeExperienceTables tables; OfflineMainlineProgressSession profile;
    readonly List<int> notifications = new List<int>();
    IEnumerator Start()
    {
        AudioListener.volume = 0; work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var steps = Check();
        for (;;) {
            object current = null; bool next;
            try { next = steps.MoveNext(); if (next) current = steps.Current; }
            catch (Exception error) { Cleanup(); HomeExperienceProfileValidation.Finish(error); yield break; }
            if (!next) break; yield return current;
        }
        Cleanup(); HomeExperienceProfileValidation.Finish(errors == 0 ? null : new Exception("Unexpected errors: " + errors));
    }
    void Cleanup() { if (widget) widget.SetActive(false); services?.Dispose(); player?.Dispose(); }
    IEnumerator Check()
    {
        var config = OfflineHomeExperienceConfiguration.LoadDefault(); tables = config.CreateTables(); CheckRules(config);
        string root = Path.Combine(work, "experience-profile-" + Guid.NewGuid().ToString("N"));
        var creation = CharacterCreationOfflineService.OpenDefault(root); string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "经验验证";
        Require(creation.TryCreateCharacter(slot, draft, out string error), error);
        var slots = new ProfileSlotCatalog(root); profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var environment = new OfflineHomeExperienceEnvironment { PlanesRank = Rank.Norank };
        player = new OfflineHomeExperiencePlayer(profile, tables, () => environment);
        Require(profile.Save.Player.SubRank == (int)SubRank.Norank && profile.Save.Player.Rank == (int)Rank.Norank,
            "new character persists the approved original beginner rank");
        Require(player.Level == 1 && player.LevelLimit == 10, "new character XP resolves through the original beginner level cap");
        var fresh = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(fresh.Save.Player.SubRank == 1 && fresh.Save.Player.Rank == 1 && fresh.Save.Player.Experience == 0,
            "beginner rank and unchanged initial XP survive disk reopen");
        // Preserve the existing canonical-growth scenario below; this no longer
        // supplies a missing rank to the new character.
        Mutate(saved => saved.Level = 1);
        player.SynchronizeCommittedPlayer();
        var store = new OfflineHomeExperienceStore(player); var blockers = new Blockers();
        Require(store.Inited && store.HandledExp == 0 && store.HandledLevel == 1, "original ConsoleDataManager.Init snapshots the new character");
        services = new OfflineHomeExperienceServices(player, store, blockers, previous => notifications.Add(previous), value => value.ToString(), rank => "season-" + rank, subRank => { });
        SetupWidget(); yield return null; yield return null;
        Require(exp.Level == 1 && player.Level == 1, "native widget starts from actual new-game experience");
        var events = new List<string>(); player.OnLevelUpdate += level => events.Add("level:" + level);
        player.OnItemCountUpdate += (item, previous, source) => events.Add("xp:" + previous);
        var ranges = new List<string>(); services.Handler.OnPlayerExpUpdated.AddListener((before, after) => ranges.Add(before + ":" + after));
        Mutate(saved => saved.Experience = 120); string committed = File.ReadAllText(slots.GetProgressPath(slot));
        player.SynchronizeCommittedPlayer(); player.SynchronizeCommittedPlayer();
        Require(events.SequenceEqual(new[] { "level:2", "xp:0" }), "committed profile projection reports original level-before-item order once");
        Require(committed == File.ReadAllText(slots.GetProgressPath(slot)) && profile.Save.Player.Level == 1, "reading HUD projection never rewrites canonical growth or saved XP");
        yield return new WaitForSeconds(1.3f);
        Require(exp.Level == 2 && player.Level == 2 && services.Handler.HandledExp == 120 && ranges.SequenceEqual(new[] { "0:120" }), "real saved XP drives original native animation and reward range");
        CheckArc(120); Capture("home-experience-profile-normal.png");
        Require(notifications.SequenceEqual(new[] { 1 }) && blockers.Active, "original notification receives previous level after arc finishes");
        yield return new WaitForSeconds(2.3f); Require(!blockers.Active, "original notification interval releases rating blocker");
        widget.SetActive(false); Mutate(saved => saved.Experience = 200); player.SynchronizeCommittedPlayer(); yield return new WaitForSeconds(.1f);
        Require(services.Handler.HandledExp == 120, "hidden native widget leaves next real reward range pending");
        widget.SetActive(true); yield return new WaitForSeconds(1.3f);
        Require(exp.Level == 3 && services.Handler.HandledExp == 200, "reopening native widget consumes actual pending XP");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        using (var restored = new OfflineHomeExperiencePlayer(reopened, tables, () => environment))
            Require(restored.Level == 3 && restored.GetItemAmount(50) == 200, "actual XP and projected level survive disk reopen");
        yield return new WaitForSeconds(2.3f);
        long capExp = tables.LevelExpParser.GetExp(1, 10);
        Mutate(saved => { saved.Experience = capExp + 10; saved.Level = 10; saved.SeasonBaseLevel = 10; });
        player.SynchronizeCommittedPlayer(); yield return new WaitForSeconds(1.3f);
        Require(player.Level == 10 && player.ShowPlayerStoreExpLimit() && player.GetMaxStoreExpLevel() == 20, "stored experience uses actual current and next subrank caps without requiring a season record");
        Require(slider.minValue == capExp && slider.maxValue == tables.LevelExpParser.GetExp(1, 20), "native storage arc spans the next rank cap");
        Capture("home-experience-profile-storage.png");
        yield return new WaitForSeconds(2.3f);
        environment.PlayerExpLimitFunctionLimited = true; player.SynchronizeCommittedPlayer(); widget.SetActive(false); widget.SetActive(true); yield return null;
        Require(!player.ShowPlayerStoreExpLimit() && slider.maxValue == tables.LevelExpParser.GetExp(1, 11), "original function 173 limitation disables storage branch");
        environment.PlayerExpLimitFunctionLimited = false;
        environment.PlanesRank = Rank.Silver; environment.ServerRecordSeason = 1;
        environment.CurrentRecordData = new HomeExperienceSeasonRecord { Season = 1, RecordRank = Rank.Silver };
        long blessFirst = tables.LevelBlessInfoParser.Get(Rank.Silver, 1).Exp;
        Mutate(saved => { saved.SubRank = (int)SubRank.Silver3; saved.Rank = (int)Rank.Silver; saved.Experience = tables.LevelExpParser.GetExp(1, 100);
            saved.Level = 100; saved.SeasonBaseLevel = 100; saved.SeasonRank = (int)Rank.Silver; saved.HasActiveSeasonRecord = true; saved.BlessingExperience = blessFirst / 2; });
        player.SynchronizeCommittedPlayer(); yield return new WaitForSeconds(1.3f);
        Require(player.IsBlessLevelOpen() && player.Level == 100 && player.GetCurBlessPlayerLevel() == 0, "original blessing opens only with real current record and matching world top subrank");
        Require(slider.minValue == 0 && slider.maxValue == blessFirst, "native blessing arc uses selected 884 threshold");
        Capture("home-experience-profile-blessing.png"); yield return new WaitForSeconds(2.3f);
        var blessOrder = new List<string>(); player.OnLevelUpdate += _ => blessOrder.Add("level");
        player.OnBlessLevelUp += (before, after) => blessOrder.Add("bless-level"); player.OnBlessExpUpdate += (before, after) => blessOrder.Add("exp:" + before + ":" + after);
        long ordinary = player.GetItemAmount(50), oldBless = player.GetCurBlessPlayerExp();
        Mutate(saved => saved.BlessingExperience = blessFirst + 100); player.SynchronizeCommittedPlayer(); yield return new WaitForSeconds(1.3f);
        Require(player.Level == 101 && exp.Level == 101 && blessOrder.SequenceEqual(new[] { "level", "bless-level", "level", "exp:" + (ordinary + oldBless) + ":" + (ordinary + blessFirst + 100) }), "original refresh and blessing level callbacks precede combined XP event, including repeated Level notification");
        Require(slider.minValue == blessFirst && slider.maxValue == tables.LevelBlessInfoParser.Get(Rank.Silver, 2).Exp, "native arc advances to the second blessing threshold");
        using (var restored = new OfflineHomeExperiencePlayer(OfflineMainlineProgressSession.OpenCharacter(slots, slot), tables, () => environment))
            Require(restored.Level == 101 && restored.GetCurBlessPlayerExp() == blessFirst + 100, "real saved Silver plane-rank key resolves to season 1 on reopen");
        Capture("home-experience-profile-blessing-level.png");
        widget.SetActive(false); services.Dispose(); services.Dispose(); Require(!blockers.Active && blockers.Added == blockers.Removed, "scope disposal cancels native reward wait and releases blockers");
        player.Dispose(); bool rejected = false; try { _ = player.Level; } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "disposed profile projection cannot be reused");
        rejected = false; try { _ = player.GetItemAmount(1); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "disposed projection rejects other inventory reads too");
    }
    void CheckRules(OfflineHomeExperienceConfiguration config)
    {
        int checkedBoundaries = 0;
        foreach (var row in config.Rows<LevelExp>("level_exp")) {
            Require(tables.LevelExpParser.GetLevelsCount(row.Id) == row.Exp.Count && tables.LevelExpParser.GetLevelMax(row.Id) == row.Exp.Count - 1, "selected level table bounds");
            foreach (long threshold in row.Exp.Distinct()) foreach (long value in new[] { threshold - 1, threshold, threshold + 1 }) {
                int expected = Math.Max(0, row.Exp.FindLastIndex(item => item <= value));
                Require(tables.LevelExpParser.CalcLevelByExp(row.Id, value) == expected, "original binary search exact/below/above threshold"); checkedBoundaries++;
            }
        }
        Require(tables.LevelExpParser.GetExp(999, 0) == long.MaxValue && tables.LevelExpParser.GetLevelMin(999) == int.MaxValue
            && tables.LevelExpParser.GetExp(1, -1) == 0 && tables.LevelExpParser.GetExp(1, 252) == long.MaxValue, "original absent/out-of-range sentinels");
        Require(tables.AstrologicalSeasonConfigParser.GetSeasonByRank(Rank.Silver) == 1 && tables.AstrologicalSeasonConfigParser.GetSeasonByRank(Rank.Norank) == -1,
            "selected original rank-to-season mapping preserves absence sentinel");
        foreach (var group in config.Rows<LevelBlessInfo>("level_bless").GroupBy(row => row.Rank)) {
            var sorted = group.OrderBy(row => row.BlessLevel).ToArray();
            foreach (var row in sorted) foreach (long value in new[] { row.Exp - 1, row.Exp, row.Exp + 1 }) {
                int expected = sorted.Where(item => item.Exp <= value).Select(item => item.BlessLevel).DefaultIfEmpty(0).Max();
                Require(tables.LevelBlessInfoParser.GetBlessLevel(group.Key, value) == expected, "original blessing search thresholds"); checkedBoundaries++;
            }
        }
        foreach (var group in config.Rows<SystemLevelLimitInfo>("system_level_limit").Where(row => row.PlayerBlessLevel == 0).GroupBy(row => row.SubRank))
            Require(tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(group.Key) == group.Max(row => row.PlayerLevel), "original limit parser takes maximum base level");
        var query = new HomeExperiencePlayer(tables) { SubRank = SubRank.Silver3, Level = 100, BaseLevel = 100, PlanesRank = Rank.Silver, ServerRecordSeason = 1 };
        Require(!query.IsBlessLevelOpen(), "no fabricated blessing record"); query.BlessRecords.Add(1, new HomeExperienceBlessRecord());
        Require(query.IsBlessLevelOpen(), "original matching blessing prerequisites"); query.PlanesRank = Rank.Gold;
        Require(!query.IsBlessLevelOpen(), "world rank is independent from player rank");
        query.PlanesRank = Rank.Silver; query.CurrentRecordData = new HomeExperienceSeasonRecord { Season = 1, RecordRank = Rank.Gold };
        Require(query.PlayerExpLimit(), "storage reads current record rank before world rank"); query.CurrentRecordData.RecordRank = Rank.Silver;
        Require(!query.PlayerExpLimit(), "current season top subrank disables storage");
        Require(query.GetExpRequestByBlessLevel(0) == 0 && query.GetExpRequestByBlessLevel(int.MaxValue) == query.GetMaxBlessExp(Rank.Silver), "original missing next blessing threshold falls back to maximum");
        Debug.Log("HOME_EXPERIENCE_RULE_BOUNDARIES=" + checkedBoundaries);
    }
    void SetupWidget()
    {
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("ExperienceCamera", typeof(Camera)).GetComponent<Camera>(); cameraUi.orthographic = true;
        cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(.12f, .2f, .28f);
        target = new RenderTexture(608, 1080, 24); cameraUi.targetTexture = target;
        var root = new GameObject("NativeExperienceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var staging = new GameObject("InactiveExperienceStaging"); staging.SetActive(false);
        var original = OfficialHomeAvatarAssetCatalog.LoadDefault().prefab.GetComponentInChildren<HomePlayerExpSlider>(true);
        widget = Object.Instantiate(original.gameObject, staging.transform, false); widget.SetActive(false);
        exp = widget.GetComponent<HomePlayerExpSlider>(); exp.Configure(services);
        widget.transform.SetParent(canvas.transform, false); widget.SetActive(true); Object.Destroy(staging);
        slider = widget.GetComponentInChildren<Slider>(true);
    }
    void CheckArc(long value)
    {
        float min = (float)typeof(HomePlayerExpSlider).GetField("mSliderMinValue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exp);
        float max = (float)typeof(HomePlayerExpSlider).GetField("mSliderMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exp);
        float expected = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Lerp(min, max, (value - slider.minValue) / (slider.maxValue - slider.minValue)));
        Require(Math.Abs(slider.value - expected) < .1f, "native original configured arc limits remain intact");
    }
    void Mutate(Action<OfflinePlayerProgress> mutation)
    {
        var result = profile.ApplyCharacterMutation(mutation, "experience:" + Guid.NewGuid().ToString("N")); Require(result.IsApplied, result.Message);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(work, name), texture.EncodeToPNG()); RenderTexture.active = previous; Object.Destroy(texture);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Blockers : IHomeExperienceCombatRatingBlockers
    {
        public bool Active; public int Added, Removed;
        public void RegisterBlocker(HomeExperienceRewardHandler handler) { Active = true; Added++; }
        public void UnregisterBlocker(HomeExperienceRewardHandler handler) { Active = false; Removed++; }
    }
}
