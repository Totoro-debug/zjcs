using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Official.HomeAwardUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyNativeFruitTreeSkins(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool passed = true;
            var skins = runtime.Rules.Data.furnitureSkins.Where(value => value.furnitureType == 4)
                .OrderBy(value => value.order).ToArray();
            var weather = runtime.Rules.Data.environmentSkins.Where(value => value.environmentType == 1
                && (value.isDefault || value.prefabName == "WeatherSnow")).ToArray();
            passed &= Check("native-fruit-tree-variant-scope", () =>
            {
                Assert(skins.Length == 6 && weather.Length == 2,
                    "selected source includes all six migrated fruit-tree styles and their normal/snow weather");
                return "all twelve migrated tree prefab variants will exercise the shared native scene flow";
            });
            using var loader = new HomeNativePrefabLoader();
            foreach (var skin in skins)
            foreach (var season in weather)
            {
                string variant = skin.prefabName + (season.prefabName == "WeatherSnow" ? "Snow" : string.Empty);
                string prefix = "native-fruit-tree-" + variant;
                float deadline = Time.realtimeSinceStartup + 6f;
                while (runtime.Hud.Canvas.GetComponentsInChildren<AwardListPanel>().Any(value => value.isActiveAndEnabled)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                bool switched = Check("native-fruit-tree-switch-" + variant, () =>
                {
                    Assert(!runtime.Hud.Canvas.GetComponentsInChildren<AwardListPanel>().Any(value => value.isActiveAndEnabled),
                        "previous original award animation closes before the next tree variant");
                    Assert(runtime.Session.Mutate("acceptance:fruit-tree-skin:" + variant, save =>
                    {
                        save.Skins.RemoveAll(value => value.Category == 0 && value.Type == 4
                            || value.Category == 2 && value.Type == 1);
                        save.Skins.Add(new OfflineHomeSkinSelection { Category = 0, Type = 4, SkinItemId = skin.skinItemId });
                        save.Skins.Add(new OfflineHomeSkinSelection { Category = 2, Type = 1, SkinItemId = season.skinItemId });
                    }), "isolated save selects the original tree skin and weather");
                    runtime.RefreshNativeFurnitureSkins();
                    return "native furniture refresh selects " + variant;
                });
                passed &= switched;
                if (!switched) continue;
                yield return null;
                using var prefab = loader.LoadPrefab("Home/Dynamic/Furniture/FruitTree/" + variant);
                if (!Check("native-fruit-tree-source-" + variant, () =>
                {
                    Assert(prefab.Result, "expected original variant prefab is available");
                    return "original source prefab retained for skeleton identity and transform comparison";
                })) { passed = false; continue; }
                yield return VerifyNativeFruitTree(runtime, value => passed &= value, prefix, prefab.Result);
            }
            completed?.Invoke(passed);
        }
    }
}
