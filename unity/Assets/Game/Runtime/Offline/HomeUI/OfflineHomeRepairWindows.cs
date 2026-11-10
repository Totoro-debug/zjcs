using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;

namespace Zjcs.Offline.HomeUI
{
    public interface IOfflineHomeRepairState
    {
        bool HasRepairCost(EFurniture furniture);
        void Repair(EFurniture furniture, Action onSuccess);
        void SystemMessage(string key);
        void GetValidHangUpIdList(EFurniture furniture, List<int> result);
        ProfessionPropertyType GetEffectFixedPropType(int hangUpId);
        double GetHangUpReward(int hangUpId, double hours);
    }

    // Local WindowManager boundary. Controls, sorting, mask behavior and clips
    // come from the original prefab and existing migrated window components.
    public sealed class OfflineHomeRepairWindows : IFurnitureRepairDialogServices,
        IFurnitureRepairedNotifyServices, IProfessionWindowService, IDisposable
    {
        private sealed class Window
        {
            public GameObject Root, Mask;
            public IDisposable Binding;
            public EFurniture Furniture;
            public bool Notify, Closing;
            public Coroutine Preparation;
        }
        private readonly Canvas canvas;
        private readonly TMP_FontAsset font;
        private readonly GameObject maskPrefab;
        private readonly MonoBehaviour coroutineHost;
        private readonly IWorldWindowBlurManager blurManager;
        private readonly Action<AudioClip, GameObject> postSound;
        private readonly IOfflineHomeRepairState state;
        private readonly OfficialHomeRepairAssetCatalog assets;
        private readonly List<Window> windows = new List<Window>();
        public event Action<GameObject, string> ActiveWindowChanged;
        public event Action<EFurniture, bool> WindowClosed;
        public FurnitureRepairDialog RepairDialog { get; private set; }

        public OfflineHomeRepairWindows(Canvas canvas, TMP_FontAsset font, GameObject maskPrefab,
            MonoBehaviour coroutineHost, IWorldWindowBlurManager blurManager,
            Action<AudioClip, GameObject> postSound, IOfflineHomeRepairState state,
            OfficialHomeRepairAssetCatalog assets = null)
        {
            this.canvas = canvas;
            this.font = font;
            this.maskPrefab = maskPrefab;
            this.coroutineHost = coroutineHost;
            this.blurManager = blurManager;
            this.postSound = postSound;
            this.state = state;
            this.assets = assets ?? OfficialHomeRepairAssetCatalog.LoadDefault();
        }

        public void ShowRepair(EFurniture furniture, bool isBuild)
        {
            var window = Create(assets.repairPrefab, furniture, false, root =>
            {
                RepairDialog = root.GetComponent<FurnitureRepairDialog>();
                RepairDialog.Services = this;
                RepairDialog.Init(furniture, isBuild);
            });
            ActiveWindowChanged?.Invoke(window.Root, "repair");
        }

        public void ShowRepairedNotify(EFurniture furniture)
        {
            var window = Create(assets.repairedNotifyPrefab, furniture, true, root =>
            {
                var dialog = root.GetComponent<FurnitureRepairedNotifyDialog>();
                dialog.Services = this;
                dialog.Init(furniture);
            });
            ActiveWindowChanged?.Invoke(window.Root, "repair_notify");
        }

        private Window Create(GameObject prefab, EFurniture furniture, bool notify, Action<GameObject> initialize)
        {
            var staging = new GameObject("HomeRepairStaging", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
            var window = new Window { Furniture = furniture, Notify = notify };
            windows.Add(window);
            try
            {
                window.Root = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                window.Binding = OfflineUiBindingModule.Register(window.Root.transform, new OfflineUiBindings
                {
                    Localize = assets.Text,
                    Font = () => font,
                    Pool = (scope, template, parent, keepWorld) => UnityEngine.Object.Instantiate(template, parent, keepWorld)
                });
                foreach (var text in window.Root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                foreach (var text in window.Root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = assets.Text;
                foreach (var sound in window.Root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = postSound;
                foreach (var sound in window.Root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = postSound;
                initialize(window.Root);
                // Awake must run before CloseWindowBtn.Init observes its popup.
                window.Root.SetActive(false);
                window.Root.transform.SetParent(canvas.transform, false);
                var popup = window.Root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => Close(window.Root));
                int order = canvas.sortingOrder;
                foreach (var current in canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(window.Root.transform))
                        order = Math.Max(order, current.sortingOrder);
                popup.SetCanvasSortingOrder(order + 10);
                window.Mask = UnityEngine.Object.Instantiate(maskPrefab, staging.transform, false);
                window.Mask.SetActive(false);
                window.Mask.transform.SetParent(canvas.transform, false);
                var mask = window.Mask.GetComponent<WorldWindowBlurMask>();
                if (mask == null || mask.MaskType != 3)
                    throw new InvalidOperationException("Original furniture popup requires its blur mask.");
                mask.BlurManager = blurManager;
                window.Preparation = coroutineHost.StartCoroutine(Prepare(window, popup, mask));
                return window;
            }
            catch { Destroy(window); throw; }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private IEnumerator Prepare(Window window, WorldPopupWindow popup, WorldWindowBlurMask mask)
        {
            yield return mask.Prepare(popup, false);
            if (!windows.Contains(window)) yield break;
            window.Root.SetActive(true);
            foreach (var button in window.Root.GetComponentsInChildren<CloseWindowBtn>(true)) button.Init(this);
            popup.Show();
            mask.AttachToWindow(popup);
            window.Preparation = null;
        }

        public void Close(GameObject root)
        {
            var window = windows.Find(item => item.Root == root);
            if (window == null || window.Closing) return;
            window.Closing = true;
            root.GetComponent<WorldPopupWindow>().Hide(() =>
            {
                Destroy(window);
                if (windows.Count == 0) ActiveWindowChanged?.Invoke(null, null);
                WindowClosed?.Invoke(window.Furniture, window.Notify);
            });
        }

        private void Destroy(Window window)
        {
            if (window.Preparation != null) coroutineHost.StopCoroutine(window.Preparation);
            window.Preparation = null;
            if (window.Mask != null)
            {
                window.Mask.GetComponent<WorldWindowMask>().UnattachToWindow();
                UnityEngine.Object.Destroy(window.Mask);
            }
            if (window.Root != null)
            {
                window.Root.SetActive(false);
                if (RepairDialog != null && RepairDialog.gameObject == window.Root) RepairDialog = null;
                UnityEngine.Object.Destroy(window.Root);
            }
            window.Binding?.Dispose();
            windows.Remove(window);
        }

        public void Dispose()
        {
            while (windows.Count > 0) Destroy(windows[windows.Count - 1]);
        }
        public void CloseLast() { if (windows.Count > 0) Close(windows[windows.Count - 1].Root); }
        public void ObserveDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory += callback;
        public void StopObservingDestroyed(WorldPopupWindow window, Action callback) => window.OnWindowDestory -= callback;
        public string GetFurnitureTypeNameKey(EFurniture furniture) => assets.FurnitureTypeNameKey(furniture);
        public int GetFurnitureItemId(EFurniture furniture) => assets.FurnitureItemId(furniture);
        public FurnitureIconWidget GetFurnitureIconPrefab(EFurniture furniture) => assets.IconPrefab(furniture);
        public bool HasRepairCost(EFurniture furniture) => state.HasRepairCost(furniture);
        public void SystemMessage(string key) => state.SystemMessage(assets.Text(key));
        public void Repair(EFurniture furniture, Action onSuccess) => state.Repair(furniture, onSuccess);
        public void GetValidHangUpIdList(EFurniture furniture, List<int> result) => state.GetValidHangUpIdList(furniture, result);
        public ProfessionPropertyType GetEffectFixedPropType(int hangUpId) => state.GetEffectFixedPropType(hangUpId);
        public double GetHangUpReward(int hangUpId, double hours) => state.GetHangUpReward(hangUpId, hours);
    }
}
