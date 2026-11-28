using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasurePromoteAssets : ScriptableObject
    {
        [Serializable] public sealed class IconEntry { public int Id; public GameObject Prefab; }
        [Serializable] public sealed class TextEntry { public string Key,Value; }
        [Serializable] public sealed class CurrencyEntry { public PlayerItemType Type;public int ItemId;public bool HasAutoIncrease; }
        public GameObject Prefab,AwakeMaterialPrefab,DefaultIconPrefab;
        public ItemIconConfig IconConfig;
        public OfflineProfessionImageCatalog Images;
        public IconEntry[] ItemTypes,ItemOverrides;
        public TextEntry[] Texts;
        public Color LightRed;
        public OfficialHomeTopBarAssetCatalog.Currency[] Currencies;
        public CurrencyEntry[] CurrencyItems;
        public GameObject IconPrefab(int itemId,int itemType)
        {
            var entry=ItemOverrides.FirstOrDefault(row=>row.Id==itemId)??ItemTypes.FirstOrDefault(row=>row.Id==itemType);
            return entry==null?DefaultIconPrefab:entry.Prefab?entry.Prefab:
                throw new InvalidOperationException("Bind the original icon type before using it in a promotion window: "+itemType);
        }
        public static OfflineTreasurePromoteAssets LoadDefault()=>Resources.Load<OfflineTreasurePromoteAssets>("Treasure/NativePromote/OfflineTreasurePromoteAssets")
            ??throw new InvalidOperationException("The original treasure promotion assets are unavailable.");
    }
}
