using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static class OfflineUiBindingAcceptance
    {
        private sealed class Loader : IProfessionImageResourceLoader
        {
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object => throw new NotSupportedException();
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object => throw new NotSupportedException();
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object => throw new NotSupportedException();
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => throw new NotSupportedException();
        }

        public static void Run()
        {
            try { Validate(); ProjectTools.Validate(); Debug.Log("UI binding lifetime acceptance passed."); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Validate()
        {
            var beforeImages = ProfessionImageResourceBridge.Provider;
            var beforePool = ProfessionGameObjectPoolBridge.Provider;
            var beforeText = WorldMultiLanguageText.Localize;
            var beforeFont = WorldMultiLanguageText.LanguageFont;
            foreach (bool closeParentFirst in new[] { true, false })
            {
                var a = new GameObject("Binding A");
                var b = new GameObject("Binding B"); b.transform.SetParent(a.transform);
                var sibling = new GameObject("Binding other window");
                var imageA = a.AddComponent<Image>(); var imageB = b.AddComponent<Image>();
                var loaderA = new Loader(); var loaderB = new Loader();
                var fontA = ScriptableObject.CreateInstance<TMP_FontAsset>();
                var fontB = ScriptableObject.CreateInstance<TMP_FontAsset>();
                IDisposable tokenA = null, tokenB = null, tokenSibling = null;
                try
                {
                    tokenA = OfflineUiBindingModule.Register(a.transform, new OfflineUiBindings {
                        Images = _ => loaderA, Font = () => fontA, Localize = key => key == "A" ? "text A" : key,
                        Pool = (scope, prefab, parent, keepWorld) => a });
                    tokenB = OfflineUiBindingModule.Register(b.transform, new OfflineUiBindings {
                        Images = _ => loaderB, Font = () => fontB, Localize = key => key == "B" ? "text B" : key,
                        Pool = (scope, prefab, parent, keepWorld) => b });
                    var dispatcher = ProfessionImageResourceBridge.Provider;
                    tokenSibling = OfflineUiBindingModule.Register(sibling.transform, new OfflineUiBindings {
                        Images = _ => new Loader(), Localize = key => WorldMultiLanguageText.Localize(key) });
                    Require(ProfessionImageResourceBridge.Resolve(imageA) == loaderA
                        && ProfessionImageResourceBridge.Resolve(imageB) == loaderB
                        && ProfessionImageResourceBridge.Provider == dispatcher
                        && ProfessionGameObjectPoolBridge.Provider(b.transform, null, null, false) == b
                        && WorldMultiLanguageText.Localize("A") == "text A"
                        && WorldMultiLanguageText.Localize("B") == "text B"
                        && WorldMultiLanguageText.LanguageFont() == fontB, "ownership, priority or recursive localization fallback");
                    if (closeParentFirst) tokenA.Dispose(); else tokenB.Dispose();
                    Require(WorldMultiLanguageText.LanguageFont() == (closeParentFirst ? fontB : fontA), "out of order close");
                    tokenA.Dispose(); tokenB.Dispose(); tokenSibling.Dispose();
                    Require(ProfessionImageResourceBridge.Provider == beforeImages
                        && ProfessionGameObjectPoolBridge.Provider == beforePool
                        && WorldMultiLanguageText.Localize == beforeText && WorldMultiLanguageText.LanguageFont == beforeFont,
                        "released registrations retained a closed window");
                    // Scene destruction and a subsequent profile's root cannot retain previous loaders.
                    tokenA = OfflineUiBindingModule.Register(a.transform, new OfflineUiBindings { Images = _ => loaderA });
                    UnityEngine.Object.DestroyImmediate(a);
                    Require(ProfessionImageResourceBridge.Provider == beforeImages, "owner destruction");
                }
                finally
                {
                    tokenA?.Dispose(); tokenB?.Dispose(); tokenSibling?.Dispose();
                    if (a) UnityEngine.Object.DestroyImmediate(a);
                    if (b) UnityEngine.Object.DestroyImmediate(b);
                    UnityEngine.Object.DestroyImmediate(sibling);
                    UnityEngine.Object.DestroyImmediate(fontA); UnityEngine.Object.DestroyImmediate(fontB);
                }
            }
        }
        private static void Require(bool condition, string behavior)
        { if (!condition) throw new InvalidOperationException("UI binding lifetime failed: " + behavior); }
    }
}
