using System;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public static class HomeFeatureRuntimeTreasureGachaSource
    {
        public static TreasureGachaSourceNavigation OpenTreasureGachaFromElementItemSource(
            this HomeFeatureRuntime runtime, int sourceItemId, int sourceElementType,
            Action<int> returnToSource = null)
        {
            if (runtime == null || runtime.TreasureGacha == null || runtime.Hud == null) return null;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            TreasureGachaSourceNavigation navigation = runtime.TreasureGacha.ResolveElementItemSource(
                sourceItemId, sourceElementType, now);
            if (navigation == null) return null;
            TreasureGachaResult selected = runtime.TreasureGacha.SelectElementType(
                navigation.SelectedElementGachaType,
                "treasure-gacha:source-element:" + Guid.NewGuid().ToString("N"));
            if (!selected.IsApplied && selected.Code != TreasureGachaResultCode.Unchanged) return null;
            runtime.Hud.ShowTreasureGachaFromItemSource(sourceItemId, 0,
                navigation.SelectedElementGachaType, returnToSource);
            return navigation;
        }
    }
}
