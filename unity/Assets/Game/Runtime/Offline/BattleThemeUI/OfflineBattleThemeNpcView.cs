using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.BattleThemeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.BattleThemeUI
{
    /// <summary>Local resource and lifetime bindings for the original theme NPC module.</summary>
    public sealed class OfflineBattleThemeNpcView : MonoBehaviour, IDisposable
    {
        private const string OriginalPrefabPath =
            "UI/Fight/FightMainPanel/ThemeNpc/FightThemeNpcLuoTianYi";
        private const string ResourcePath = "Combat/Theme/LuoTianYi/FightThemeNpcLuoTianYi";
        private FightMainPanelThemeNpcModule module;
        private GameObject instance;
        private AppearanceAssetManager appearanceManager;
        private OfflinePartnerUiTextureResources textures;
        private IProfessionAppearanceTextureBindings previousTextures;
        private bool disposed;
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class TextData { public string manifest; public TextRow[] texts; }

        public static OfflineBattleThemeNpcView Mount(Transform slot, string prefabPath)
        {
            if (!slot) throw new ArgumentNullException(nameof(slot));
            var view = slot.gameObject.AddComponent<OfflineBattleThemeNpcView>();
            try
            {
                view.module = slot.gameObject.AddComponent<FightMainPanelThemeNpcModule>();
                view.module.Configure(slot, prefabPath, ClearChildren, view.Instantiate);
                view.module.OnActivate();
                return view;
            }
            catch { view.Dispose(); Object.Destroy(view); throw; }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                Object.Destroy(parent.GetChild(index).gameObject);
        }

        private void Instantiate(string path, Transform parent)
        {
            if (path != OriginalPrefabPath)
                throw new InvalidOperationException("The original battle theme NPC prefab is not bound: " + path);
            var prefab = Resources.Load<GameObject>(ResourcePath);
            var actors = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
            var config = Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config");
            if (!prefab || !actors || !config)
                throw new InvalidOperationException("Packaged original battle theme NPC resources are missing.");

            appearanceManager = new AppearanceAssetManager(config, action => new LowMemorySubscription(action));
            previousTextures = ProfessionAppearanceTextureBindings.Current;
            textures = new OfflinePartnerUiTextureResources(actors, appearanceManager, previousTextures);
            ProfessionAppearanceTextureBindings.Current = textures;
            // Original parent OnEnable starts both animation sequences. Inject
            // the local appearance and graphic operations before activation.
            var staging = new GameObject("Original battle theme resource binding", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
            try
            {
                instance = Object.Instantiate(prefab, staging.transform, false);
                instance.SetActive(false);
                var languageAsset = Resources.Load<TextAsset>("Combat/Theme/LuoTianYi/theme-npc-texts");
                var language = languageAsset ? JsonUtility.FromJson<TextData>(languageAsset.text) : null;
                if (language?.manifest != "884_156692" || language.texts == null)
                    throw new InvalidOperationException("Packaged original theme NPC localization is missing.");
                Dictionary<string, string> texts = language.texts.ToDictionary(row => row.key, row => row.text);
                foreach (var label in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                {
                    label.Localizer = key => texts.TryGetValue(key, out var text) ? text :
                        throw new InvalidOperationException("The original theme NPC text is not bound: " + key);
                    label.Refresh();
                }
                textures.BindTree(instance);
                new OfflinePartnerDetailLiveServices(actors,
                    _ => throw new InvalidOperationException("The theme sequence does not request NPC information."),
                    _ => null).BindTree(instance);
                foreach (var actor in instance.GetComponentsInChildren<PartnerNativeUiActor>(true))
                    if (actor.Pose == null && actor.SkeletonDataAsset)
                        actor.SelectAsset((PartnerNativeUiSkeletonAsset)actor.SkeletonDataAsset);
                instance.transform.SetParent(parent, false);
                // UIUtils.InstantiateAsync calls the GameObject Instantiate
                // overload (All), then ResetUI(AutoActive), in that order.
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                instance.SetActive(true);
            }
            finally { Object.Destroy(staging); }
        }

        private void Update() => appearanceManager?.Update();
        private void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Let the original AppearanceUILite OnDisable return its material
            // and texture references while its manager remains valid.
            if (instance) { instance.SetActive(false); Object.Destroy(instance); }
            instance = null;
            appearanceManager?.Dispose();
            appearanceManager = null;
            if (ReferenceEquals(ProfessionAppearanceTextureBindings.Current, textures))
                ProfessionAppearanceTextureBindings.Current = previousTextures;
            textures = null;
            previousTextures = null;
            if (module) Object.Destroy(module);
            module = null;
        }

        private sealed class LowMemorySubscription : IDisposable
        {
            private Action callback;
            internal LowMemorySubscription(Action callback)
            {
                this.callback = callback;
                Application.lowMemory += callback.Invoke;
            }
            public void Dispose()
            {
                if (callback == null) return;
                Application.lowMemory -= callback.Invoke;
                callback = null;
            }
        }
    }
}
