using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Zjcs.Official.TeamUI;
using Zjcs.Official.ProfessionUI;

public static class Map12TeamInviteRulesHarness
{
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); }

    private sealed class Services : ITeamInviteNpcServices, ITeamInviteComparerServices
    {
        public long Owner = 42;
        public long MainPlayerId => Owner;
        public int PlanesRank => 2;
        public int MainPlayerProfession => 10;
        public bool IsNpcPinLimited { get; set; }
        public readonly List<int> Candidates = new List<int>();
        public readonly HashSet<int> NotFighters = new HashSet<int>(), Limited = new HashSet<int>();
        public readonly Dictionary<int, TeamInviteNpcRecord> Npcs = new Dictionary<int, TeamInviteNpcRecord>();
        public readonly List<string> Trace = new List<string>();
        public readonly Dictionary<long, (long, long)> Friendships = new Dictionary<long, (long, long)>();
        public List<TeamInviteNpcTarget> Targets;
        public TaskCompletionSource<List<TeamInvitePlayerInfoData>> Response;
        public CancellationToken RequestedToken;
        public IEnumerable<int> GetNpcsExcludingMapExploreBoss() => Candidates;
        public bool IsFightAssist(int id) { Trace.Add("fighter:" + id); return !NotFighters.Contains(id); }
        public bool IsNpcAssistLimit(int id) { Trace.Add("limit:" + id); return Limited.Contains(id); }
        public TeamInviteNpcRecord GetNpc(int id) => Npcs.TryGetValue(id, out var value) ? value : null;
        public TeamInviteNpcRecord CreateNpcDataByInfo(long owner, int id)
        { Trace.Add("create:" + id); return new TeamInviteNpcRecord { PlayerId = owner, NpcClassId = id }; }
        public Task<List<TeamInvitePlayerInfoData>> GetTeamInviteInfoData(
            List<TeamInviteNpcTarget> targets, TeamTargetData target, CancellationToken token)
        { Trace.Add("request"); Targets = targets; RequestedToken = token; return Response.Task; }
        public int GetNpcAssistCount(int type, long id) => checked(type + (int)id);
        public string GetNpcFigurePath(int id, Sex sex) => "official-figure-" + id;
        public bool CheckNpcCanAssist(int type, long id) => type == 9;
        public bool CanJointTogether(int target, int main) => target == main;
        public (long ToTarget, long ToPlayer)? ToFriendshipData(IProfessionInfoTarget target)
            => Friendships.TryGetValue(target.TargetId, out var value) ? value : ((long, long)?)null;
        public void RefreshFestivalActivityData() { Trace.Add("activity"); }
        public bool HasFestivalBossNpc(int activity, int id) => activity == 6 && id == 101;
        public void Error(string text) { Trace.Add("error"); }
        public void Info(string text) { Trace.Add("info"); }
    }

    private static TeamInvitePlayerInfoData Row(Services services, int id, bool online = true, long rating = 1)
        => new TeamInvitePlayerInfoData
        {
            TargetData = new TeamInviteNpcTarget(services, id, id, services.Owner, 2),
            Online = online, CombatRating = rating, Profession = 10,
            PropTypes = new Dictionary<PropType, long>()
        };

    private static async Task Run()
    {
        var services = new Services();
        var provider = new TeamInviteNpc(services);
        Require(await provider.LoadPlayers(null, default) == null && !services.Trace.Contains("request"),
            "The original empty selector must return null without RPC.");
        services.Candidates.AddRange(new[] { 102, 101, 103, 104 });
        services.NotFighters.Add(103); services.Limited.Add(104);
        services.Npcs.Add(101, new TeamInviteNpcRecord { NpcId = 9001, PlayerId = 42, NpcClassId = 101 });
        services.Response = new TaskCompletionSource<List<TeamInvitePlayerInfoData>>();
        using var cancellation = new CancellationTokenSource();
        var task = provider.LoadPlayers(null, cancellation.Token);
        Require(!task.IsCompleted && services.RequestedToken == cancellation.Token,
            "Original request must be awaited and use the caller cancellation token.");
        Require(services.Targets.Select(x => x.TargetId).SequenceEqual(new long[] { 42000102, 9001 }),
            "Original source order, synthetic identity, and existing NPC identity changed.");
        Require(!services.Trace.Contains("limit:103") && !services.Trace.Contains("create:104"),
            "Original short circuit eligibility order changed.");
        Require(services.Targets.All(x => x.NpcLevel == 0 && x.NpcSubRank == 0 && x.ServerId == -1
            && x.CreatePlanesRank == 2 && x.OwnerPlayerId == 42), "Unfilled original target fields changed.");
        var response = new List<TeamInvitePlayerInfoData> { Row(services, 102) };
        services.Response.SetResult(response);
        Require(ReferenceEquals(await task, response), "Original response identity must be retained.");
        Require(provider.GetAssistCount(9, 101) == 110 && provider.GetSortTypes().Count == 12
            && provider.GetSortTypes()[0] == TeamInviteSubPanelBase.TeamInviteSortType.FriendShip
            && provider.GetSortTypes()[1] == TeamInviteSubPanelBase.TeamInviteSortType.CombatRating,
            "Original count boundary/default sort order changed.");
        services.Owner = long.MaxValue; services.Candidates.Clear(); services.Candidates.Add(102);
        services.Trace.Clear(); services.Response = new TaskCompletionSource<List<TeamInvitePlayerInfoData>>();
        var overflow = provider.LoadPlayers(null, default);
        Require(services.Targets[0].TargetId == 0 && services.Trace.Contains("error"),
            "Original overflow logs and retains the created identity; it does not throw or repair it.");
        services.Response.SetResult(null); await overflow;

        var rows = new Dictionary<long, TeamInvitePlayerInfoData>
        {
            [101] = Row(services, 101, false, 1000),
            [102] = Row(services, 102, true, 100),
            [103] = Row(services, 103, true, 999)
        };
        services.Npcs[101].IsCollected = true; services.Npcs[101].CollectedTime = new DateTime(200);
        services.Npcs[102] = new TeamInviteNpcRecord { IsCollected = true, CollectedTime = new DateTime(100) };
        var comparer = new TeamInvitePlayerComparer(id => rows[id], false, services);
        var ids = new List<long> { 103, 101, 102 };
        ids.Sort(comparer.PlayerCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 102, 101, 103 }), "Pinned NPC priority must precede online/rating.");
        services.IsNpcPinLimited = true; ids.Sort(comparer.PlayerCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 103, 102, 101 }), "Locked pin function must not expose pin priority.");
        comparer.SetCompanionId(102); ids.Sort(comparer.PlayerCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 102, 103, 101 }), "Companion precedes rating after online.");
        comparer.SetCompanionId(0);
        services.Friendships[102] = (3, 4); services.Friendships[103] = (2, 6);
        ids.Sort(comparer.PlayerFriendComparer);
        Require(ids.SequenceEqual(new long[] { 103, 102, 101 }), "Friendship uses both directions.");
        rows[102].PropTypes[PropType.LightDamageAdd] = 12;
        var elemental = new TeamInvitePlayerComparer(id => rows[id], false, services, PropType.LightDamageAdd);
        ids.Sort(elemental.PlayerElementPropTypeComparer);
        Require(ids.SequenceEqual(new long[] { 102, 103, 101 }), "Missing elemental property must compare as zero.");
        Require(TeamInvitePlayerComparer.ComparerResult(long.MaxValue, -1, ComparerOrderType.Asc) == -1,
            "The original signed subtraction overflow semantics changed.");
        var joint = new TeamInvitePlayerComparer(id => rows[id], true, services);
        rows[103].Profession = 11;
        Require(joint.GetProfessionLimitVal(rows[101]) == 0 && joint.GetProfessionLimitVal(rows[103]) == 0
            && joint.GetProfessionLimitVal(rows[102]) == 1, "Joint-strike eligibility/online short circuit changed.");
        joint.SetOnlineCheck(false);
        Require(joint.GetProfessionLimitVal(rows[101]) == 1, "Original online check override changed.");
        var assistCounts = new Dictionary<long, int> { [101] = 2, [102] = 1, [103] = 3 };
        var robot = new TeamInviteRobotComparer(id => rows[id], id => assistCounts[id], false, services);
        ids.Sort(robot.RobotCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 103, 101, 102 }),
            "Robot priority must use remaining assistance before rating and must not sort by online state.");
        robot.SetCompanionId(102); ids.Sort(robot.RobotCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 102, 103, 101 }), "Robot companion priority precedes assistance.");
        robot.SetCompanionId(0); services.IsNpcPinLimited = false;
        ids.Sort(robot.RobotCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 101, 102, 103 }),
            "Robot pinned NPC priority precedes assistance; remaining assistance precedes pin time.");
        assistCounts[102] = 2; ids.Sort(robot.RobotCombatRatingComparer);
        Require(ids.SequenceEqual(new long[] { 102, 101, 103 }), "Robot pin time remains ascending when earlier keys tie.");
        var robotJoint = new TeamInviteRobotComparer(id => rows[id], id => assistCounts[id], true, services);
        Require(robotJoint.GetProfessionLimitVal(rows[101]) == 1,
            "Robot joint-strike compatibility must ignore the offline player-state check.");
        var order = new List<int>();
        var action = new Zjcs.Official.WorldUI.WorldGAction();
        Action duplicate = () => order.Add(1);
        action += duplicate;
        action += () => order.Add(2);
        action += duplicate;
        action -= duplicate;
        action.Invoke();
        Require(order.SequenceEqual(new[] { 2, 1 }), "Original popup sorting notification removes the first matching listener.");
        order.Clear();
        var mutation = new Zjcs.Official.WorldUI.WorldGAction();
        IDisposable removed = null;
        mutation += () => { order.Add(3); removed.Dispose(); mutation += () => order.Add(5); };
        removed = mutation.Subscribe(() => order.Add(4));
        mutation.Invoke();
        Require(order.SequenceEqual(new[] { 3 }), "Original notification suppresses removed and newly added listeners during invocation.");
        Console.WriteLine("Original TeamInviteNpc async/identity/filter and multi-priority comparer checks passed.");
    }

    public static int Main(string[] args)
    {
        string[] search = { args[1], Path.Combine(args[0], "unity/Library/ScriptAssemblies"),
            Path.Combine(args[2], "Data/Managed/UnityEngine"), Path.Combine(args[2], "Data/Managed") };
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = search.Select(dir => Path.Combine(dir, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : context.LoadFromAssemblyPath(Path.GetFullPath(path));
        };
        Run().GetAwaiter().GetResult();
        return 0;
    }
}
