using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerCombineSkinData
    {
        IEnumerable<string> SkinNames { get; }
        IProfessionAppearanceSkin DefaultSkin { get; }
        IProfessionAppearanceSkin FindSkin(string name);
    }
    public interface IPartnerCombineSkeletonComponent : IProfessionAppearanceSkeletonComponent
    { ScriptableObject SkeletonDataAsset { get; } }
    public interface IPartnerCombineAppearanceComponent
    {
        void Release();
        void SetSkins(IEnumerable<string> names);
    }
    public interface IPartnerCombineSkinBindings
    {
        Component GetGraphic(Component owner);
        Component GetRenderer(Component owner);
        IPartnerCombineSkeletonComponent GetSkeleton(Component owner);
        IPartnerCombineAppearanceComponent GetAppearance(Component owner);
        IPartnerCombineSkinData GetData(ScriptableObject asset, bool quiet);
        IProfessionAppearanceSkin CreateSkin(string name);
        void TrimRenderers(Component graphic);
        void MatchRectTransformWithBounds(Component graphic);
        void UpdateRenderer(Component renderer);
        int RandomRange(int minimum, int maximum);
    }
    public static class PartnerCombineSkinBindings
    {
        public static IPartnerCombineSkinBindings Current { get; set; }
        public static IPartnerCombineSkinBindings Required => Current ??
            throw new InvalidOperationException("Bind the original partner combination skin application ports.");
    }
}
