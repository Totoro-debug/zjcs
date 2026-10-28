using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.World.NativeMap12Labels;
using Zjcs.Official.WorldUI;

public static class Map12SteleContextHarness
{
    private sealed class State : IPlayerPlayState
    {
        public PlayerPlayStateType Type => PlayerPlayStateType.ChatAction;
        public IPlayerPlayParam Param => new PlayerPlayActionNoneParam();
    }
    private sealed class Services : IWorldBattleSteleServices, IPlayerPlayServices, IPlayerPlayStateSource
    {
        public int SubRank, PlayerReads, RewardLevel, RewardRank, StageLookup;
        public long Rating;
        public int? Members;
        public bool Hard;
        public int[] RewardRules;
        public WorldBattleSteleTeamTarget Target;
        public PlayerPlayStageInfo StageInfo;
        public readonly List<string> Trace = new List<string>();
        public int PlayerSubRank { get { PlayerReads++; return SubRank; } }
        public long PlayerCombatRating { get { PlayerReads++; return Rating; } }
        public int? TeamMembers => Members;
        public event Action TeamPlayerAdded, TeamPlayerRemoved, TeamUpdated;
        public PlayerPlayStateModule PlayStateModule { get; }
        public Services() { PlayStateModule = new PlayerPlayStateModule(this); }
        public PlayerPlayStageInfo GetStageInfo(int id) { StageLookup = id; return StageInfo; }
        public string Compact(long value) => value.ToString();
        public void OpenTeam(WorldBattleSteleTeamTarget target) { Target = target; }
        public void Confirm(string key, Action accepted) { Trace.Add("confirm:" + key); }
        public void Notify(string key) { Trace.Add("notice:" + key); }
        public void PopulateRewardPreview(Transform slot, int[] rules, int level, int rank)
        { RewardRules = rules; RewardLevel = level; RewardRank = rank; }
        public void Emit() { TeamPlayerAdded?.Invoke(); TeamPlayerRemoved?.Invoke(); TeamUpdated?.Invoke(); }
        public IPlayerPlayStateSource MainPlayer => this;
        public IEnumerable<IPlayerPlayState> GetStates() => Hard ? new IPlayerPlayState[] { new State() } : Array.Empty<IPlayerPlayState>();
        public PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState state, IPlayerPlayAction action,
            IPlayerPlayActionEnv env) => null;
        public void Notify(PlayerPlayText text) { Trace.Add("notice:" + text.Key); }
        public void Error(string message) { throw new Exception(message); }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Same(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    private static int Int(JsonElement value, string key) => value.GetProperty(key).GetInt32();

    private sealed class InviteServices : IWorldTeamInviteServices, IWorldTeamInviteTarget
    {
        public readonly List<string> Trace = new List<string>();
        public int? Count;
        public bool Leader = true, MemberInvite, LimitTip;
        public TaskCompletionSource<WorldTeamInviteResult> Pending;
        public WorldTeamInviteActor Actor;
        public IWorldTeamInviteTarget Target;
        public int? TeamPlayerCount => Count;
        public bool IsLeader { get { Trace.Add("leader"); return Leader; } }
        public bool AllowMemberInvite { get { Trace.Add("permission"); return MemberInvite; } }
        public Task<WorldTeamInviteResult> OperPlayerTeamInvite(WorldTeamInviteActor actor,
            IWorldTeamInviteTarget target)
        {
            Trace.Add("invite"); Actor = actor; Target = target;
            return Pending == null ? Task.FromResult(new WorldTeamInviteResult(0, false)) : Pending.Task;
        }
        public string NpcName(int id) { Trace.Add("npc:" + id); return "原NPC名"; }
        public string Format(string key, params object[] args) => key + ":" + string.Join(",", args);
        public void SystemMessage(string text) { Trace.Add("message:" + text); }
        public void ShowError(int code, bool defaultTip) { Trace.Add("error:" + code + ":" + defaultTip); }
        public int GetTeamMaxPlayer() { Trace.Add("max"); return 4; }
        public bool TryGetLimitErrorTip(bool isSelf, int code, out string tip)
        { Trace.Add("limit:" + isSelf + ":" + code); tip = "original-limit"; return LimitTip; }
    }

    private static async Task CheckInviter()
    {
        var actor = new WorldTeamInviteActor { TargetId = 9007199254740993L,
            OwnerPlayerId = 9007199254740991L, NpcClassId = 123, IsNpc = true };
        var s = new InviteServices { Count = 4, Leader = false };
        Require(await WorldTeamInviter.Execute(s, actor, s) == 6102 &&
            string.Join("|", s.Trace) == "max|error:6102:True", "Full team stops before permission or operation");
        s = new InviteServices { Count = 3, Leader = false };
        Require(await WorldTeamInviter.Execute(s, actor, s) == 6168 &&
            string.Join("|", s.Trace) == "max|leader|permission|error:6168:False", "Original member permission error flag");
        s = new InviteServices { Count = null };
        Require(await WorldTeamInviter.Execute(s, actor, null) == 0 &&
            string.Join("|", s.Trace) == "leader|invite|npc:123|message:TeamOperBoradcast.JoinMsg:原NPC名" &&
            ReferenceEquals(s.Actor, actor) && s.Actor.TargetId == 9007199254740993L && s.Target == null,
            "No-team/null-target branch, original NPC message, exact identity, RPC bool is not success condition");
        s = new InviteServices { Count = 1, Leader = false, MemberInvite = true,
            Pending = new TaskCompletionSource<WorldTeamInviteResult>() };
        Task<int> operation = WorldTeamInviter.Execute(s, actor, s);
        Require(!operation.IsCompleted && string.Join("|", s.Trace) == "max|leader|permission|invite",
            "No success message or target limit query before operation completes");
        s.LimitTip = true;
        s.Pending.SetResult(new WorldTeamInviteResult(777, true));
        Require(await operation == 777 && string.Join("|", s.Trace).EndsWith("limit:False:777|message:original-limit"),
            "Error code wins over result bool and uses non-self target tip after await");
        s = new InviteServices { Pending = new TaskCompletionSource<WorldTeamInviteResult>() };
        s.Pending.SetResult(new WorldTeamInviteResult(778, true));
        Require(await WorldTeamInviter.Execute(s, actor, s) == 778 &&
            string.Join("|", s.Trace).EndsWith("limit:False:778|error:778:True"), "Unmapped target error fallback");
        s = new InviteServices { Pending = new TaskCompletionSource<WorldTeamInviteResult>() };
        s.Pending.SetResult(new WorldTeamInviteResult(779, false));
        Require(await WorldTeamInviter.Execute(s, actor, null) == 779 &&
            string.Join("|", s.Trace) == "leader|invite|error:779:True", "Null-target error fallback");
        s = new InviteServices();
        actor.IsNpc = false;
        Require(await WorldTeamInviter.Execute(s, actor, s) == 0 &&
            string.Join("|", s.Trace) == "leader|invite|message:TeamInviteSelectPlayerPanel.InviteMsg:",
            "Original non-NPC invitation branch");
        s = new InviteServices { Pending = new TaskCompletionSource<WorldTeamInviteResult>() };
        operation = WorldTeamInviter.Execute(s, actor, s);
        var failure = new InvalidOperationException("local operation failed");
        s.Pending.SetException(failure);
        try { await operation; throw new Exception("Operation failure was swallowed"); }
        catch (InvalidOperationException e) { Require(ReferenceEquals(e, failure), "Preserve original async failure"); }
        Require(string.Join("|", s.Trace) == "leader|invite", "Failed operation emits no success or mapped error");
        Console.WriteLine("Original team invitation checks passed: capacity/permission ordering, exact IDs, async operation, NPC/player messages and target error branches.");
    }

    private static void Run(string root)
    {
        string assets = Path.Combine(root, "unity/Assets/Official/World/Map/12");
        var labels = JsonSerializer.Deserialize<Map12LabelData>(File.ReadAllText(Path.Combine(assets,
            "Resources/Map12/Native/region-label-data.json")), new JsonSerializerOptions { IncludeFields = true });
        using var rulesDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "Resources/Map12/steles.json")));
        using var battleDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "map12-battles.json")));
        JsonElement rules = rulesDoc.RootElement;
        foreach (var row in rules.GetProperty("Steles").EnumerateArray())
        {
            var source = new WorldBattleSteleInitData
            {
                StageId = Int(row, "StageId"), PeriodicityGroupId = Int(row, "PeriodicityGroupId"),
                EntityLevel = Int(row, "RegionLevel") + .75f, EntitySubRank = Int(row, "RegionSubRank"),
                RegionSubRank = Int(row, "RegionSubRank"), MonsterLevelAdd = Int(row, "MonsterLevelAdd"),
                SupportTeam = row.GetProperty("SupportTeam").GetBoolean(), TeamMaxPlayer = Int(rules, "TeamMaxPlayer"),
                RecommendCombatRatingIndex = Int(rules, "RecommendCombatRatingIndex"),
                RuleIds = row.GetProperty("RuleIds").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                CombatRatingLevelValues = row.GetProperty("CombatRatingLevelValues").EnumerateArray().Select(x => x.GetInt64()).ToArray()
            };
            var stageInfo = battleDoc.RootElement.GetProperty("stages").EnumerateArray().Single(x => Int(x, "Id") == source.StageId);
            var services = new Services { StageInfo = new PlayerPlayStageInfo(
                stageInfo.GetProperty("SupportTeam").GetBoolean(), stageInfo.GetProperty("JoinInMiddle").GetBoolean(),
                stageInfo.GetProperty("Background").GetBoolean(), stageInfo.GetProperty("LeaveInPreparing").GetBoolean(),
                stageInfo.GetProperty("LeaveInPlaying").GetBoolean()) };
            var stage = WorldBattleSteleContextFactory.Bind(new WorldStageUiContext(), source, labels, services);
            var info = stage.BattleStele;
            Require(services.PlayerReads == 0, "Defer player reads until original initialization step");
            Require(info.RecommendCombatRating == source.CombatRatingLevelValues[source.RecommendCombatRatingIndex],
                "Original recommendation index");
            services.SubRank = source.EntitySubRank;
            info.PrepareRankSuppression(); Require(!info.RankSuppressed, "Equal rank has no advantage");
            services.SubRank = Enumerable.Range(labels.suppressUnlockRank, labels.subRankToRank.Length - labels.suppressUnlockRank)
                .First(i => labels.subRankToRank[i] > labels.subRankToRank[source.EntitySubRank]);
            info.PrepareRankSuppression(); Require(info.RankSuppressed, "Higher player rank enables original advantage branch");
            services.SubRank = 0; info.PrepareRankSuppression(); Require(!info.RankSuppressed, "Suppression unlock threshold");
            source.RegionSubRank = null;
            int reads = services.PlayerReads; info.PrepareRankSuppression();
            Require(!info.RankSuppressed && services.PlayerReads == reads, "Missing region short-circuits player query");
            source.RegionSubRank = source.EntitySubRank;
            var colors = labels.combatRatingColors.Single(x => x.type == (int)CombatRateingColorType.BattleStele);
            foreach (long threshold in source.CombatRatingLevelValues)
            {
                services.Rating = threshold; info.PrepareCombatRating();
                int expected = source.CombatRatingLevelValues.Count(x => x > threshold);
                Require(info.CombatRating == threshold && Same(info.CombatRatingColor, colors.colors.Single(x => x.Key == expected).Value),
                    "Threshold equality uses original BattleStele theme");
            }
            Require(info.TeamMemberCount() == (1, 4) && !info.HasTeam(), "Original solo branch");
            services.Members = 0; Require(info.TeamMemberCount() == (0, 4) && info.HasTeam(), "Do not clamp existing empty team");
            services.Members = 3; Require(info.TeamMemberCount() == (3, 4), "Read live team count");
            info.PrepareTeamTarget(); info.OpenTeam();
            Require(services.Target.StageId == source.StageId && services.Target.GroupId == source.PeriodicityGroupId &&
                WorldBattleSteleTeamTarget.TargetCatalog == 9, "Original stage and periodicity team identity");
            Require(services.Target.Equals(new WorldBattleSteleTeamTarget(source.StageId, source.PeriodicityGroupId)) &&
                !services.Target.Equals(new WorldBattleSteleTeamTarget(source.StageId, source.PeriodicityGroupId + 1)), "Team target equality");
            stage.ShowRewardPreview(null);
            Require(stage.Level == Int(row, "RegionLevel") && services.RewardLevel == Int(row, "RegionLevel") + 7 &&
                services.RewardRank == source.EntitySubRank && ReferenceEquals(services.RewardRules, source.RuleIds),
                "Original cast-before-add reward level, entity rank and exact award rule IDs");
            int refreshes = 0; Action refresh = () => refreshes++;
            info.SubscribeTeamChanges(refresh); services.Emit(); Require(refreshes == 3, "All three original team events");
            info.UnsubscribeTeamChanges(refresh); services.Emit(); Require(refreshes == 3, "Detach all original team events");
            info.PlayFightAction(source.StageId, () => { services.Trace.Add("start"); services.Trace.Add("close"); });
            Require(services.StageLookup == source.StageId && string.Join(",", services.Trace) == "start,close", "Actual original state gate and callback order");
            services.Trace.Clear(); services.Hard = true;
            info.PlayFightAction(source.StageId, () => services.Trace.Add("start"));
            Require(string.Join(",", services.Trace) == "notice:ui_play_state_hard_mutex", "Hard state gate cannot start battle");
        }
        Console.WriteLine("Map12 stele context checks passed: seven original stages, suppression, threshold colors, rewards, team identity/events and actual action gate.");
    }

    public static int Main(string[] args)
    {
        string editor = args[2];
        string[] search = { args[1], Path.Combine(args[0], "unity/Library/ScriptAssemblies"),
            Path.Combine(editor, "Data/Managed/UnityEngine"), Path.Combine(editor, "Data/Managed") };
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = search.Select(dir => Path.Combine(dir, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : context.LoadFromAssemblyPath(Path.GetFullPath(path));
        };
        Run(args[0]); CheckInviter().GetAwaiter().GetResult(); return 0;
    }
}
