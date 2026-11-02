using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class ProfessionNativePortraitStudioResources : ScriptableObject
    {
        public GameObject StudioPrefab;
        public TextAsset Hierarchy;
        public PlayerPortraitSettings Settings;
        public AppearanceConfig AppearanceConfig;
        public ProfessionNativeSceneAppearanceResources Characters;
        public string PartSlot;
        public string SortingLayer;
        public int SortingOrder;

        public static ProfessionNativePortraitStudioResources LoadDefault()
        {
            var value = Resources.Load<ProfessionNativePortraitStudioResources>("Partners/OfficialPartnerPlayerPortraitResources");
            return value ? value : throw new InvalidOperationException("Original partner player-portrait studio is unbound.");
        }
    }
}
