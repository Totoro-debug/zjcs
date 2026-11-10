using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    public sealed class HomeBottomBarBindings
    {
        public IHomeBottomBarServices Bar;
        public IHomeBottomBarEntryServices Entry;
        public IHomePlayerUiServices PlayerUi;
        public IHomePetEvolutionServices Pets;
        public IProfessionTipDotSystem TipDots;
        public IProfessionTipDotSystemController TipController;
        public TMP_FontAsset Font;
        public Action<AudioClip, GameObject> PostSound;
    }

    // Instantiates the complete original hierarchy inactive, then supplies local
    // services before Unity runs its original Awake/OnEnable/Start methods.
    public sealed class OfflineHomeBottomBar : IHomeHudResourceLoader, IDisposable
    {
        private readonly OfficialHomeBottomBarAssetCatalog assets;
        private readonly IHomeHudResourceLoader previousSceneLoader;
        private IDisposable binding;
        private OfflinePartnerUiSoundScope sound;
        public GameObject Root { get; private set; }
        public PublicBottomBar Bar { get; private set; }
        public OfflineHomeBottomBar(Transform parent, HomeBottomBarBindings services,
            OfficialHomeBottomBarAssetCatalog assets = null)
        {
            if (!parent || services == null || services.Bar == null || services.Entry == null
                || services.PlayerUi == null || services.Pets == null || !services.Font)
                throw new ArgumentException("Complete local bottom bar bindings are required.");
            this.assets = assets ?? OfficialHomeBottomBarAssetCatalog.LoadDefault();
            previousSceneLoader = HomeHudResourceScope.ActiveSceneLoader;
            var staging = new GameObject("HomeBottomBarStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                Root = Object.Instantiate(this.assets.prefab, staging.transform, false);
                Root.SetActive(false);
                sound = new OfflinePartnerUiSoundScope(this.assets.Sound);
                var scope = Root.AddComponent<HomeHudResourceScope>(); scope.Loader = this;
                HomeHudResourceScope.ActiveSceneLoader = this;
                binding = OfflineUiBindingModule.Register(Root.transform, new OfflineUiBindings {
                    Localize = this.assets.Text, Font = () => services.Font });
                foreach (var text in Root.GetComponentsInChildren<TMP_Text>(true)) text.font = services.Font;
                foreach (var text in Root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = this.assets.Text;
                foreach (var sound in Root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = services.PostSound;
                foreach (var node in Root.GetComponentsInChildren<PublicBottomBarCharacterBtn>(true)) node.Services = services.Bar;
                foreach (var node in Root.GetComponentsInChildren<TravelNoteBtn>(true)) node.Services = services.Entry;
                foreach (var node in Root.GetComponentsInChildren<LegionBtn>(true)) node.Services = services.Entry;
                foreach (var node in Root.GetComponentsInChildren<SwitchInGameBtnBase>(true)) node.Services = services.Entry;
                foreach (var node in Root.GetComponentsInChildren<LegionMapDataListener>(true)) node.Services = services.Entry;
                foreach (var node in Root.GetComponentsInChildren<LegionEntryControllerWithBossGameEnd>(true)) node.Services = services.Entry;
                foreach (var node in Root.GetComponentsInChildren<HomePlayerUIBase>(true)) node.PlayerServices = services.PlayerUi;
                foreach (var node in Root.GetComponentsInChildren<PetEvolutionRedDotItem>(true)) node.Services = services.Pets;
                foreach (var node in Root.GetComponentsInChildren<TipDotNodeLinker>(true)) {
                    node.TipDotSystem = services.TipDots; node.Controller = services.TipController;
                }
                Bar = Root.GetComponent<PublicBottomBar>(); Bar.Services = services.Bar;
                Root.transform.SetParent(parent, false);
                Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }
        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; Bar = null; }
            binding?.Dispose(); binding = null;
            sound?.Dispose(); sound = null;
            if (ReferenceEquals(HomeHudResourceScope.ActiveSceneLoader, this))
                HomeHudResourceScope.ActiveSceneLoader = previousSceneLoader;
        }
        public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T : Object
            => new Handle<T>(assets.Sprite(guid) as T ?? throw new InvalidOperationException("Unexpected bottom bar resource type."));
        public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => LoadAssetByGuid<T>(guid);
        private sealed class Handle<T> : IHomeHudAssetHandle<T> where T : Object
        {
            private T value;
            public Handle(T value) { this.value = value; }
            public bool IsValid => value;
            public int Status => IsValid ? 2 : 0;
            public T Result => value;
            public T WaitForAsyncComplete() => value;
            public event Action<IHomeHudAssetHandle<T>> Completed {
                add { if (IsValid) value?.Invoke(this); }
                remove { }
            }
            public void Dispose() { value = null; }
        }
    }
}
