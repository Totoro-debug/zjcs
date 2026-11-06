using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.PartnersUI
{
    /// <summary>Lifetime scoped to the official partner and conversation windows.</summary>
    public sealed class OfflinePartnerActorUiRuntime : IDisposable
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineCharacterProgressionService progression;
        private readonly OfflinePartnerNpcRuntime npcRuntime;
        private readonly OfflineProfessionSceneTextureResources textures;
        private readonly AppearanceAssetManager appearanceManager;
        private readonly OfflineProfessionUiPlayerBindings appearanceUi;
        private readonly LocalServices players;
        private readonly OfflinePartnerPlayerInfoSource playerSource;
        private readonly OfflinePartnerPlayerHpBindings health;
        private readonly OfflinePartnerNpcIconBindings npcIcons;
        private readonly ProfessionNativeSceneAppearanceResources appearanceResources;
        private readonly TextureRegistration textureRegistration;
        private bool disposed;

        public IProfessionPlayerInfoBindings PlayerInfo => npcIcons.PlayerInfo;
        public ITeamIconServices TeamIcons => npcIcons;
        public OfflinePartnerNpcRuntime NpcRuntime => npcRuntime;

        public OfflinePartnerActorUiRuntime(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression, OfficialPartnerCatalog partners,
            IProfessionImageResourceLoader images, Func<string, string> localize,
            Action<int> openNpc, Action openPlayer, IProfessionAppearancePartBindings partBindings = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (partners == null) throw new ArgumentNullException(nameof(partners));
            if (images == null) throw new ArgumentNullException(nameof(images));
            if (localize == null) throw new ArgumentNullException(nameof(localize));
            openNpc ??= _ => { };
            openPlayer ??= () => { };

            var playerIdentities = session.Save.Player.CharacterProperties.LocalActorIdentities;
            if (playerIdentities == null || playerIdentities.PlayerId <= 0)
                throw new InvalidOperationException("Persisted local actor identities must exist before binding partner icons.");

            npcRuntime = new OfflinePartnerNpcRuntime(OfficialPartnerNpcRuntimeCatalog.Load());
            try
            {
                appearanceResources = Resources.Load<ProfessionNativeSceneAppearanceResources>("Partners/appearance-resources");
                if (!appearanceResources) throw new InvalidOperationException("Original partner player appearance resources are unbound.");
                var portrait = ProfessionNativePortraitStudioResources.LoadDefault();
                var tablesAsset = Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables");
                if (!tablesAsset) throw new InvalidOperationException("Original profession appearance tables are unbound.");
                var tables = ProfessionAppearanceTables.Load(tablesAsset.text);
                var defaults = new ProfessionAppearanceSkinDefaults(tables,
                    (Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults")
                        ?? throw new InvalidOperationException("Original profession skin defaults are unbound.")).text,
                    message => Debug.LogError(message));
                var animationRules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
                if (!animationRules) throw new InvalidOperationException("Original appearance animation rules are unbound.");
                var playerAppearance = new OfflineProfessionAppearanceBindings(playerIdentities.PlayerId,
                    () => progression.Player, itemId => progression.Catalog.ItemType(itemId), tables,
                    portrait.AppearanceConfig, animationRules, defaults);
                playerSource = new OfflinePartnerPlayerInfoSource(session, progression, playerAppearance,
                    () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(), _ => null, _ => null, (_, __) => false);

                var lowMemory = new LowMemorySubscriptionSource();
                appearanceManager = new AppearanceAssetManager(
                    Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),
                    lowMemory.Subscribe);
                textures = new OfflineProfessionSceneTextureResources(appearanceResources);
                var partImages = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                partImages.Entries = Array.Empty<OfflineProfessionImageCatalog.Entry>();
                var parts = partBindings ?? new OfflineProfessionAppearancePartBindings(portrait.AppearanceConfig,
                    tables.SpineSkinInfoParser, partImages);
                appearanceUi = new OfflineProfessionUiPlayerBindings(appearanceResources, appearanceManager,
                    parts, playerAppearance);

                var oldTextureBindings = ProfessionAppearanceTextureBindings.Current;
                textureRegistration = new TextureRegistration(oldTextureBindings);
                ProfessionAppearanceTextureBindings.Current = textures;
                var playerHp = new OfflinePlayerHealth(session, progression, () => 0,
                    () => Array.Empty<bool>(), () => DateTime.UtcNow);
                health = new OfflinePartnerPlayerHpBindings(playerHp, playerSource.OnPlayerUpdate,
                    () => playerSource.MainPlayer != null, () => true);
                players = new LocalServices(playerSource, images, npcRuntime, localize, openNpc, openPlayer);
                npcIcons = new OfflinePartnerNpcIconBindings(npcRuntime, players, health, images,
                    localize, (npc, sex) => npcRuntime.FigureModelUI(npc), (_, __) => false);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void BindTree(GameObject root)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerActorUiRuntime));
            if (!root) throw new ArgumentNullException(nameof(root));
            foreach (var widget in root.GetComponentsInChildren<PlayerInfoUI>(true))
                widget.ConfigureInfo(PlayerInfo);
            foreach (var widget in root.GetComponentsInChildren<PlayerIconMe>(true))
                widget.ConfigureInfo(PlayerInfo);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerIconOther>(true))
                widget.Configure(npcIcons);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerIcon>(true))
                widget.Configure(npcIcons);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerUIBase>(true))
                widget.Configure(health);
            appearanceUi.BindTree(root);
        }

        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerActorUiRuntime));
            npcIcons.InitializeDialogueIcon(icon, npc, 0);
        }

        public void InitializeActorIcon(Component component, long actorId, PartnerDialogueSpokesMan speaker,
            IReadOnlyDictionary<long, PartnerDialogueNpcData> npcs)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerActorUiRuntime));
            if (!component) throw new ArgumentNullException(nameof(component));
            if (speaker == PartnerDialogueSpokesMan.NPC)
            {
                if (npcs == null || !npcs.TryGetValue(actorId, out var npc))
                    throw new InvalidOperationException("Original dialogue NPC actor identity is absent: " + actorId);
                npcIcons.InitializeDialogueIcon(component, npc, 0);
                return;
            }
            if (speaker != PartnerDialogueSpokesMan.Player || playerSource.MainPlayer.Id != actorId)
                throw new InvalidOperationException("Original partner dialogue references an unsupported local actor: " + actorId);

            var target = playerSource.BuildData(playerSource.MainPlayer).TargetData;
            if (component is TeamPlayerIcon icon)
            { icon.Configure(npcIcons); icon.Init(target); }
            else if (component is TeamPlayerIconOther other)
            { other.Configure(npcIcons); other.Init(target); }
            else throw new InvalidOperationException("Original dialogue player icon controller is unbound.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (textureRegistration != null)
            {
                ProfessionAppearanceTextureBindings.Current = textureRegistration.Previous;
                textureRegistration.Dispose();
            }
            textures?.Dispose();
            appearanceManager?.Dispose();
            npcRuntime?.Dispose();
        }

        private sealed class LowMemorySubscriptionSource
        {
            private sealed class Subscription : IDisposable
            {
                private Action callback;
                internal Subscription(Action callback) { this.callback = callback; }
                public void Dispose()
                {
                    if (callback == null) return;
                    Application.lowMemory -= callback.Invoke;
                    callback = null;
                }
            }
            internal IDisposable Subscribe(Action callback)
            {
                if (callback == null) throw new ArgumentNullException(nameof(callback));
                Application.lowMemory += callback.Invoke;
                return new Subscription(callback);
            }
        }

        private sealed class LocalServices : ITeamIconServices, IProfessionPlayerInfoBindings
        {
            private readonly OfflinePartnerPlayerInfoSource source;
            private readonly OfflinePartnerNpcRuntime npcRuntime;
            private readonly Func<string, string> localize;
            private readonly Action<int> openNpc;
            private readonly Action openPlayer;
            public LocalServices(OfflinePartnerPlayerInfoSource source, IProfessionImageResourceLoader images,
                OfflinePartnerNpcRuntime npcRuntime, Func<string, string> localize,
                Action<int> openNpc, Action openPlayer)
            {
                this.source = source;
                Resources = images;
                this.npcRuntime = npcRuntime;
                this.localize = localize;
                this.openNpc = openNpc;
                this.openPlayer = openPlayer;
            }
            public IProfessionPlayerInfoBindings PlayerInfo => this;
            public IProfessionImageResourceLoader Resources { get; }
            public IProfessionInfoPlayer MainPlayer => source.MainPlayer;
            public long MainPlayerId => source.MainPlayer.Id;
            public bool ManagerAvailable => true;
            public bool MainPlayerAvailable => true;
            public bool RelationshipAvailable => false;
            public SafeAction OnPlayerUpdate => source.OnPlayerUpdate;
            public PlayerInfoData BuildData(IProfessionInfoPlayer player) => source.BuildData(player);
            public PlayerInfoData BuildLiteData(object info, IProfessionInfoTarget target)
            {
                if (target == null || target.TargetId != MainPlayerId || target.TargetType != ActorTargetType.Player)
                    throw new InvalidOperationException("Only the selected local player has a saved player-info record.");
                if (info is PlayerInfoData data && data.TargetData.TargetId == MainPlayerId) return data;
                return BuildData(MainPlayer);
            }
            public object BuildBriefInfo(IProfessionInfoPlayer player, bool online) => BuildData(player);
            public object CreateNameArg(string name, bool translate) => new CultureTextArg(name, null, translate);
            public object CompactNumber(long value) => CultureNumberArg.Compact(value);
            public string SubRankKey(int rank)
                => "SubRank." + npcRuntime.SubRankName(rank);
            public string ProfessionNameKey(int profession)
            {
                if (!Enum.IsDefined(typeof(Zjcs.Official.ProfessionUI.Profession), profession))
                    throw new ArgumentOutOfRangeException(nameof(profession));
                return "profession_name_" + Enum.GetName(typeof(Zjcs.Official.ProfessionUI.Profession), profession);
            }
            // The recovered partner and dialogue icon prefabs use the local
            // 3D appearance widget, not PlayerInfoUI.PlayerFigure.
            public string GetFullBodyGuid(int profession, Sex sex)
                => throw new NotSupportedException("The selected partner icon prefab has no PlayerFigure binding.");
            public string FirstProfessionIcon(int profession) => npcRuntime.ProfessionIcon(profession, false);
            public string ThirdProfessionIcon(int profession) => npcRuntime.ProfessionIcon(profession, true);
            public ProfessionInfoText NameWithPlanes(object name, long server) => new ProfessionInfoText("{0}", name);
            public bool HasHonorTitle(object appearance)
                => appearance is AppearanceData data && data.HonorItemDict.ContainsKey(HonorType.Title);
            public ProfessionInfoNpcDefinition GetNpc(int classId)
                => new ProfessionInfoNpcDefinition { Id = classId, FigureModelUI = npcRuntime.FigureModelUI(classId) };
            public GameObject InstantiateKeepScale(GameObject prefab, Transform parent)
                => prefab ? UnityEngine.Object.Instantiate(prefab, parent, false) : null;
            public void OverrideNpcInitialAnimation(GameObject model, int npcId)
                => throw new NotSupportedException("The selected partner icon prefab has no NPC appearance slot.");
            public TeamTargetCatalog? TeamCatalog(Component component) => null;
            public ProfessionInfoRobotInvite RobotInvite(Component component) => null;
            public ProfessionInfoRobotInvite DefaultRobotInvite() => null;
            public bool CanJointTogether(int targetProfession, int mainProfession) => false;
            public int DuelSkillGroup(long player, long target) => 0;
            public void Popup(Component owner, Action<IProfessionPlayerInfoDetailPanel> initialized)
            {
                initialized?.Invoke(new PlayerDetailPanel(openPlayer));
            }

            public bool IsFriend(long playerId) => false;
            public bool? TeamPlayerOnline(long playerId) => null;
            public event Action<long> TeamPlayerOffline { add { } remove { } }
            public event Action<long> TeamPlayerOnlineChanged { add { } remove { } }
            public event Action<long> RelationAdded { add { } remove { } }
            public event Action<long> RelationRemoved { add { } remove { } }
            public Task<object> GetLiteInfo(IProfessionInfoTarget target, CancellationToken cancellation, bool latest = false)
            {
                cancellation.ThrowIfCancellationRequested();
                if (target == null || target.TargetType != ActorTargetType.Player || target.TargetId != MainPlayerId)
                    throw new InvalidOperationException("Only the selected local player can be loaded offline.");
                return Task.FromResult<object>(BuildData(MainPlayer));
            }
            public long LiteInfoId(object info)
                => info is PlayerInfoData data ? data.Id : throw new InvalidOperationException("Unknown local player lite-info type.");
            public Task WaitForSeconds(float seconds, CancellationToken cancellation)
                => Task.Delay(TimeSpan.FromSeconds(Math.Max(0f, seconds)), cancellation);
            public Task<object> GetPlayerBriefInfo(long playerId)
                => playerId == MainPlayerId ? Task.FromResult<object>(BuildData(MainPlayer)) : Task.FromResult<object>(null);
            public void PopupNpc(Component owner, IProfessionInfoNpcTarget target)
            {
                if (target == null) throw new ArgumentNullException(nameof(target));
                openNpc(target.NpcClassId);
            }
            public void PopupPlayer(Component owner, object briefInfo, int popupScene, int skillGroup) => openPlayer();

            private sealed class PlayerDetailPanel : IProfessionPlayerInfoDetailPanel
            {
                private readonly Action open;
                internal PlayerDetailPanel(Action open) { this.open = open; }
                public void Init(object briefInfo, int popupScene, int skillPlanUsedPos) => open();
            }
        }

        private sealed class TextureRegistration : IDisposable
        {
            internal readonly IProfessionAppearanceTextureBindings Previous;
            internal TextureRegistration(IProfessionAppearanceTextureBindings previous) { Previous = previous; }
            public void Dispose() { }
        }
    }
}
