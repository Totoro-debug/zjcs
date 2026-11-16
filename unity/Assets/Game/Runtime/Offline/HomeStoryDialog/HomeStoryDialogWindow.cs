using System;
using Common;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeStory;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeStoryDialog
{
    // Local window ownership around the complete original dialogue and mask.
    public sealed class HomeStoryDialogWindow : IDisposable, IHomeStoryDialogEnvironment
    {
        private readonly Canvas canvas;
        private readonly HomeStoryDialogAssets assets;
        private readonly HomeStoryPortraitAssets portraits;
        private readonly IHomeStoryDialogEnvironment environment;
        private readonly Action<AudioClip, GameObject> postSound;
        private readonly TMP_FontAsset font;
        private GameObject root, mask;
        private bool closing, disposed;

        public HomeStoryDialogServices Services { get; }
        public GameObject Root => root;
        public event Action Closed;

        public HomeStoryDialogWindow(Canvas canvas, HomeStoryDialogAssets assets,
            HomeStoryPortraitAssets portraits, HomeStoryOptionAssets options, HomeStorySettings settings,
            TMP_FontAsset font, IHomeStoryDialogEnvironment environment, Action<AudioClip, GameObject> postSound)
        {
            this.canvas = canvas ? canvas : throw new ArgumentNullException(nameof(canvas));
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.portraits = portraits ? portraits : throw new ArgumentNullException(nameof(portraits));
            this.font = font ? font : throw new ArgumentNullException(nameof(font));
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.postSound = postSound ?? throw new ArgumentNullException(nameof(postSound));
            Services = new HomeStoryDialogServices(assets, portraits, options, settings, font, this);
        }

        public void Open(Action<OptionTalkDialogPanel> initialize)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HomeStoryDialogWindow));
            if (initialize == null) throw new ArgumentNullException(nameof(initialize));
            if (root)
            {
                if (closing) throw new InvalidOperationException("The original story dialogue is still closing.");
                initialize(root.GetComponent<OptionTalkDialogPanel>());
                return;
            }
            var staging = new GameObject("HomeStoryDialogStaging", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
            try
            {
                root = Object.Instantiate(assets.OptionTalkDialog, staging.transform, false);
                root.AddComponent<HomeStoryDialogScope>().Services = Services;
                root.AddComponent<HomeHudResourceScope>().Loader = portraits;
                foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                {
                    text.Localizer = Services.Localize;
                    text.FontProvider = () => font;
                }
                foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = postSound;
                foreach (var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = postSound;
                root.SetActive(false);
                root.transform.SetParent(canvas.transform, false);
                root.SetActive(true);
                var popup = root.GetComponent<WorldPopupWindow>();
                var panel = root.GetComponent<OptionTalkDialogPanel>();
                if (!popup || !panel) throw new InvalidOperationException("The original dialogue controllers are missing.");
                initialize(panel);
                int order = canvas.sortingOrder;
                foreach (var current in canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(root.transform))
                        order = Math.Max(order, current.sortingOrder);
                popup.SetCanvasSortingOrder(order + 10);
                mask = Object.Instantiate(assets.InvisibleMask, staging.transform, false);
                mask.SetActive(false);
                mask.transform.SetParent(canvas.transform, false);
                var originalMask = mask.GetComponent<WorldWindowMask>();
                if (!originalMask || originalMask.MaskType != 2)
                    throw new InvalidOperationException("The original invisible window mask is missing.");
                originalMask.AttachToWindow(popup);
                popup.Show();
            }
            catch { DestroyWindow(); throw; }
            finally { Object.Destroy(staging); }
        }

        public void Close()
        {
            if (!root || closing) return;
            closing = true;
            root.GetComponent<WorldPopupWindow>().Hide(() => { DestroyWindow(); Closed?.Invoke(); });
        }

        private void DestroyWindow()
        {
            if (mask)
            {
                var originalMask = mask.GetComponent<WorldWindowMask>();
                if (originalMask) originalMask.UnattachToWindow();
                Object.Destroy(mask);
                mask = null;
            }
            if (root)
            {
                root.SetActive(false);
                Object.Destroy(root);
                root = null;
            }
            closing = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DestroyWindow();
        }

        string IHomeStoryDialogEnvironment.PlayerName => environment.PlayerName;
        int IHomeStoryDialogEnvironment.PlayerRoleId => environment.PlayerRoleId;
        RegionCodeEnum IHomeStoryDialogEnvironment.Region() => environment.Region();
        DateTime IHomeStoryDialogEnvironment.Now() => environment.Now();
        bool IHomeStoryDialogEnvironment.IsConditionMet(int type, string parameters) => environment.IsConditionMet(type, parameters);
        bool IHomeStoryDialogEnvironment.ContainsText(string key) => environment.ContainsText(key);
        string IHomeStoryDialogEnvironment.Localize(string key) => environment.Localize(key);
        WorldPopupWindow IHomeStoryDialogEnvironment.LastWindowIgnoringTips() => environment.LastWindowIgnoringTips();
        bool IHomeStoryDialogEnvironment.ActivePanelHasSceneGraphic() => environment.ActivePanelHasSceneGraphic();
        void IHomeStoryDialogEnvironment.CloseWindow(GameObject window)
        {
            if (window == root) Close();
            else environment.CloseWindow(window);
        }
        void IHomeStoryDialogEnvironment.PostVoice(string key, AudioClip clip, GameObject emitter) => environment.PostVoice(key, clip, emitter);
        MyPlayable IHomeStoryDialogEnvironment.PlayWorldNpcPlayable(int roleId, string path)
            => environment.PlayWorldNpcPlayable(roleId, path);
        void IHomeStoryDialogEnvironment.ReleasePlayableResources() => environment.ReleasePlayableResources();
    }
}
