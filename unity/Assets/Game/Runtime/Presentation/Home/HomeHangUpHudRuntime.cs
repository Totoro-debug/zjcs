using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeHangUpUI;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.World.NativeCamera;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    // Scene-owned bindings for the original bed/cart controllers. The reward
    // retrieval NPC has a separate owner which is not installed here.
    public sealed class HomeHangUpHudRuntime : IHomeSceneUiServices,
        IHomeDirectionCameraServices, IHomeDirectionFocusController, IDisposable
    {
        readonly HomeFeatureRuntime host;
        readonly HomeSceneView scene;
        readonly OfflineHomeHangUpTipConfiguration configuration;
        readonly Dictionary<EFurniture, Furniture> furniture = new Dictionary<EFurniture, Furniture>();
        readonly HomeHangUpTipController bed, cart;
        readonly string previousCameraTag;
        readonly OfflinePartnerUiSoundScope sound;
        Action<HangUpTipLevelChangeData> tips;
        Action<SetHomeSceneUIActiveData> active;
        Vector3 lastView;
        int lastLevel;
        bool disposed;
        public GameObject Root { get; private set; }
        public OfflineHomeHangUpTipSource Source { get; }

        public HomeHangUpHudRuntime(HomeFeatureRuntime host, Transform parent)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            scene = host.Scene;
            configuration = OfflineHomeHangUpTipConfiguration.LoadDefault();
            var experience = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
            Source = new OfflineHomeHangUpTipSource(host.Session, configuration,
                () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(), () => true,
                () => host.Profile.Save.Player.Level, () => host.CanGetHangUpExperience(experience),
                level => experience.LevelExpParser.GetLevelUpExp(1, level),
                host.GetHangUpQuickReceiveState, (type, level) => furniture[type].Tip = level,
                data => tips?.Invoke(data));
            previousCameraTag = scene.Camera.tag;
            GameObject staging = null;
            try
            {
                foreach (var type in new[] { EFurniture.Bed, EFurniture.Cart })
                {
                    var entry = host.Catalog.Scene.furniture.Single(value => value.type == (int)type);
                    if (entry.slot == null || entry.slot.Length != 3)
                        throw new InvalidOperationException("Original home furniture anchor is unavailable: " + type);
                    var anchor = new GameObject("HangUpDirectionAnchor_" + type).transform;
                    anchor.SetParent(scene.Root.transform, false);
                    anchor.position = new Vector3(entry.slot[0], entry.slot[1], entry.slot[2]);
                    furniture.Add(type, new Furniture(anchor));
                }
                bed = new HomeHangUpTipController(EFurniture.Bed, Source);
                cart = new HomeHangUpTipController(EFurniture.Cart, Source);
                bed.Enable(); cart.Enable(); bed.Start(); cart.Start();
                lastLevel = host.Profile.Save.Player.Level;
                lastView = scene.Position;
                staging = new GameObject("HomeHangUpDependencyBinding", typeof(RectTransform));
                staging.transform.SetParent(parent, false); staging.SetActive(false);
                var assets = OfflineHomeHudAssets.LoadDefault();
                sound = new OfflinePartnerUiSoundScope(assets.Sound);
                var prefab = assets.homePrefab.transform.Find("Content/Guider");
                if (!prefab) throw new InvalidOperationException("Original home Guider subtree is unavailable.");
                Root = Object.Instantiate(prefab.gameObject, staging.transform, false);
                Root.SetActive(false);
                foreach (var guide in Root.GetComponentsInChildren<AUIDirectionGuider>(true))
                {
                    guide.Services = this;
                    if (guide is AwardRetrieveNpcDirectionGuider) guide.gameObject.SetActive(false);
                }
                Root.GetComponent<HomeScreenShootActive>().Services = this;
                // The original Awake captures Camera.main. This scene owns its
                // camera; do not silently capture a portrait or another scene.
                scene.Camera.tag = "MainCamera";
                if (UnityEngine.Camera.main != scene.Camera)
                    throw new InvalidOperationException("Home direction guides require the actual home main camera.");
                Root.transform.SetParent(parent, false); Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { if (staging) Object.Destroy(staging); }
        }

        public int TipLevel(EFurniture type) => furniture[type].Tip;
        public void Tick()
        {
            if (disposed) return;
            var current = scene.Position;
            if (current != lastView) { lastView = current; ViewPosUpdated.Invoke(current); }
            int level = host.Profile.Save.Player.Level;
            if (level != lastLevel) { lastLevel = level; Source.NotifyLevelChanged(level); }
        }
        public void SetUiActive(bool value) => active?.Invoke(new SetHomeSceneUIActiveData { IsActive = value });
        public IHomeDirectionCameraServices Camera => this;
        public IDragControllerManager DragControllerManager { get; } = new DragControllerManager();
        public float DirectionGuideFocusDuration => configuration.directionGuideFocusDuration;
        public IHomeDirectionFurniture GetFurniture(EFurniture type) => furniture[type];
        public IDisposable SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback)
        { tips += callback; return new Subscription(() => tips -= callback); }
        public IDisposable SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback)
        { active += callback; return new Subscription(() => active -= callback); }
        public HomeViewMode HomeViewMode => HomeViewMode.Owner;
        public UnityEvent<Vector3> ViewPosUpdated { get; } = new UnityEvent<Vector3>();
        public Object ControlBrainIdentity => scene.Camera;
        public Vector3 CameraFollowPosition => scene.Position;
        public Vector3 GetViewPos() => scene.Position;
        public IHomeDirectionFocusController GetHomeController() => this;
        public void SetFocusPositionXForce(float x)
        {
            var position = scene.Position; position.x = x;
            scene.Camera.transform.position = scene.Clamp(position);
            lastView = scene.Position; ViewPosUpdated.Invoke(lastView);
        }

        // Keep unrelated NPC requests explicit while its original subtree is
        // inactive. Empty visitor records must not masquerade as a bound owner.
        static NotSupportedException NpcNotBound() => new NotSupportedException("The reward-retrieval NPC owner is not installed in the bed/cart HUD binding.");
        public UnityEvent OnHomeNpcRefresh => throw NpcNotBound();
        public List<IHomeDirectionNpcData> HomeNpcDatas => throw NpcNotBound();
        public int AwardRetrieveHomeNpcId => throw NpcNotBound();
        public Transform GetSpawnPoint(int index) => throw NpcNotBound();
        public HomeDirectionNpcInfo GetNpcInfo(int id) => throw NpcNotBound();
        public IHomeMenuPrefabHandle LoadPrefab(string path) => throw NpcNotBound();
        public GameObject InstantiatePrefab(GameObject prefab, Transform slot) => throw NpcNotBound();
        public void FreezeSkeletonGraphic(GameObject instance) => throw NpcNotBound();

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; }
            bed?.Dispose(); cart?.Dispose();
            foreach (var entry in furniture.Values) if (entry.transform) Object.Destroy(entry.transform.gameObject);
            if (scene.Camera) scene.Camera.tag = previousCameraTag;
            sound?.Dispose(); tips = null; active = null;
        }
        sealed class Furniture : IHomeDirectionFurniture
        {
            public Furniture(Transform anchor) { transform = anchor; }
            public int Tip = -1;
            public Object UnityIdentity => transform;
            public Transform transform { get; }
            public int TipLevel => Tip;
        }
        sealed class Subscription : IDisposable
        {
            Action release;
            public Subscription(Action action) { release = action; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
    }

    public sealed partial class HomeFeatureRuntime
    {
        private HomeHangUpHudRuntime hangUpHud;
        public HomeHangUpHudRuntime HangUpHud => hangUpHud;
        internal bool CanGetHangUpExperience(HomeExperienceTables tables)
        {
            var calculator = DailyDungeonProgression.PropertyCalculator;
            var snapshot = calculator.Evaluate(profile.Save.Player, profile.Save.World, Now());
            var player = new HomeExperiencePlayer(tables) {
                SubRank = (SubRank)profile.Save.Player.SubRank, Experience = profile.Save.Player.Experience,
                PlayerLevelLimitProperty = CharacterPropFormula.Value(snapshot.NonBattleProps,
                    calculator.Props.Id("PlayerLevelLimit"), calculator.Props)
            };
            // Only BaseLevel is consumed below; it is independent of season and
            // blessing records. TryRefreshLevel retains the official rank clamp.
            player.TryRefreshLevel();
            int maxLevelLimit = player.MaxLevelLimit;
            if (player.BaseLevel < maxLevelLimit)
            {
                long remaining = tables.LevelExpParser.GetExp(1, maxLevelLimit) - player.Experience;
                if (remaining < 0) remaining = 0;
                return Math.Min(remaining, 1L) > 0;
            }
            return false;
        }
        public bool GetHangUpQuickReceiveState(EFurniture type)
        {
            if (rules.IsFunctionLimited(164)) return true;
            var entry = session.Save.QuickReceiveStates.FirstOrDefault(value => value.Type == (int)type);
            return entry?.Enabled ?? true;
        }
        public bool SetHangUpQuickReceiveState(EFurniture type, bool enabled)
        {
            if (type != EFurniture.Bed && type != EFurniture.Cart) throw new ArgumentOutOfRangeException(nameof(type));
            return session.Mutate("home:quick-receive:" + Guid.NewGuid().ToString("N"), save => {
                var entry = save.QuickReceiveStates.FirstOrDefault(value => value.Type == (int)type);
                if (entry == null) save.QuickReceiveStates.Add(entry = new OfflineHomeQuickReceiveState { Type = (int)type });
                entry.Enabled = enabled;
            });
        }
        private void DisposeHangUpHud() { hangUpHud?.Dispose(); hangUpHud = null; }
    }
}
