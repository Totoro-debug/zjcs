using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    // Local ownership of the selected WindowContainer boundary. Every visible
    // object, animation, mask and interactive control comes from its real prefab.
    public sealed class OfflineHomeHangUpWindows : IProfessionWindowService, IDisposable
    {
        sealed class Window
        {
            internal GameObject Root, Mask;
            internal IDisposable Binding;
            internal Coroutine Preparation;
            internal bool Closing;
        }
        readonly Canvas canvas;
        readonly MonoBehaviour host;
        readonly GameObject blurPrefab;
        readonly IWorldWindowBlurManager blur;
        readonly IScreenAdaptService screen;
        readonly Action<AudioClip, GameObject> sound;
        readonly IHomeHangUpWindowServices services;
        readonly OfflineHomeFurnitureOutput output;
        readonly OfficialHomeHangUpWindowAssetCatalog assets;
        readonly FontAssetFontService fonts;
        readonly GameObject staging;
        readonly List<Window> windows = new List<Window>();
        bool disposed;
        public event Action<GameObject> ActiveWindowChanged;
        public event Action<GameObject> WindowOpened;
        public FurnitureBedDialog Bed { get; private set; }

        public OfflineHomeHangUpWindows(Canvas canvas, MonoBehaviour host,
            GameObject blurPrefab, IWorldWindowBlurManager blur, IScreenAdaptService screen,
            Action<AudioClip, GameObject> sound, IHomeHangUpWindowServices services,
            OfflineHomeFurnitureOutput output)
        {
            this.canvas = canvas ? canvas : throw new ArgumentNullException(nameof(canvas));
            this.host = host ? host : throw new ArgumentNullException(nameof(host));
            this.blurPrefab = blurPrefab ? blurPrefab : throw new ArgumentNullException(nameof(blurPrefab));
            this.blur = blur ?? throw new ArgumentNullException(nameof(blur));
            this.screen = screen ?? throw new ArgumentNullException(nameof(screen));
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            assets = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
            fonts = OfflineHomeHudAssets.LoadDefault().CreateFontService();
            fonts.Initialize();
            staging = new GameObject("HomeHangUpInactiveBinding", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
        }

        public FurnitureBedDialog ShowBed()
        {
            output.Refresh();
            Create(assets.bedPrefab, root => {
                Bed = root.GetComponent<FurnitureBedDialog>();
                Bed.Init(EFurniture.Bed);
            });
            return Bed;
        }

        public void ShowConfirm(HomeWindowText content, HomeWindowText? title, Action confirmed)
        {
            Create(OfficialPartnerConfirmAssetCatalog.LoadDefault().Prefab, root => {
                var dialog = root.GetComponent<WorldConfirmDialog>();
                dialog.CloseWindow = () => Close(root);
                dialog.Init(content.Key, content.Args, confirmed);
                if (title.HasValue) dialog.SetTitleText(title.Value.Key, title.Value.Args);
            });
        }

        public void ShowBlessingRule()
            => Create(assets.blessingRulePrefab, root => root.GetComponent<BlessingLevelRuleDialog>().Init());

        GameObject Clone(GameObject prefab, Transform parent, bool keepWorld = false)
        {
            var root = Object.Instantiate(prefab, staging.transform, false);
            root.SetActive(false);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font) fonts.SetupFont(text.font);
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                text.Localizer = services.Text;
            foreach (var area in root.GetComponentsInChildren<SafeArea>(true))
                area.ScreenAdaptService = screen;
            foreach (var button in root.GetComponentsInChildren<WorldButtonSound>(true)) button.Post = sound;
            foreach (var popup in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) popup.Post = sound;
            foreach (var bed in root.GetComponentsInChildren<FurnitureBedDialog>(true)) bed.Services = services;
            foreach (var quick in root.GetComponentsInChildren<FurnitureHangUpQuickReceive>(true)) quick.Services = services;
            foreach (var addition in root.GetComponentsInChildren<OutputAdditionDetailButton>(true)) addition.Services = services;
            foreach (var ruleButton in root.GetComponentsInChildren<BlessingRuleBtn>(true)) ruleButton.Services = services;
            foreach (var info in root.GetComponentsInChildren<FurnitureOutputInfo>(true)) info.OutputServices = output;
            foreach (var rule in root.GetComponentsInChildren<BlessingLevelRuleDialog>(true)) rule.Services = (IHomeBlessingRuleServices)services;
            foreach (var row in root.GetComponentsInChildren<BlessingLevelRuleItem>(true)) row.Services = (IHomeBlessingRuleServices)services;
            root.transform.SetParent(parent, keepWorld);
            return root;
        }

        void Create(GameObject prefab, Action<GameObject> initialize)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeHangUpWindows));
            var window = new Window();
            windows.Add(window);
            try
            {
                window.Root = Clone(prefab, canvas.transform);
                TMP_FontAsset originalFont = null;
                foreach (var text in window.Root.GetComponentsInChildren<TMP_Text>(true))
                    if (text.font) { originalFont = text.font; break; }
                window.Binding = OfflineUiBindingModule.Register(window.Root.transform, new OfflineUiBindings {
                    Localize = services.Text, Font = () => originalFont,
                    Images = _ => services.Resources,
                    Pool = (_, template, parent, world) => Clone(template, parent, world) });
                initialize(window.Root);
                var popup = window.Root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => Close(window.Root));
                window.Mask = Clone(blurPrefab, canvas.transform);
                var mask = window.Mask.GetComponent<WorldWindowBlurMask>();
                if (!mask || mask.MaskType != 3)
                    throw new InvalidOperationException("Selected bed popup requires its original blur mask.");
                mask.BlurManager = blur;
                // Block scene input while the original blur preparation is pending.
                ActiveWindowChanged?.Invoke(window.Root);
                window.Preparation = host.StartCoroutine(Prepare(window, popup, mask));
            }
            catch { Destroy(window); throw; }
        }

        IEnumerator Prepare(Window window, WorldPopupWindow popup, WorldWindowBlurMask mask)
        {
            yield return mask.Prepare(popup, false);
            if (!windows.Contains(window)) yield break;
            window.Root.SetActive(true);
            foreach (var close in window.Root.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(this);
            Action<WorldPopupWindow> showing = null;
            showing = shown => {
                shown.OnShowing -= showing;
                int order = canvas.sortingOrder;
                foreach (var current in windows)
                    if (current != window && current.Root && !current.Closing)
                        order = Math.Max(order, current.Root.GetComponent<WorldPopupWindow>().Canvas.sortingOrder);
                shown.SetCanvasSortingOrder(order + 10);
                mask.AttachToWindow(shown);
            };
            popup.OnShowing += showing;
            popup.Show();
            window.Preparation = null;
            WindowOpened?.Invoke(window.Root);
        }

        public void Close(GameObject root)
        {
            var window = windows.Find(value => value.Root == root);
            if (window == null || window.Closing) return;
            window.Closing = true;
            if (window.Preparation != null) { Destroy(window); return; }
            root.GetComponent<WorldPopupWindow>().Hide(() => Destroy(window));
        }

        void Destroy(Window window)
        {
            if (!windows.Remove(window)) return;
            if (window.Preparation != null) host.StopCoroutine(window.Preparation);
            window.Preparation = null;
            if (window.Mask)
            {
                window.Mask.GetComponent<WorldWindowMask>().UnattachToWindow();
                window.Mask.SetActive(false);
                Object.Destroy(window.Mask);
            }
            if (window.Root)
            {
                if (Bed && Bed.gameObject == window.Root) Bed = null;
                window.Root.SetActive(false);
                Object.Destroy(window.Root);
            }
            // OnDisable must still see the live services and binding.
            window.Binding?.Dispose();
            ActiveWindowChanged?.Invoke(windows.Count == 0 ? null : windows[windows.Count - 1].Root);
        }
        public void ObserveDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory += callback;
        public void CloseLast() { if (windows.Count > 0) Close(windows[windows.Count - 1].Root); }
        public void StopObservingDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory -= callback;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            while (windows.Count > 0) Destroy(windows[windows.Count - 1]);
            fonts.Dispose();
            if (staging) Object.Destroy(staging);
            ActiveWindowChanged = null; WindowOpened = null;
        }
    }
}
