using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;
using WikiEntry = Zjcs.Official.HomeFunctionMenuUI.WikiEntry;
using Zjcs.Official.HomeScreenUI;

namespace Zjcs.Offline.HomeFunctionMenuUI
{
    public sealed class HomeFunctionMenuBindings
    {
        public Camera Camera;
        public IScreenAdaptService Screen;
        public IHomeFunctionMenuServices Menu;
        public IHomeMenuActivityServices Activities;
        public IHomeMenuWikiServices Wiki;
        public IHomeMenuPredownloadServices Download;
        public IHomeMenuTimeServices Time;
        public IProfessionTipDotSystem TipDots;
        public IProfessionTipDotSystemController TipController;
        public TMP_FontAsset Font;
        public Action<AudioClip, GameObject> PostSound;

        internal void Validate()
        {
            if (!Camera || Screen == null || Menu == null || Activities == null || Wiki == null || Download == null
                || Time == null || !Font || PostSound == null)
                throw new ArgumentException("Complete local function menu bindings are required.");
        }
    }

    // Owns the recovered hierarchy and window lifetime. Feature rules and
    // navigation remain with their local modules, through the original ports.
    public sealed class OfflineHomeFunctionMenu : IHomeMainFunctionEntryDialog, IProfessionWindowService, IDisposable
    {
        private readonly Dictionary<Action, IDisposable> observers = new Dictionary<Action, IDisposable>();
        private IDisposable uiScope;
        private OfflinePartnerUiSoundScope sound;
        private bool closeButtonsBound;
        public GameObject Root { get; private set; }
        public FunctionEntryDialog Dialog { get; private set; }
        public WorldPopupWindow Popup { get; private set; }
        public bool IsOpen => Root && Root.activeSelf;

        public OfflineHomeFunctionMenu(Transform parent, HomeFunctionMenuBindings bindings,
            OfflineHomeFunctionMenuAssets assets = null)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            bindings.Validate();
            assets = assets ? assets : OfflineHomeFunctionMenuAssets.LoadDefault();
            var staging = new GameObject("HomeFunctionMenuDependencyBinding", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
            var stagingRect = (RectTransform)staging.transform;
            stagingRect.anchorMin = Vector2.zero; stagingRect.anchorMax = Vector2.one;
            stagingRect.offsetMin = stagingRect.offsetMax = Vector2.zero;
            try
            {
                Root = Object.Instantiate(assets.prefab, staging.transform, false);
                Root.SetActive(false);
                Dialog = Root.GetComponent<FunctionEntryDialog>();
                Popup = Root.GetComponent<WorldPopupWindow>();
                if (!Dialog || !Popup) throw new InvalidOperationException("Original function menu components are unavailable.");
                foreach (var canvas in Root.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = bindings.Camera;
                foreach (var area in Root.GetComponentsInChildren<SafeArea>(true)) area.ScreenAdaptService = bindings.Screen;
                sound = new OfflinePartnerUiSoundScope(assets.Sound);
                uiScope = OfflineUiBindingModule.Register(Root.transform, new OfflineUiBindings {
                    Localize = assets.Text, Font = () => bindings.Font });
                foreach (var text in Root.GetComponentsInChildren<TMP_Text>(true)) text.font = bindings.Font;
                foreach (var text in Root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = assets.Text;
                foreach (var node in Root.GetComponentsInChildren<WorldButtonSound>(true)) node.Post = bindings.PostSound;
                foreach (var node in Root.GetComponentsInChildren<WorldPopupWindowSound>(true)) node.Post = bindings.PostSound;
                foreach (var node in Root.GetComponentsInChildren<FunctionEntryBtnBase>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<AchievementGroupBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<CollectBookBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<FriendBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<FunctionEntryTaskBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<HomeSkinBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<LocalRecordBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<ScreenShotEntry>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<SettingsBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<ShopEntryBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<SocialBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<TopListBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<UserBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<AnnounceBtn>(true)) node.Services = bindings.Menu;
                foreach (var node in Root.GetComponentsInChildren<FunctionEntryDialog>(true)) node.Services = bindings.Activities;
                foreach (var node in Root.GetComponentsInChildren<ActivityRecommendItemView>(true)) node.Services = bindings.Activities;
                foreach (var node in Root.GetComponentsInChildren<WikiEntry>(true)) node.Services = bindings.Wiki;
                foreach (var node in Root.GetComponentsInChildren<PredownloadEntry>(true)) node.Services = bindings.Download;
                foreach (var node in Root.GetComponentsInChildren<TimeInfoUI>(true)) node.Services = bindings.Time;
                foreach (var node in Root.GetComponentsInChildren<TipDotNodeLinker>(true)) {
                    node.TipDotSystem = bindings.TipDots; node.Controller = bindings.TipController;
                }
                Root.transform.SetParent(parent, false);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        public void Init()
        {
            if (!Root) throw new ObjectDisposedException(nameof(OfflineHomeFunctionMenu));
            Root.SetActive(true);
            // Awake resolves each serialized Parent/Specified close target.
            if (!closeButtonsBound)
            {
                foreach (var node in Root.GetComponentsInChildren<CloseWindowBtn>(true)) node.Init(this);
                closeButtonsBound = true;
            }
            Dialog.Init();
            Popup.Show();
        }
        public void CloseLast() => Close(Root);
        public void Close(GameObject window)
        {
            if (!Root) throw new ObjectDisposedException(nameof(OfflineHomeFunctionMenu));
            if (window != Root) throw new ArgumentException("The function menu can only close its own window.", nameof(window));
            if (Root.activeSelf) Popup.Hide(null);
        }
        public void ObserveDestroyed(WorldPopupWindow window, Action callback)
        {
            if (window != Popup) throw new ArgumentException("The function menu can only observe its own window.", nameof(window));
            observers.Add(callback, window.OnWindowDestory.Subscribe(callback));
        }
        public void StopObservingDestroyed(WorldPopupWindow window, Action callback)
        {
            if (!observers.TryGetValue(callback, out var subscription)) return;
            observers.Remove(callback); subscription.Dispose();
        }
        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; }
            foreach (var subscription in observers.Values) subscription.Dispose();
            observers.Clear();
            Dialog = null; Popup = null;
            uiScope?.Dispose(); uiScope = null;
            sound?.Dispose(); sound = null;
        }
    }
}
