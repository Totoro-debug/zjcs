using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureDetailAssets:ScriptableObject
    {
        public GameObject Prefab;
        public OfflineProfessionImageCatalog Images;
        public TextAsset Configuration;
        public TextAsset PropertyConfiguration;
        public OfflineTreasureSuitAssets Suit;
        public static OfflineTreasureDetailAssets LoadDefault()
            =>Resources.Load<OfflineTreasureDetailAssets>("Treasure/NativeDetail/OfflineTreasureDetailAssets")
                ??throw new InvalidOperationException("Original treasure detail resources are absent.");
    }
}
