using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Bind the exact page/template before activation. The surrounding original
    // NpcPanel owns navigation; this component creates no controls or layout.
    public sealed class PartnerStarGodPanelView : MonoBehaviour
    {
        private sealed class GroupPool : LoopScrollPrefabSource
        {
            private readonly LoopScrollPrefabSource source;
            private readonly IPartnerStarGodUiServices services;
            private readonly Action<GameObject> bindNativeTree;
            internal GroupPool(LoopScrollPrefabSource source, IPartnerStarGodUiServices services, Action<GameObject> bindNativeTree)
            { this.source = source; this.services = services; this.bindNativeTree = bindNativeTree; }
            public Transform GetPoolRoot() => source.GetPoolRoot();
            public GameObject GetObject(int index)
            {
                var value = source.GetObject(index);
                bindNativeTree(value);
                foreach (var item in value.GetComponentsInChildren<StarGodNpcItem>(true)) item.Configure(services);
                BindSounds(value, services);
                foreach (var text in value.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
                return value;
            }
            public void ReturnObject(Transform value, int index) => source.ReturnObject(value, index);
        }
        private static readonly FieldInfo PrefabSource = typeof(LoopScrollRectBase)
            .GetField("mPrefabSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject content, bindingRoot;
        private IDisposable uiBinding;
        private OfflineProfessionImageCatalog imageCatalog;
        private bool hasScope;

        public void Open(OfficialPartnerStarGodAssetCatalog assets, IPartnerStarGodUiServices services,
            TMPro.TMP_FontAsset font, Action<GameObject> bindNativeTree)
        {
            if (!assets || services == null || !font || bindNativeTree == null) throw new ArgumentNullException();
            if (hasScope) throw new InvalidOperationException("Star-god page is already open.");
            hasScope = true;
            try
            {
                imageCatalog = assets.CreateImageCatalog();
                var images = new OfflineProfessionImageResourceLoader(imageCatalog);
                // An inactive parent also prevents OnEnable when the source prefab
                // is active. Preserve the source page's own RectTransform values.
                bindingRoot = new GameObject("OfficialPartnerStarGodBinding", typeof(RectTransform));
                var rect = (RectTransform)bindingRoot.transform;
                rect.SetParent(transform, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero; bindingRoot.SetActive(false);
                uiBinding = OfflineUiBindingModule.Register(rect, new OfflineUiBindings {
                    Font = () => font, Images = image => images,
                    Localize = key => { foreach (var row in assets.Texts) if (row.key == key) return row.text; return key; },
                    Pool = (scope, prefab, parent, keepWorld) => {
                        var value = Instantiate(prefab, parent, keepWorld); bindNativeTree(value);
                        foreach (var text in value.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
                        return value;
                    }
                });
                content = Instantiate(assets.PagePrefab, rect, false);
                var list = content.GetComponent<NpcPanelStarGodContent>();
                var loop = content.GetComponentInChildren<LoopVerticalScrollRect>(true);
                if (!list || !loop || loop.DefaultPrefabSource?.Prefab == null)
                    throw new InvalidOperationException("Original star-god list/template is unbound.");
                bindNativeTree(content);
                PrefabSource.SetValue(loop, new GroupPool(loop.prefabSource, services, bindNativeTree));
                BindSounds(content, services);
                foreach (var text in content.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
                list.Configure(services);
                content.SetActive(true); bindingRoot.SetActive(true);
            }
            catch { Close(); throw; }
        }
        private static void BindSounds(GameObject value, IPartnerStarGodUiServices services)
        {
            foreach (var sound in value.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = services.PostSound;
            foreach (var sound in value.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = services.PostSound;
        }
        public void Close()
        {
            if (!hasScope) return;
            hasScope = false;
            uiBinding?.Dispose(); uiBinding = null;
            if (bindingRoot)
            {
                bindingRoot.SetActive(false); // Unsubscribe before the deferred destroy.
                Destroy(bindingRoot);
            }
            bindingRoot = null; content = null;
            if (imageCatalog) Destroy(imageCatalog);
            imageCatalog = null;
        }
        private void OnDestroy() => Close();
    }
}
