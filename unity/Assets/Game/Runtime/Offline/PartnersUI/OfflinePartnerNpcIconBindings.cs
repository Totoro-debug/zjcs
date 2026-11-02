using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Offline.PartnersUI
{
    // NPC specialization of the original player-icon ports. Player portraits,
    // popups and team status remain supplied by the containing feature.
    public sealed class OfflinePartnerNpcIconBindings : ITeamIconServices
    {
        private readonly OfflinePartnerNpcRuntime runtime;
        private readonly ITeamIconServices players;
        private readonly ITeamPlayerHpServices hp;
        private readonly Func<int, Sex, string> figure;
        private readonly Func<int, int, bool> canAssist;
        public IProfessionPlayerInfoBindings PlayerInfo { get; }

        public OfflinePartnerNpcIconBindings(OfflinePartnerNpcRuntime runtime, ITeamIconServices players, ITeamPlayerHpServices hp,
            IProfessionImageResourceLoader images, Func<string, string> localize,
            Func<int, Sex, string> figure, Func<int, int, bool> canAssist)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.hp = hp ?? throw new ArgumentNullException(nameof(hp));
            this.figure = figure ?? throw new ArgumentNullException(nameof(figure));
            this.canAssist = canAssist ?? throw new ArgumentNullException(nameof(canAssist));
            PlayerInfo = new NpcInfoBindings(runtime, players.PlayerInfo, images, localize);
        }

        public void InitializeDialogueIcon(Component component, PartnerDialogueNpcData npc, int planesRank)
        {
            if (!component) throw new ArgumentNullException(nameof(component));
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            // NpcPanelDialogueListNpcItem uses this exact original constructor:
            // address-book level 1 / Norank; assistance supplies its own levels.
            var info = runtime.CreateLiteInfo(npc.NpcId, npc.NpcClassId, npc.PlayerId, 1, runtime.Norank, planesRank);
            var target = CreateTarget(info);
            foreach (var child in component.GetComponentsInChildren<TeamPlayerUIBase>(true)) child.Configure(hp);
            if (component is TeamPlayerIcon icon)
            { icon.Configure(this); icon.Init(target, playerLiteInfo: info); }
            else if (component is TeamPlayerIconOther other)
            { other.Configure(this); other.Init(target, playerLiteInfo: info); }
            else throw new InvalidOperationException("Original dialogue NPC icon controller is unbound.");
        }

        public IProfessionInfoNpcTarget CreateTarget(OfflinePartnerNpcLiteInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            return new NpcTarget(info, figure, canAssist);
        }

        public Task<object> GetLiteInfo(IProfessionInfoTarget target, CancellationToken token, bool latest = false)
        {
            token.ThrowIfCancellationRequested();
            if (!(target is NpcTarget npc)) return players.GetLiteInfo(target, token, latest);
            var info = npc.Info;
            return Task.FromResult<object>(runtime.CreateLiteInfo(info.TargetId, info.NpcClassId, info.OwnerPlayerId,
                info.Level, info.SubRank, info.CreatePlanesRank));
        }
        public long LiteInfoId(object info) => info is OfflinePartnerNpcLiteInfo npc ? npc.TargetId : players.LiteInfoId(info);
        public long MainPlayerId => players.MainPlayerId;
        public bool ManagerAvailable => players.ManagerAvailable;
        public bool MainPlayerAvailable => players.MainPlayerAvailable;
        public bool RelationshipAvailable => players.RelationshipAvailable;
        public bool IsFriend(long id) => players.IsFriend(id);
        public bool? TeamPlayerOnline(long id) => players.TeamPlayerOnline(id);
        public event Action<long> TeamPlayerOffline { add { players.TeamPlayerOffline += value; } remove { players.TeamPlayerOffline -= value; } }
        public event Action<long> TeamPlayerOnlineChanged { add { players.TeamPlayerOnlineChanged += value; } remove { players.TeamPlayerOnlineChanged -= value; } }
        public event Action<long> RelationAdded { add { players.RelationAdded += value; } remove { players.RelationAdded -= value; } }
        public event Action<long> RelationRemoved { add { players.RelationRemoved += value; } remove { players.RelationRemoved -= value; } }
        public Task WaitForSeconds(float seconds, CancellationToken token) => players.WaitForSeconds(seconds, token);
        public Task<object> GetPlayerBriefInfo(long id) => players.GetPlayerBriefInfo(id);
        public void PopupNpc(Component owner, IProfessionInfoNpcTarget target) => players.PopupNpc(owner, target);
        public void PopupPlayer(Component owner, object info, int scene, int skills) => players.PopupPlayer(owner, info, scene, skills);

        private sealed class NpcTarget : IProfessionInfoNpcTarget
        {
            internal readonly OfflinePartnerNpcLiteInfo Info;
            private readonly Func<int, Sex, string> figure;
            private readonly Func<int, int, bool> canAssist;
            internal NpcTarget(OfflinePartnerNpcLiteInfo info, Func<int, Sex, string> figure, Func<int, int, bool> canAssist)
            { Info = info; this.figure = figure; this.canAssist = canAssist; }
            public ActorTargetType TargetType => ActorTargetType.Npc;
            public long TargetId => Info.TargetId;
            public long ServerId => Info.ServerId;
            public bool IsNpc => true;
            public int NpcClassId => Info.NpcClassId;
            public string GetFigurePath(Sex sex) => figure(NpcClassId, sex);
            public bool CheckActorCanAssist(int playType) => canAssist(NpcClassId, playType);
        }

        private sealed class NpcInfoBindings : IProfessionPlayerInfoBindings
        {
            private readonly OfflinePartnerNpcRuntime runtime;
            private readonly IProfessionPlayerInfoBindings players;
            private readonly Func<string, string> localize;
            public IProfessionImageResourceLoader Resources { get; }
            internal NpcInfoBindings(OfflinePartnerNpcRuntime runtime, IProfessionPlayerInfoBindings players,
                IProfessionImageResourceLoader images, Func<string, string> localize)
            {
                this.runtime = runtime;
                this.players = players ?? throw new ArgumentNullException(nameof(players));
                Resources = new NpcImages(images ?? throw new ArgumentNullException(nameof(images)),
                    players.Resources ?? throw new ArgumentException("Player image resources are unbound.", nameof(players)));
                this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
            }
            public PlayerInfoData BuildLiteData(object value, IProfessionInfoTarget target)
            {
                if (!(value is OfflinePartnerNpcLiteInfo info)) return players.BuildLiteData(value, target);
                if (!(target is IProfessionInfoNpcTarget npc) || target.TargetId != info.TargetId || npc.NpcClassId != info.NpcClassId)
                    throw new InvalidOperationException("NPC lite-info identity does not match its icon target.");
                // Original online NPC PlayerLiteInfo -> PlayerInfoData branch.
                return new PlayerInfoData { TargetData = target, Icon = info.Icon, Name = localize(info.NameKey),
                    NameArg = players.CreateNameArg(info.NameKey, true), CombatRating = info.CombatRating,
                    SubRank = info.SubRank, Level = info.Level, Profession = info.Profession, Sex = (Sex)info.Sex };
            }
            public string FirstProfessionIcon(int profession) => runtime.ProfessionIcon(profession, false);
            public string ThirdProfessionIcon(int profession) => runtime.ProfessionIcon(profession, true);
            public IProfessionInfoPlayer MainPlayer => players.MainPlayer;
            public bool ManagerAvailable => players.ManagerAvailable;
            public SafeAction OnPlayerUpdate => players.OnPlayerUpdate;
            public PlayerInfoData BuildData(IProfessionInfoPlayer player) => players.BuildData(player);
            public object BuildBriefInfo(IProfessionInfoPlayer player, bool online) => players.BuildBriefInfo(player, online);
            public object CreateNameArg(string name, bool translate) => players.CreateNameArg(name, translate);
            public object CompactNumber(long value) => players.CompactNumber(value);
            public string SubRankKey(int rank) => players.SubRankKey(rank);
            public string ProfessionNameKey(int profession) => players.ProfessionNameKey(profession);
            public string GetFullBodyGuid(int profession, Sex sex) => players.GetFullBodyGuid(profession, sex);
            public ProfessionInfoText NameWithPlanes(object name, long server) => players.NameWithPlanes(name, server);
            public bool HasHonorTitle(object appearance) => players.HasHonorTitle(appearance);
            public ProfessionInfoNpcDefinition GetNpc(int classId) => players.GetNpc(classId);
            public GameObject InstantiateKeepScale(GameObject prefab, Transform parent) => players.InstantiateKeepScale(prefab, parent);
            public void OverrideNpcInitialAnimation(GameObject model, int npcId) => players.OverrideNpcInitialAnimation(model, npcId);
            public TeamTargetCatalog? TeamCatalog(Component component) => players.TeamCatalog(component);
            public ProfessionInfoRobotInvite RobotInvite(Component component) => players.RobotInvite(component);
            public ProfessionInfoRobotInvite DefaultRobotInvite() => players.DefaultRobotInvite();
            public bool CanJointTogether(int target, int main) => players.CanJointTogether(target, main);
            public int DuelSkillGroup(long player, long target) => players.DuelSkillGroup(player, target);
            public void Popup(Component owner, Action<IProfessionPlayerInfoDetailPanel> initialized) => players.Popup(owner, initialized);
        }

        private sealed class NpcImages : IProfessionImageResourceLoader
        {
            private readonly IProfessionImageResourceLoader icons, players;
            internal NpcImages(IProfessionImageResourceLoader icons, IProfessionImageResourceLoader players)
            { this.icons = icons; this.players = players; }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object
            {
                var operation = icons.LoadAsset<T>(path);
                if (operation.Result != null) return operation;
                operation.Dispose();
                return players.LoadAsset<T>(path);
            }
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
            {
                var operation = icons.LoadAsset<T>(path);
                if (operation.Result != null) return operation;
                operation.Dispose();
                return players.LoadAssetAsync<T>(path);
            }
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
            {
                var operation = icons.LoadAssetByGuid<T>(guid);
                if (operation.Result != null) return operation;
                operation.Dispose();
                return players.LoadAssetByGuid<T>(guid);
            }
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object
            {
                var operation = icons.LoadAssetByGuid<T>(guid);
                if (operation.Result != null) return operation;
                operation.Dispose();
                return players.LoadAssetAsyncByGuid<T>(guid);
            }
        }
    }
}
