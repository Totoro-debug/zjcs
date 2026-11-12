using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.HomeMainPanelUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeResultUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeSocialUiControls;
using Zjcs.Official.HomeTaskUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;
using ShowRemainTimeInfo = Zjcs.Official.HomeTaskUI.ShowRemainTimeInfo;
using SafeArea = Zjcs.Official.HomeScreenUI.SafeArea;
using HomePanel = Zjcs.Official.HomeMainPanelUI.HomeUI;
using TaskTipDotObject = Zjcs.Official.HomeMainUiControls.TipDotObject;

namespace Zjcs.Offline.HomeHudUI
{
    public sealed class HomeHudBindings
    {
        public Camera Camera;
        public ITopBarManager TopBar;
        public IHomeMainUiConditions Conditions;
        public IHomeMainUiInput Input;
        public IHomeMainUiWindows Windows;
        public IHomeHudRightEntryServices RightEntries;
        public IHomeHudDownloadServices Download;
        public IHomeSceneUiServices Scene;
        public IHomeMatchServices Match;
        public IHomeResultServices Results;
        public IHomeResultFightRoot FightRoot;
        public IHomeTaskServices Tasks;
        public IProfessionTipDotSystem TipDots;
        public IProfessionTipDotSystemController TipController;
        public Func<string, string> Localize;
        public Action<AudioClip, GameObject> PostSound;

        internal void Validate()
        {
            if (!Camera || TopBar == null || Conditions == null || Input == null || Windows == null
                || RightEntries == null || Download == null || Scene == null || Match == null
                || Results == null || FightRoot == null || Tasks == null || TipDots == null
                || TipController == null || Localize == null || PostSound == null)
                throw new ArgumentException("Complete local services must be bound before original HomeUI activation.");
        }
    }

    // Owns the exact original hierarchy. The profile host supplies transactions,
    // conditions and navigation; this class only binds dependencies/lifetimes.
    public sealed class OfflineHomeHud : IDisposable
    {
        private readonly HomeHudBindings bindings;
        private readonly OfflineHomeHudAssets assets;
        private readonly ScreenAdaptService screen = new ScreenAdaptService();
        private FontAssetFontService fonts;
        private IDisposable uiScope;
        private OfflinePartnerUiSoundScope sound;
        public GameObject Root { get; private set; }
        public HomePanel Home { get; private set; }
        public Transform PublicSlot { get; private set; }

        public OfflineHomeHud(Transform parent, HomeHudBindings bindings, OfflineHomeHudAssets assets = null)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            bindings.Validate();
            this.assets = assets ? assets : OfflineHomeHudAssets.LoadDefault();
            var staging = new GameObject("HomeHudDependencyBinding");
            staging.SetActive(false);
            try
            {
                screen.Initialize();
                fonts = this.assets.CreateFontService(); fonts.Initialize();
                sound = new OfflinePartnerUiSoundScope(this.assets.Sound);
                Root = Object.Instantiate(this.assets.framePrefab, staging.transform, false);
                Root.SetActive(false);
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = bindings.Camera;
                foreach (var nested in Root.GetComponentsInChildren<Canvas>(true)) nested.worldCamera = bindings.Camera;
                Root.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService = screen;
                var homeSlot = Root.transform.Find("Content/Main/Anim");
                PublicSlot = Root.transform.Find("Content/Main/Public/Anim");
                if (!homeSlot || !PublicSlot) throw new InvalidOperationException("Original HUD ancestors are unavailable.");
                var home = Object.Instantiate(this.assets.homePrefab, homeSlot, false);
                home.SetActive(false);
                Home = home.GetComponent<HomePanel>();
                if (!Home) throw new InvalidOperationException("Original HomeUI script binding is unavailable.");
                uiScope = OfflineUiBindingModule.Register(Root.transform, new OfflineUiBindings {
                    Localize = bindings.Localize, Font = () => this.assets.defaultFont
                });
                BindDependencies(Root);
                Root.transform.SetParent(parent, false);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        // Public avatar/bottom-bar owners attach to PublicSlot before Show.
        // The original panel owns its animator wait and final active state.
        public UniTask Show(CancellationToken cancellationToken)
        {
            if (!Root) throw new ObjectDisposedException(nameof(OfflineHomeHud));
            Root.SetActive(true);
            return Home.Show(cancellationToken);
        }
        public UniTask Hide(CancellationToken cancellationToken)
        {
            if (!Root) throw new ObjectDisposedException(nameof(OfflineHomeHud));
            return Home.Hide(cancellationToken);
        }

        public TaskListItem InstantiateTaskItem(GameObject template, Transform parent)
        {
            if (!Root) throw new ObjectDisposedException(nameof(OfflineHomeHud));
            var staging = new GameObject("HomeTaskDependencyBinding"); staging.SetActive(false);
            try
            {
                var instance = Object.Instantiate(template, staging.transform, false);
                BindDependencies(instance);
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                return instance.GetComponent<TaskListItem>();
            }
            finally { Object.Destroy(staging); }
        }

        private void BindDependencies(GameObject root)
        {
            foreach (var safe in root.GetComponentsInChildren<SafeArea>(true)) safe.ScreenAdaptService = screen;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.font) fonts.SetupFont(text.font);
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = bindings.Localize;
            foreach (var button in root.GetComponentsInChildren<WorldButtonSound>(true)) button.Post = bindings.PostSound;
            foreach (var handler in root.GetComponentsInChildren<TopBarHandler>(true)) handler.Bind(bindings.TopBar);
            foreach (var controller in root.GetComponentsInChildren<ChatQuickMsgEntryController>(true)) controller.Services = bindings.Conditions;
            foreach (var controller in root.GetComponentsInChildren<MainTaskFunctionLimit>(true)) controller.Services = bindings.Conditions;
            foreach (var controller in root.GetComponentsInChildren<FunctionEntrysPanelBase>(true)) {
                controller.InputOperationModule = bindings.Input; controller.Windows = bindings.Windows; controller.UiCamera = bindings.Camera;
            }
            foreach (var controller in root.GetComponentsInChildren<HomeUIActivityEntryController>(true)) controller.Services = bindings.RightEntries;
            foreach (var controller in root.GetComponentsInChildren<PlayAsYouDownloadEntry>(true)) controller.Services = bindings.Download;
            foreach (var controller in root.GetComponentsInChildren<AUIDirectionGuider>(true)) controller.Services = bindings.Scene;
            foreach (var controller in root.GetComponentsInChildren<HomeScreenShootActive>(true)) controller.Services = bindings.Scene;
            foreach (var controller in root.GetComponentsInChildren<GameMatchMiniPanel>(true)) controller.Services = bindings.Match;
            foreach (var controller in root.GetComponentsInChildren<GameEndUIHandlerInHome>(true)) controller.Services = bindings.Results;
            foreach (var controller in root.GetComponentsInChildren<GameEndNotifyUI>(true)) controller.Services = bindings.Results;
            foreach (var controller in root.GetComponentsInChildren<FightDemonBattlefieldAutoPushEndNotifyUI>(true)) controller.Services = bindings.Results;
            foreach (var controller in root.GetComponentsInChildren<GameCurrentStageText>(true)) controller.Services = bindings.Results;
            foreach (var controller in root.GetComponentsInChildren<GameUIBase>(true)) controller.mFightRoot = bindings.FightRoot;
            foreach (var controller in root.GetComponentsInChildren<PinnedTaskPanel>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<TaskListItem>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<TaskGoal>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<TaskConditionItemBase>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<Zjcs.Official.HomeTaskUI.ShowRemainTimeInfo>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<TaskListRecommendCombatRatingItem>(true)) controller.Services = bindings.Tasks;
            foreach (var controller in root.GetComponentsInChildren<UIColorByLevel>(true)) controller.Services = bindings.Tasks;
            foreach (var dot in root.GetComponentsInChildren<TaskTipDotObject>(true)) dot.TipDotSystem = bindings.TipDots;
            foreach (var dot in root.GetComponentsInChildren<TipDotNodeLinker>(true)) {
                dot.TipDotSystem = bindings.TipDots; dot.Controller = bindings.TipController;
            }
        }

        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; }
            Home = null; PublicSlot = null;
            uiScope?.Dispose(); uiScope = null;
            sound?.Dispose(); sound = null;
            fonts?.Dispose(); fonts = null;
            screen.Dispose();
        }
    }
}
