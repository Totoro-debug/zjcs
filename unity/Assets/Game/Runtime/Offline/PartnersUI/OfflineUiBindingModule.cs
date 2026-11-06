using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflineUiBindings
    {
        public Func<string, string> Localize;
        public Func<TMP_FontAsset> Font;
        public Func<Image, IProfessionImageResourceLoader> Images;
        public Func<Transform, GameObject, Transform, bool, GameObject> Pool;
        public IProfessionAppearanceSkinClipBindings SkinClip;
    }

    // Global official bridges remain stable while registrations exist. Requests
    // with a node use the closest owner; localization/font use newest live scope.
    public static class OfflineUiBindingModule
    {
        private sealed class Registration : IDisposable
        {
            internal Transform Root;
            internal OfflineUiBindings Bindings;
            internal Registration(Transform root, OfflineUiBindings bindings) { Root = root; Bindings = bindings; }
            public void Dispose()
            {
                if (Bindings == null) return;
                registrations.Remove(this);
                Bindings = null; Root = null;
                Rebind();
            }
        }

        private static readonly List<Registration> registrations = new List<Registration>();
        private static OfflineUiBindings original;
        private static int localizeCursor = int.MaxValue;

        public static IDisposable Register(Transform ownerRoot, OfflineUiBindings bindings)
        {
            if (!ownerRoot) throw new ArgumentNullException(nameof(ownerRoot));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (registrations.Count == 0)
                original = new OfflineUiBindings {
                    Localize = WorldMultiLanguageText.Localize, Font = WorldMultiLanguageText.LanguageFont,
                    Images = ProfessionImageResourceBridge.Provider, Pool = ProfessionGameObjectPoolBridge.Provider,
                    SkinClip = ProfessionAppearanceSkinClipBindings.Current
                };
            var registration = new Registration(ownerRoot, bindings);
            registrations.Add(registration);
            var lifetime = ownerRoot.gameObject.AddComponent<OfflineUiBindingLifetime>();
            lifetime.Token = registration;
            Rebind();
            return registration;
        }

        private static void Rebind()
        {
            if (original == null) return;
            WorldMultiLanguageText.Localize = registrations.Count == 0 ? original.Localize : Localize;
            WorldMultiLanguageText.LanguageFont = registrations.Count == 0 ? original.Font : Font;
            ProfessionImageResourceBridge.Provider = registrations.Count == 0 ? original.Images : Images;
            ProfessionGameObjectPoolBridge.Provider = registrations.Count == 0 ? original.Pool : Pool;
            var skin = original.SkinClip;
            foreach (var entry in registrations)
                if (entry.Root && entry.Bindings.SkinClip != null) skin = entry.Bindings.SkinClip;
            ProfessionAppearanceSkinClipBindings.Current = skin;
            if (registrations.Count == 0) original = null;
        }

        private static string Localize(string key)
        {
            int previous = localizeCursor;
            try
            {
                for (int index = Math.Min(previous - 1, registrations.Count - 1); index >= 0; index--)
                {
                    var entry = registrations[index];
                    if (!entry.Root || entry.Bindings.Localize == null) continue;
                    localizeCursor = index;
                    string value = entry.Bindings.Localize(key);
                    if (value != null && value != key) return value;
                }
                return original?.Localize?.Invoke(key) ?? key;
            }
            finally { localizeCursor = previous; }
        }

        private static TMP_FontAsset Font()
        {
            for (int index = registrations.Count - 1; index >= 0; index--)
                if (registrations[index].Root && registrations[index].Bindings.Font != null)
                {
                    var font = registrations[index].Bindings.Font();
                    if (font) return font;
                }
            return original?.Font?.Invoke();
        }

        private static Registration Owner(Transform node, bool pool)
        {
            // Traversing parents gives nested scopes precedence over later peers.
            for (var current = node; current; current = current.parent)
                for (int index = registrations.Count - 1; index >= 0; index--)
                {
                    var entry = registrations[index];
                    if (entry.Root == current && (pool ? entry.Bindings.Pool != null : entry.Bindings.Images != null))
                        return entry;
                }
            return null;
        }

        private static IProfessionImageResourceLoader Images(Image image)
        {
            var entry = Owner(image ? image.transform : null, false);
            return entry != null ? entry.Bindings.Images(image) : original?.Images?.Invoke(image);
        }

        private static GameObject Pool(Transform scope, GameObject prefab, Transform parent, bool keepWorld)
        {
            var entry = Owner(scope, true);
            if (entry != null) return entry.Bindings.Pool(scope, prefab, parent, keepWorld);
            if (original?.Pool != null) return original.Pool(scope, prefab, parent, keepWorld);
            throw new InvalidOperationException("No live official UI pool binding owns the request node.");
        }
    }
}
