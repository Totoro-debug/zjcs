using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeCurrencyUI
{
    public sealed class OfflineHomeCurrencyWindowBindings
    {
        public Canvas Canvas;
        public MonoBehaviour CoroutineHost;
        public ITopBarManager TopBar;
        public GameObject BlurMaskPrefab;
        public IWorldWindowBlurManager BlurManager;
        public FontAssetFontService Fonts;
        public IScreenAdaptService Screen;
        public IProfessionItemIconContext Icons;
        public Action<AudioClip, GameObject> PostSound;
    }

    // Owns the local WindowManager/resource boundary. Layouts, controls, pooling
    // callbacks, masks and animation clips remain those of the selected client.
    public sealed class OfflineHomeCurrencyWindows : IProfessionWindowService, IHomeCountColors, IDisposable
    {
        private sealed class Window
        {
            public GameObject Root, Mask;
            public IDisposable Binding;
            public Coroutine Preparation;
            public bool Closing;
        }

        private readonly OfflineHomeCurrencyWindowBindings bindings;
        private readonly OfflineHomeCurrencyAssets assets;
        private readonly IHomeCurrencyWindowServices services;
        private readonly OfflineProfessionImageResourceLoader images;
        private readonly ItemIconAttachmentManager attachments;
        private readonly GameObjectPool<ItemIcon> icons;
        private readonly GameObject poolRoot;
        private readonly IDisposable poolBinding;
        private readonly List<Window> windows = new List<Window>();
        private RectTransform defaultIconTransform;
        private bool disposed;

        public event Action<GameObject> WindowOpened;
        public event Action<GameObject> WindowClosed;
        public int WindowCount => windows.Count;

        public OfflineHomeCurrencyWindows(OfflineHomeCurrencyWindowBindings bindings,
            IHomeCurrencyWindowServices services, OfflineHomeCurrencyAssets assets = null)
        {
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.assets = assets ?? OfflineHomeCurrencyAssets.LoadDefault();
            if (!bindings.Canvas || !bindings.CoroutineHost || !bindings.BlurMaskPrefab ||
                bindings.TopBar == null || bindings.BlurManager == null || bindings.Fonts == null ||
                bindings.Screen == null || bindings.Icons == null || bindings.PostSound == null)
                throw new ArgumentException("Original currency-window services must be bound before activation.");
            images = new OfflineProfessionImageResourceLoader(this.assets.images);
            attachments = new ItemIconAttachmentManager(images, () => this.assets.attachmentConfig, new GAction());
            poolRoot = new GameObject("HomeCurrencyWindowPools", typeof(RectTransform));
            poolRoot.transform.SetParent(bindings.Canvas.transform, false);
            poolBinding = Register(poolRoot);
            icons = new GameObjectPool<ItemIcon>(poolRoot.transform, (slot, _, active) =>
            {
                var root = InstantiateBound(this.assets.itemIconPrefab, slot, false);
                if (active) root.SetActive(true);
                return root;
            }, "HomeCurrencyItemIconDefault");
        }

        public QuickBuyDialog ShowQuickBuy(int itemId, long requireCount = 0, bool isLimitByRes = true,
            Action onCancel = null, bool showMax = false)
        {
            QuickBuyDialog dialog = null;
            Create(assets.quickBuyPrefab, root =>
            {
                dialog = root.GetComponent<QuickBuyDialog>();
                dialog.Init(itemId, requireCount, isLimitByRes, onCancel, showMax);
            });
            return dialog;
        }

        public PayMoneyChangeDialog ShowExchange(long count = 0)
        {
            PayMoneyChangeDialog dialog = null;
            Create(assets.payChangePrefab, root =>
            {
                dialog = root.GetComponent<PayMoneyChangeDialog>();
                dialog.Init(count);
            });
            return dialog;
        }

        public WorldConfirmDialog ShowConfirm(string key, object[] args, Action confirm, Action cancel = null)
        {
            WorldConfirmDialog dialog = null;
            Create(OfficialPartnerConfirmAssetCatalog.LoadDefault().Prefab, root =>
            {
                dialog = root.GetComponent<WorldConfirmDialog>();
                dialog.CloseWindow = () => Close(root);
                dialog.Init(key, args, confirm, cancel);
            });
            return dialog;
        }

        public void PopupConfirm(Action<WorldConfirmDialog> initialize)
        {
            Create(OfficialPartnerConfirmAssetCatalog.LoadDefault().Prefab, root =>
            {
                var dialog = root.GetComponent<WorldConfirmDialog>();
                dialog.CloseWindow = () => Close(root);
                initialize(dialog);
            });
        }

        private IDisposable Register(GameObject root) => OfflineUiBindingModule.Register(root.transform,
            new OfflineUiBindings {
                Localize = assets.Text, Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont,
                Images = _ => images,
                Pool = (scope, prefab, parent, keepWorld) => InstantiateBound(prefab, parent, keepWorld)
            });

        private GameObject InstantiateBound(GameObject prefab, Transform parent, bool keepWorld)
        {
            var staging = new GameObject("InactiveCurrencyBindings", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(bindings.Canvas.transform, false);
            try
            {
                var root = Object.Instantiate(prefab, staging.transform, false);
                root.SetActive(false);
                Bind(root);
                root.transform.SetParent(parent, keepWorld);
                return root;
            }
            finally { Object.Destroy(staging); }
        }

        private void Bind(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font) bindings.Fonts.SetupFont(text.font);
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = assets.Text;
            foreach (var safe in root.GetComponentsInChildren<SafeArea>(true)) safe.ScreenAdaptService = bindings.Screen;
            foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = bindings.PostSound;
            foreach (var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = bindings.PostSound;
            foreach (var icon in root.GetComponentsInChildren<ItemIcon>(true)) icon.Bind(bindings.Icons);
            foreach (var cell in root.GetComponentsInChildren<ItemIconAttachment>(true)) cell.Bind(attachments);
            foreach (var progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                progress.Compact = value => CultureNumberArg.Compact(value).ToString();
            foreach (var control in root.GetComponentsInChildren<SetCountSlider>(true)) control.GlobalColors = this;
            foreach (var bar in root.GetComponentsInChildren<TopBarHandler>(true)) bar.Bind(bindings.TopBar);
            foreach (var cost in root.GetComponentsInChildren<PlayerCostMoneyContent>(true)) cost.Services = services;
            foreach (var quick in root.GetComponentsInChildren<QuickBuyDialog>(true)) quick.Services = services;
            foreach (var pay in root.GetComponentsInChildren<PayMoneyChangeDialog>(true)) pay.Services = services;
        }

        private void Create(GameObject prefab, Action<GameObject> initialize)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeCurrencyWindows));
            var window = new Window();
            windows.Add(window);
            try
            {
                window.Root = InstantiateBound(prefab, bindings.Canvas.transform, false);
                window.Binding = Register(window.Root);
                initialize(window.Root);
                var popup = window.Root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => Close(window.Root));
                window.Mask = InstantiateBound(bindings.BlurMaskPrefab, bindings.Canvas.transform, false);
                var mask = window.Mask.GetComponent<WorldWindowBlurMask>();
                if (!mask || mask.MaskType != 3)
                    throw new InvalidOperationException("Original currency popup requires its selected blur mask.");
                mask.BlurManager = bindings.BlurManager;
                window.Preparation = bindings.CoroutineHost.StartCoroutine(Prepare(window, popup, mask));
            }
            catch { Destroy(window); throw; }
        }

        private IEnumerator Prepare(Window window, WorldPopupWindow popup, WorldWindowBlurMask mask)
        {
            yield return mask.Prepare(popup, false);
            if (!windows.Contains(window)) yield break;
            window.Root.SetActive(true);
            foreach (var button in window.Root.GetComponentsInChildren<CloseWindowBtn>(true)) button.Init(this);
            // Selected WindowContainer.OnWindowShowing assigns sorting only after
            // activation. First activation otherwise restores the prefab Canvas values.
            Action<WorldPopupWindow> showing = null;
            showing = shown =>
            {
                shown.OnShowing -= showing;
                int order = bindings.Canvas.sortingOrder;
                foreach (var current in bindings.Canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(window.Root.transform))
                        order = Math.Max(order, current.sortingOrder);
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

        public void CloseExchange()
        {
            for (int index = windows.Count - 1; index >= 0; index--)
                if (windows[index].Root && windows[index].Root.GetComponent<PayMoneyChangeDialog>())
                    Close(windows[index].Root);
        }

        private void Destroy(Window window)
        {
            if (!windows.Remove(window)) return;
            if (window.Preparation != null) bindings.CoroutineHost.StopCoroutine(window.Preparation);
            window.Preparation = null;
            if (window.Mask)
            {
                window.Mask.GetComponent<WorldWindowMask>().UnattachToWindow();
                Object.Destroy(window.Mask);
            }
            var root = window.Root;
            if (root) { root.SetActive(false); Object.Destroy(root); }
            window.Binding?.Dispose();
            WindowClosed?.Invoke(root);
        }

        public ItemIcon GetIcon(HomeCurrencyWindowItemInfo item, Transform slot)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeCurrencyWindows));
            if (item.Id != assets.MoneyId && item.Id != assets.DiamonId && item.Id != assets.FlareStarId)
                throw new InvalidOperationException("The selected default icon binding is limited to the three currency items.");
            var icon = icons.Borrow(slot);
            // Selected ItemIconManager.ResetIconGo, with one verified default pool.
            icon.SetUIGray(false);
            var rect = icon.GetComponent<RectTransform>();
            if (defaultIconTransform) CopyRectTransform(rect, defaultIconTransform);
            else
            {
                defaultIconTransform = new GameObject("CurrencyIconDefaultTransform", typeof(RectTransform)).GetComponent<RectTransform>();
                defaultIconTransform.SetParent(poolRoot.transform, false);
                CopyRectTransform(defaultIconTransform, rect);
            }
            icon.Init(item.Id);
            return icon;
        }

        // Selected UIUtils.CopyRectTransform, preserving assignment order.
        private static void CopyRectTransform(RectTransform target, RectTransform source)
        {
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchorMax = source.anchorMax;
            target.anchorMin = source.anchorMin;
            target.anchoredPosition = source.anchoredPosition;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
            target.localPosition = source.localPosition;
        }

        public void RecycleIcon(ItemIcon icon) => icons.Return(icon);
        public IProfessionAssetOperation<Sprite> LoadSprite(string path) => images.LoadAsset<Sprite>("Assets/Image/" + path);
        public void CloseLast() { if (windows.Count > 0) Close(windows[windows.Count - 1].Root); }
        public void ObserveDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory += callback;
        public void StopObservingDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory -= callback;
        public Color LightRed => assets.LightRed;
        public string WrapColor(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            while (windows.Count > 0) Destroy(windows[windows.Count - 1]);
            icons.Destory();
            attachments.Dispose();
            poolBinding.Dispose();
            Object.Destroy(poolRoot);
        }
    }
}
