using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Offline.NativeSpine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;
using GAction = Zjcs.Official.StoryPlayables.GAction;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        NativeStorage storage;

        public GameObject NativeStorageInstance => storage?.Instance;
        public IStoryNodeRegisterMgr NativeStorageStoryNodes => storage?.StoryNodes;
        public void RefreshNativeFurnitureSkins() => storage?.RefreshSkin();
        public void SetHomeViewMode(HomeViewMode mode) => storage?.SetViewMode(mode);
        public void PublishHomeActionEvent(ActionEventData data) => storage?.PublishActionEvent(data);

        void OpenNativeStorage()
        {
            var owner = new NativeStorage(this);
            try
            {
                owner.Open();
                storage = owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        void CloseNativeStorage()
        {
            var owner = storage;
            storage = null;
            owner?.Dispose();
        }

        sealed class NativeStorage : IHomeFurnitureServices, IHomeFurniturePlayer,
            IHomeFurnitureViewModes, IHomeFurnitureActionEvents, IHomeWeaponAnimationBindings,
            IHomeEntityManagerContext, IHomeEntitySkinData, IHomeFurnitureChangePublisher, IDisposable
        {
            readonly HomeFeatureRuntime home;
            readonly HomeNativePrefabLoader loader = new HomeNativePrefabLoader();
            readonly StoryNodeRegisterMgr storyNodes = new StoryNodeRegisterMgr();
            readonly GAction changed = new GAction();
            readonly GAction<HomeViewMode> viewModes = new GAction<HomeViewMode>();
            readonly GAction<ActionEventData> actions = new GAction<ActionEventData>();
            readonly HashSet<Func<HomeFurnitureAward, bool>> awardHandlers = new HashSet<Func<HomeFurnitureAward, bool>>();
            readonly List<AnimationClipPlayable> clips = new List<AnimationClipPlayable>();
            AppearanceAssetManager materials;
            NativeSpineAppearanceBindings textures;
            HomeNativeWeaponRackServices rackServices;
            OfflineProfessionAppearanceBindings appearance;
            OfflinePlayerProgress renderedPlayer;
            string renderedWardrobe;
            HomeNativePrefabFactory factory;
            HomeEntityCreationCatalog tables;
            HomeEntityPrefabPaths paths;
            FurnitureEntityCreator creator;
            GameObject anchor;
            GameObject instance;
            AudioSource clickSound;
            PlayableGraph timeline;
            HomeViewMode currentViewMode = HomeViewMode.Owner;
            bool disposed;

            internal NativeStorage(HomeFeatureRuntime home) => this.home = home;
            internal GameObject Instance => instance;
            internal IStoryNodeRegisterMgr StoryNodes => storyNodes;

            internal void Open()
            {
                var assets = Resources.Load<HomeNativeStorageAssets>("Home/NativeStorageAssets");
                if (!assets || !assets.WeaponAppearance)
                    throw new InvalidOperationException("The original Home weapon appearance catalog is missing.");
                var control = assets.WeaponAppearance.Config;
                var professionTables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                var defaults = new ProfessionAppearanceSkinDefaults(professionTables,
                    Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, Debug.LogError);
                var animations = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
                var config = Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config");
                renderedPlayer = home.profile.Save.Player;
                renderedWardrobe = JsonUtility.ToJson(renderedPlayer.Wardrobe);
                appearance = new OfflineProfessionAppearanceBindings(renderedPlayer.CharacterProperties.LocalActorIdentities.PlayerId,
                    () => home.profile.Save.Player, id => home.progression.Catalog.ItemType(id), professionTables, control, animations, defaults);
                materials = new AppearanceAssetManager(config, changed.Subscribe);
                textures = new NativeSpineAppearanceBindings(assets.WeaponAppearance, materials);
                rackServices = new HomeNativeWeaponRackServices(() => appearance, () => appearance,
                    new ProfessionAppearanceResolver(professionTables), professionTables, changed.Subscribe, textures, this);
                factory = new HomeNativePrefabFactory(Install);
                tables = HomeEntityCreationCatalog.Load();
                paths = new HomeEntityPrefabPaths(tables);
                using var house = loader.LoadPrefab(paths.GetThemePrefabPath(home.rules.Data.themeSkins
                    .Single(value => value.isDefault && value.themeType == (int)HomeThemeType.House).skinItemId));
                var housePrefab = house.Result;
                if (!housePrefab || !housePrefab.TryGetComponent<HomeSlotEntityData>(out var roomSlots)
                    || !roomSlots.FurnitureDict.TryGetValue(EFurniture.StorageBox, out var slot)
                    || !slot || !slot.IsChildOf(housePrefab.transform))
                    throw new InvalidOperationException("The official Room has no StorageBox slot.");
                // The current room composite exposes the original theme coordinate space.
                // Project the authored transform chain until the native house owns this slot.
                anchor = new GameObject("NativeIndoorFurniture");
                anchor.transform.SetParent(home.scene.Root.transform, false);
                var projectedSlot = ProjectSlot(slot, housePrefab.transform, anchor.transform);
                creator = new FurnitureEntityCreator(
                    new Dictionary<EFurniture, Transform> { { EFurniture.StorageBox, projectedSlot } },
                    null);
                if (!creator.GetOrCreate(EFurniture.StorageBox, this))
                    throw new InvalidOperationException("The original StorageBox prefab is missing.");
            }

            internal void RefreshSkin()
            {
                if (disposed || creator == null) return;
                creator.OnSkinChanged(EFurniture.StorageBox, GetFurnitureSkinId(EFurniture.StorageBox), this, out _);
            }

            internal void SetViewMode(HomeViewMode mode)
            {
                currentViewMode = mode;
                viewModes.Invoke(mode);
            }

            internal void PublishActionEvent(ActionEventData data) => actions.Invoke(data);

            static Transform ProjectSlot(Transform slot, Transform house, Transform parent)
            {
                if (slot != house) parent = ProjectSlot(slot.parent, house, parent);
                var node = new GameObject(slot.name).transform;
                node.SetParent(parent, false);
                node.SetLocalPositionAndRotation(slot.localPosition, slot.localRotation);
                node.localScale = slot.localScale;
                return node;
            }

            void Install(GameObject root)
            {
                var furniture = root.GetComponent<StoreBoxFurniture>();
                furniture.Services = this;
                furniture.OnClickAction.AddListener(() => home.FurnitureClickedForTutorial?.Invoke((int)EFurniture.StorageBox));
                home.nativeSceneInput.Bind(furniture);
                foreach (var notice in root.GetComponentsInChildren<HomeNotice>(true)) notice.Services = this;
                foreach (var rack in root.GetComponentsInChildren<HomeWeaponRack>(true)) rack.Services = rackServices;
                foreach (var mode in root.GetComponentsInChildren<HomeViewModeControl>(true)) mode.ViewModes = this;
                foreach (var action in root.GetComponentsInChildren<HomeActionEventHandler>(true)) action.ActionEvents = this;
                foreach (var node in root.GetComponentsInChildren<Zjcs.Official.StoryNodes.StoryNodeRegister>(true))
                    node.NodeRegisterManager = storyNodes;
                clickSound = root.AddComponent<AudioSource>();
                clickSound.playOnAwake = false;
                HomeAudioSettings.Register(clickSound, HomeAudioChannel.Sound);
                foreach (var sound in root.GetComponentsInChildren<EntityClickSound>(true))
                {
                    sound.InputOperationModule = home.nativeSceneInput.InputModule;
                    sound.Post = (clip, _) => clickSound.PlayOneShot(clip);
                }
                home.BindBagTipNativeTree(root);
            }

            void SetupTimeline()
            {
                if (timeline.IsValid()) timeline.Destroy();
                clips.Clear();
                timeline = PlayableGraph.Create("HomeFurnitureClips");
                timeline.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                foreach (var binding in instance.GetComponent<HomePlayableClipBind>().ClipDatas)
                {
                    var clip = AnimationClipPlayable.Create(timeline, binding.Clip);
                    clip.SetSpeed(0);
                    var output = AnimationPlayableOutput.Create(timeline, binding.Clip.name, binding.Animator);
                    output.SetSourcePlayable(clip);
                    clips.Add(clip);
                }
                timeline.Play();
            }

            internal void Tick(float dayPhase)
            {
                var next = home.profile.Save.Player;
                if (!ReferenceEquals(renderedPlayer, next))
                {
                    renderedPlayer = next;
                    string wardrobe = JsonUtility.ToJson(next.Wardrobe);
                    if (wardrobe != renderedWardrobe)
                    {
                        renderedWardrobe = wardrobe;
                        appearance.Refresh(AppearanceOperateSource.Normal);
                    }
                }
                if (!instance || !instance.activeInHierarchy) return;
                foreach (var clip in clips) clip.SetTime(dayPhase * clip.GetAnimationClip().length);
                timeline.Evaluate();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                creator?.ReleaseAll(this);
                if (timeline.IsValid()) timeline.Destroy();
                clips.Clear();
                HomeAudioSettings.Unregister(clickSound);
                if (anchor) { anchor.SetActive(false); Object.Destroy(anchor); }
                ReleaseAfterInstances(anchor).Forget();
            }

            async UniTask ReleaseAfterInstances(GameObject root)
            {
                while (root) await UniTask.NextFrame();
                factory?.Dispose();
                rackServices?.Dispose();
                textures?.Dispose();
                materials?.Dispose();
                loader.Dispose();
            }

            public IHomeEntityResourceLoader ResourceLoader => loader;
            public IHomeFurnitureChangePublisher FurnitureChangePublisher => this;
            IHomeEntitySkinData IHomeEntityManagerContext.PlayerData => this;
            public IHomeEntityPrefabTables Tables => tables;
            public HomeEntityPrefabPaths PrefabPaths => paths;
            public int PreviewWeatherId => 0;
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
                => factory.Instantiate(prefab, parent, worldPositionStays);
            public void OnEntityWillDestroy(GameObject go)
            {
                if (timeline.IsValid()) timeline.Destroy();
                clips.Clear();
                HomeAudioSettings.Unregister(clickSound);
                clickSound = null;
                if (instance == go) instance = null;
            }
            public void OnEntityCreated(GameObject go)
            {
                instance = go;
                SetupTimeline();
                home.scene.BindNativeFurniture((int)EFurniture.StorageBox, instance);
            }
            public int GetFurnitureSkinId(EFurniture type)
            {
                var defaultSkin = home.rules.Data.furnitureSkins.First(value => value.furnitureType == (int)type && value.isDefault);
                return home.session.SkinOf(0, (int)type)?.SkinItemId ?? defaultSkin.skinItemId;
            }
            public int GetThemeSkinId(HomeThemeType type)
            {
                var defaultSkin = home.rules.Data.themeSkins.First(value => value.themeType == (int)type && value.isDefault);
                return home.session.SkinOf(1, (int)type)?.SkinItemId ?? defaultSkin.skinItemId;
            }
            public int GetEnvironmentSkinId(HomeEnvironmentType type)
            {
                var defaultSkin = home.rules.Data.environmentSkins.First(value => value.environmentType == (int)type && value.isDefault);
                return home.session.SkinOf(2, (int)type)?.SkinItemId ?? defaultSkin.skinItemId;
            }
            public void Publish(HomeFurnitureChangeData data) => changed.Invoke();

            public bool InMyHome => true;
            public IHomeFurniturePlayer Player => this;
            public IHomeFurniturePlayerData PlayerData => this;
            public HomeViewMode HomeViewMode => currentViewMode;
            public IDisposable Subscribe(Action<HomeViewMode> action) => viewModes.Subscribe(action);
            public IDisposable Subscribe(Action<ActionEventData> action) => actions.Subscribe(action);
            public bool HasFurniture(EFurniture furniture) => home.HasFurnitureForSkin((int)furniture);
            public bool CheckFunctionDisabled(EFurniture furniture) => home.hud.IsClosedByNetworkPolicy((int)furniture);
            public void PopupPlayerBag() => home.hud.ShowStorage();
            public void PopupRepair(EFurniture furniture, bool isBuild) => home.hud.ShowRepair((int)furniture);
            public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) => awardHandlers.Add(handler);
            public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) => awardHandlers.Remove(handler);
            public IDisposable SubscribePostHomeChange(Action action) => changed.Subscribe(action);
            public void ShowError(int textId) => home.hud.ShowNotice(home.catalog.LocalizedText("TextError." + textId, textId.ToString()));
            public void SystemMessage(string message) => home.hud.ShowNotice(message);
            public bool HasStartedStory(string name) => home.StoryStatus(name) > 0;
            public void DoStoryAction(Action action) => action();
            public FurnitureInfo GetFurnitureInfo(EFurniture furniture)
            {
                var row = home.rules.FurnitureOf((int)furniture);
                if (!string.IsNullOrEmpty(row.repairStory)) throw UnexpectedRepair();
                return new FurnitureInfo();
            }
            static InvalidOperationException UnexpectedRepair()
                => new InvalidOperationException("Default storage has no repair condition or story in the original configuration.");
            public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => throw UnexpectedRepair();
            public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => throw UnexpectedRepair();
            public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action) => throw UnexpectedRepair();
            public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => throw UnexpectedRepair();
            public IHomeFurnitureStoryContext CreateStoryContext(string owner) => throw UnexpectedRepair();
            public void PlayStory(IHomeFurnitureStoryContext context, string name) => throw UnexpectedRepair();
            public void DisposeStory(IHomeFurnitureStoryContext context) => throw UnexpectedRepair();
            public event Action<EFurniture> OnRepairFurniture { add { } remove { } }
            public event Action<long> OnDebugTimeOffset { add { } remove { } }

            public Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string name)
                => component.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(name);
            public void UpdateAnimation(SkeletonRenderer renderer)
                => renderer.GetComponent<NativeSpineAppearanceAdapter>().UpdateAnimationAndMesh();
            public void UpdateAnimation(SkeletonGraphic graphic) { graphic.ApplyAnimation(); graphic.UpdateMesh(); }
        }
    }
}
