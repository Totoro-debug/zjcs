using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using InfoFixture = Zjcs.Editor.ProfessionUI.ProfessionPlayerInfoValidation;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private sealed class TeamIconTarget : IProfessionInfoNpcTarget
        {
            public long Id;
            public bool Npc;
            public ActorTargetType TargetType => Npc ? ActorTargetType.Npc : ActorTargetType.Player;
            public long TargetId => Id;
            public long ServerId => -1;
            public bool IsNpc => Npc;
            public int NpcClassId => 123;
            public string GetFigurePath(Sex sex) => null;
            public bool CheckActorCanAssist(int playType) => true;
        }
        private sealed class TeamLite { public long Id; }
        private sealed class TeamIconsFixture : ITeamIconServices, ITeamNpcIconServices
        {
            public readonly InfoFixture.Bindings Info = new InfoFixture.Bindings();
            public IProfessionPlayerInfoBindings PlayerInfo => Info;
            public long MainPlayerId => 1;
            public bool ManagerAvailable => true;
            public bool MainPlayerAvailable => true;
            public bool RelationshipAvailable => true;
            public bool? Online;
            public bool? TeamPlayerOnline(long id) => Online;
            public bool IsFriend(long id) => false;
            public event Action<long> TeamPlayerOffline, TeamPlayerOnlineChanged, RelationAdded, RelationRemoved;
            public int Subscribers => (TeamPlayerOffline?.GetInvocationList().Length ?? 0)
                + (TeamPlayerOnlineChanged?.GetInvocationList().Length ?? 0);
            public readonly List<(long id, bool latest)> Requests = new List<(long, bool)>();
            public TaskCompletionSource<object> NextLite;
            public TaskCompletionSource<bool> Wait;
            public TaskCompletionSource<object> Brief;
            public int Waits, CancelledWaits, NpcPopups, PlayerPopups;
            public object LastBrief;
            public int LastPopupScene, LastSkillGroup;
            public IProfessionInfoNpcTarget LastNpc;
            public int PlanesRank => 2;
            public int Profession = 10;
            public readonly List<string> NpcTrace = new List<string>();
            public TeamNpcIconDefinition GetNpc(int id)
            { NpcTrace.Add("npc:" + id); return new TeamNpcIconDefinition { NameKey = id == 11015 ? "ui_npc_11015" : "npc-name", Icon = "npc-icon" }; }
            public int GetProfession(int id, int level, int planesRank)
            { NpcTrace.Add("level:" + id + ":" + level + ":" + planesRank); return Profession; }
            public long CalcCombatRating(int id, int level, int subRank)
            { NpcTrace.Add("rating:" + id + ":" + level + ":" + subRank); return 321; }
            public Task<object> GetLiteInfo(IProfessionInfoTarget actor, CancellationToken token, bool latest = false)
            {
                Requests.Add((actor.TargetId, latest));
                return NextLite?.Task ?? Task.FromResult<object>(new TeamLite { Id = actor.TargetId });
            }
            public long LiteInfoId(object info) => ((TeamLite)info).Id;
            public Task WaitForSeconds(float seconds, CancellationToken token)
            {
                SteleRequire(seconds == 9f, "Original portrait update interval changed.");
                Waits++; Wait = new TaskCompletionSource<bool>();
                token.Register(() => { CancelledWaits++; Wait.TrySetCanceled(token); });
                return Wait.Task;
            }
            public Task<object> GetPlayerBriefInfo(long id)
            { Brief = new TaskCompletionSource<object>(); return Brief.Task; }
            public void PopupNpc(Component owner, IProfessionInfoNpcTarget target) { NpcPopups++; LastNpc = target; }
            public void PopupPlayer(Component owner, object info, int scene, int group)
            { PlayerPopups++; LastBrief = info; LastPopupScene = scene; LastSkillGroup = group; }
            public void EmitOffline(long id) => TeamPlayerOffline?.Invoke(id);
            public void EmitOnline(long id) => TeamPlayerOnlineChanged?.Invoke(id);
        }
        private sealed class TeamIconStateProbe : IWorldStateConfig
        {
            public int State = -1;
            public void SetState(WorldStateConfigValue state) { State = state; }
            public void Clear() { }
        }
        private static void SetTeamIconField(object owner, string name, object value)
            => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(owner, value);

        private static IEnumerator VerifyTeamIconSteps()
        {
            // Component lifecycle fixture only. The complete original native team
            // window and its real local player/NPC services are validated separately.
            var s = new TeamIconsFixture();
            var root = new GameObject("OriginalTeamIconLifecycleFixture"); root.SetActive(false);
            var ui = root.AddComponent<PlayerInfoUI>();
            ui.mStateSwitcher = WorldStateSwitcher<ActorTargetType>.FromBindings(root, Array.Empty<IWorldStateConfig>());
            SetTeamIconField(ui, "mIsMyselfSwitcher", WorldStateSwitcher<bool>.FromBindings(root, Array.Empty<IWorldStateConfig>()));
            var me = root.AddComponent<PlayerIconMe>(); me.InfoUI = ui;
            var other = root.AddComponent<TeamPlayerIconOther>(); other.InfoUI = ui;
            var playerIcon = root.AddComponent<TeamPlayerIcon>(); playerIcon.myIconMe = me; playerIcon.myIconOther = other;
            SetTeamIconField(playerIcon, "mIsSelfSwitcher", WorldStateSwitcher<bool>.FromBindings(root, Array.Empty<IWorldStateConfig>()));
            var member = root.AddComponent<Zjcs.Official.TeamUI.TeamMemberIcon>();
            var leader = new GameObject("Leader flag fixture"); leader.transform.SetParent(root.transform);
            var offline = new GameObject("Offline flag fixture"); offline.transform.SetParent(root.transform);
            var state = new TeamIconStateProbe();
            SetTeamIconField(member, "mPlayerIcon", playerIcon);
            SetTeamIconField(member, "mLeaderFlagGo", leader);
            SetTeamIconField(member, "mOfflineGo", offline);
            SetTeamIconField(member, "mMemberStateSwitcher",
                WorldStateSwitcher<Zjcs.Official.TeamUI.TeamMemberIcon.MemberType>.FromBindings(root, new[] { state }));
            member.Configure(s);
            root.SetActive(true);
            try
            {
                member.Init(null, false);
                SteleRequire(state.State == 0 && !leader.activeSelf && !offline.activeSelf && s.Subscribers == 2,
                    "Original empty member or event subscription branch changed.");
                var npc = new TeamIconTarget { Id = 9007199254740993L, Npc = true };
                member.Init(npc, false);
                yield return null;
                SteleRequire(state.State == 1 && !me.enabled && other.enabled && other.InitFinished
                    && ReferenceEquals(s.Info.LastTarget, npc) && s.Requests.Last() == (npc.Id, false)
                    && !offline.activeSelf && s.Subscribers == 2,
                    "NPC member must use the original generic Player icon branch and exact 64-bit identity.");
                other.HandleOnClick();
                SteleRequire(s.NpcPopups == 1 && ReferenceEquals(s.LastNpc, npc) && s.PlayerPopups == 0,
                    "Original NPC portrait click branch changed.");
                other.IsShowNpcInfo = false; other.HandleOnClick();
                SteleRequire(s.NpcPopups == 1, "Original NPC info visibility guard changed.");
                other.IsShowNpcInfo = true;

                var player = new TeamIconTarget { Id = 42 };
                s.Online = false; member.Init(player, false);
                yield return null;
                SteleRequire(offline.activeSelf && state.State == 1, "Original offline player branch changed.");
                s.Online = true; s.EmitOnline(99);
                SteleRequire(offline.activeSelf, "Unrelated team event refreshed a different member.");
                s.EmitOnline(42);
                SteleRequire(!offline.activeSelf, "Original online event must share the offline refresh handler.");
                s.Online = null; s.EmitOffline(42);
                SteleRequire(!offline.activeSelf, "Missing team-player record defaults to online.");
                other.HandleOnClick(); var pendingBrief = s.Brief;
                SteleRequire(s.PlayerPopups == 0 && pendingBrief != null, "Player popup must await brief info.");
                var brief = new object(); pendingBrief.SetResult(brief); yield return null;
                SteleRequire(s.PlayerPopups == 1 && ReferenceEquals(s.LastBrief, brief)
                    && s.LastPopupScene == 4 && s.LastSkillGroup == 123, "Original player detail initialization differs.");

                s.NextLite = new TaskCompletionSource<object>();
                other.Init(player, keepUpdate: true);
                SteleRequire(!other.InitFinished, "Reinitialization must disable the old portrait state first.");
                s.NextLite.SetResult(new TeamLite { Id = 42 }); s.NextLite = null;
                yield return null;
                SteleRequire(other.InitFinished && s.Waits == 1 && !s.Requests.Last().latest,
                    "Original first async portrait request or update delay changed.");
                s.Wait.SetResult(true); yield return null;
                SteleRequire(s.Requests.Last() == (42L, true) && s.Waits == 2,
                    "Repeated original portrait request must ask for latest data after nine seconds.");
                root.SetActive(false); yield return null;
                SteleRequire(!other.InitFinished && s.CancelledWaits == 2 && s.Subscribers == 0,
                    "Disable must cancel the live portrait request loop and detach both team events.");
                root.SetActive(true); yield return null;
                SteleRequire(s.Subscribers == 2, "Re-enable duplicated or lost original member listeners.");
                member.Init(new TeamIconTarget { Id = 1 }, true); yield return null;
                SteleRequire(playerIcon.IsSelfPlayer && me.enabled && !other.enabled && leader.activeSelf
                    && state.State == 1 && me.InitFinished, "Original self portrait or leader flag branch changed.");
                member.Init(null, false);
                SteleRequire(state.State == 0 && !leader.activeSelf, "Reused member did not restore its original empty state.");
                Debug.Log("Map12 original team icon lifecycle passed: generic NPC/self/player branches, exact identities, awaited refresh/popup, online flags, cancellation and event reuse. Native full-window rendering and real local services remain pending.");
            }
            finally { UnityEngine.Object.Destroy(root); s.Info.Cleanup(); }
            var npcSteps = VerifyTeamNpcIconSteps();
            while (npcSteps.MoveNext()) yield return npcSteps.Current;
        }

        private static IEnumerator VerifyTeamNpcIconSteps()
        {
            var s = new TeamIconsFixture();
            var root = new GameObject("OriginalNpcIconLifecycleFixture"); root.SetActive(false);
            var view = root.AddComponent<TeamNpcIconUI>(); view.Configure(s);
            var icon = new GameObject("NPC image fixture", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.transform.SetParent(root.transform); view.NpcIcon = icon.GetComponent<Image>();
            var profession = new GameObject("Profession image fixture", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            profession.transform.SetParent(root.transform);
            view.ProfessionIconGo = profession; view.ProfessionIcon = profession.GetComponent<Image>();
            var name = new GameObject("NPC name fixture", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(WorldMultiLanguageText));
            name.transform.SetParent(root.transform);
            var nameText = name.GetComponent<WorldMultiLanguageText>();
            nameText.Localizer = key => key == "ui_npc_11015" ? "利姆鲁" : key;
            nameText.myKey = "ui_npc_11015";
            name.GetComponent<TextMeshProUGUI>().font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset");
            view.NameText = nameText;
            var text = new GameObject("NPC rating fixture", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(WorldMultiLanguageText));
            text.transform.SetParent(root.transform);
            text.GetComponent<TextMeshProUGUI>().font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset");
            view.CombatRatingText = text.GetComponent<WorldMultiLanguageText>();
            view.CombatRatingText.myKey = "{0}"; view.CombatRatingText.Localizer = key => key;
            try
            {
                view.Init(11015, 55, 6);
                SteleRequire(string.Join("|", s.NpcTrace) == "npc:11015|level:11015:55:2" && s.Info.Operations.Count == 0,
                    "Original NPC icon must load config/level detail before active refresh.");
                root.SetActive(true); yield return null;
                SteleRequire(string.Join("|", s.NpcTrace) == "npc:11015|level:11015:55:2|rating:11015:55:6"
                    && view.CombatRatingText.Text == "compact-321" && profession.activeSelf
                    && !icon.activeSelf && view.NameText.Text == "利姆鲁"
                    && s.Info.Operations.Select(op => op.Path).SequenceEqual(new[] { "npc-icon", "first-10" }),
                    "Missing Map12 assist portraits must fall back to the original text name without changing NPC icon lifecycle.");
                root.SetActive(false);
                SteleRequire(s.Info.Operations.All(op => op.Disposals == 1), "NPC icon disable leaked an original resource operation.");
                root.SetActive(true); yield return null;
                SteleRequire(s.Info.Operations.Count == 4 && s.Info.Operations.Skip(2).All(op => op.Disposals == 0),
                    "NPC icon re-enable must use a fresh disposable bag.");
                s.Profession = 0; view.Init(123, 56, 7);
                SteleRequire(!profession.activeSelf && s.Info.Operations.Count == 5
                    && icon.activeSelf
                    && s.Info.Operations.Take(4).All(op => op.Disposals == 1)
                    && s.NpcTrace.Last() == "rating:123:56:7",
                    "NPC reinitialization must release prior images and preserve the no-profession branch.");
                root.SetActive(false);
                SteleRequire(s.Info.Operations.All(op => op.Disposals == 1), "NPC icon resource lifetime differs after reuse.");
                Debug.Log("Map12 original NpcIconUI lifecycle passed: parser order, planes-rank selection, consumed rating, original resource order, no-profession branch and exact disposal. Real native resources and offline NPC formulas remain separate.");
            }
            finally { UnityEngine.Object.Destroy(root); s.Info.Cleanup(); }
        }
    }
}
