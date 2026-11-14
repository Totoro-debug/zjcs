using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeLevelUpUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeLevelUpUI
{
    public sealed class OfflineHomeLevelUpWindowBindings
    {
        public Canvas Canvas;
        public IProfessionPropertyBindings Properties;
        public IProfessionSoundHost SoundHost;
        public Action<AudioClip, GameObject> PostSound;
        // Shared containers may supply their original sorting owner here.
        public Action<WorldPopupWindow> Showing, Closed;
    }

    // Platform ownership only. LevelUpNotifyTip retains initialization, property
    // comparison and OnShow close; the original duplicate tip is never pooled.
    public sealed class OfflineHomeLevelUpWindows : IHomeLevelUpServices, IDisposable
    {
        private sealed class Window
        {
            public GameObject Root;
            public WorldPopupWindow Popup;
            public bool Closing;
        }
        private readonly OfflineHomeLevelUpWindowBindings bindings;
        private readonly OfflineHomeLevelUpAssets assets;
        private readonly OfflineHomeLevelUpProperties calculator;
        private readonly Func<int> level;
        private readonly FontAssetFontService fonts;
        private readonly List<Window> windows = new List<Window>();
        private readonly int initialSortingOrder;
        private int sortingOrder;
        private bool disposed;
        public int WindowCount => windows.Count;
        public int CurrentLevel => level();
        public IProfessionImageResourceLoader Resources { get; }

        // Calculator, player, shared property/sound services and canvas are borrowed.
        public OfflineHomeLevelUpWindows(OfflineHomeLevelUpWindowBindings bindings,
            OfflineHomeLevelUpProperties calculator, Func<int> level, OfflineHomeLevelUpAssets assets = null)
        {
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            if (!bindings.Canvas || bindings.Properties == null || bindings.SoundHost == null || bindings.PostSound == null
                || !(bindings.SoundHost is IProfessionSoundObjectHost soundObjects) || !soundObjects.IsInitialized)
                throw new InvalidOperationException("The original notification needs its canvas, shared property and initialized sound owners.");
            if ((bindings.Showing == null) != (bindings.Closed == null))
                throw new ArgumentException("Supply both shared container callbacks.");
            this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            this.level = level ?? throw new ArgumentNullException(nameof(level));
            this.assets = assets ? assets : OfflineHomeLevelUpAssets.LoadDefault();
            Resources = new OfflineProfessionImageResourceLoader(this.assets.images);
            fonts = this.assets.CreateFonts(Resources);
            try { fonts.Initialize(); }
            catch { fonts.Dispose(); throw; }
            initialSortingOrder = sortingOrder = bindings.Canvas.sortingOrder;
        }

        public Dictionary<ProfessionPropertyType, long> CalcCharacterBattleProps(int value) => calculator.Evaluate(value);
        public long GetRawProp(Dictionary<ProfessionPropertyType, long> values, ProfessionPropertyType type)
            => values != null && values.TryGetValue(type, out long value) ? value : 0;
        public ProfessionPropertyDefinition GetProperty(ProfessionPropertyType type) => bindings.Properties.GetProperty(type);

        public void Popup(Action<LevelUpNotifyTip> initialized)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeLevelUpWindows));
            // Original PropItemValue format objects are refreshed again by Start.
            // The shared property service must outlive every page using them.
            if (!ReferenceEquals(ProfessionPropertyBindings.Current, bindings.Properties))
                throw new InvalidOperationException("Bind the shared property language service before opening the original notification.");
            var staging = new GameObject("LevelUpInactiveBinding"); staging.SetActive(false);
            var window = new Window();
            try
            {
                window.Root = Object.Instantiate(assets.prefab, staging.transform, false);
                window.Root.SetActive(false);
                window.Popup = window.Root.GetComponent<WorldPopupWindow>();
                var language = assets.CreateLanguage();
                foreach (var text in window.Root.GetComponentsInChildren<TMP_Text>(true)) fonts.SetupFont(text.font);
                foreach (var text in window.Root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = key => language.GetText(key, language.CurrentLanguage);
                foreach (var row in window.Root.GetComponentsInChildren<ProfessionPlayerPropItem>(true)) row.Configure(bindings.Properties);
                foreach (var sound in window.Root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = bindings.PostSound;
                foreach (var sound in window.Root.GetComponentsInChildren<AkEvent>(true)) sound.data.InstanceHost = bindings.SoundHost;
                var tip = window.Root.GetComponent<LevelUpNotifyTip>(); tip.Services = this;
                window.Root.transform.SetParent(bindings.Canvas.transform, false);
                // AkGameObj caches this host at Awake. Restore the previous host
                // immediately; AkEvent uses its explicit instance host thereafter.
                var previous = OfficialWwiseEvent.Host;
                try { OfficialWwiseEvent.Host = bindings.SoundHost; window.Root.SetActive(true); }
                finally { OfficialWwiseEvent.Host = previous; }
                window.Popup.Canvas.renderMode = bindings.Canvas.renderMode;
                window.Popup.Canvas.worldCamera = bindings.Canvas.worldCamera;
                window.Popup.Canvas.planeDistance = bindings.Canvas.planeDistance;
                initialized?.Invoke(tip);
                windows.Add(window);
                window.Root.transform.SetAsLastSibling();
                window.Popup.OnShowing += OnShowing;
                window.Popup.Resume(); window.Popup.Show();
            }
            catch { Destroy(window); throw; }
            finally { Object.Destroy(staging); }
        }

        private void OnShowing(WorldPopupWindow popup)
        {
            popup.OnShowing -= OnShowing;
            if (bindings.Showing != null) bindings.Showing(popup);
            else { sortingOrder += 10; popup.SetCanvasSortingOrder(sortingOrder); }
        }

        public void Close(GameObject root)
        {
            var window = windows.FirstOrDefault(value => value.Root == root);
            if (window == null || window.Closing) return;
            window.Closing = true;
            window.Popup.Hide(() => Destroy(window));
        }

        private void Destroy(Window window)
        {
            bool owned = windows.Remove(window);
            if (window.Root)
            {
                if (window.Popup) window.Popup.OnShowing -= OnShowing;
                window.Root.SetActive(false);
                Object.Destroy(window.Root);
            }
            if (owned && bindings.Closed != null) bindings.Closed(window.Popup);
            else if (owned) sortingOrder = windows.LastOrDefault(value => !value.Closing)?.Popup.Canvas.sortingOrder ?? initialSortingOrder;
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var window in windows.ToArray()) Destroy(window);
            fonts.Dispose(); disposed = true;
        }
    }
}
