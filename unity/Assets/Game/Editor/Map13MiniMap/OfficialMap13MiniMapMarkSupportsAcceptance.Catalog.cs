using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMap13MiniMapMarkSupportsAcceptance
    {
        private static MainlineMiniMapMarkerCatalog VerifyMarkerCatalog(NativeMap13AssetCatalog assets)
        {
            var source = Resources.Load<TextAsset>("MainlineMiniMap/current-marker-data");
            Require(source, "Selected original marker query data");
            var original = source.text;
            var catalog = new MainlineMiniMapMarkerCatalog(source, assets);
            var second = new MainlineMiniMapMarkerCatalog(source, assets);
            const string prefix = "UI/Map/MiniMap/MiniMapMark/";
            Require(catalog.GetMarker(5007, 12, null, null) == prefix + "MiniMapMonster4_2",
                "Original map-specific monster icon overrides extension");
            Require(catalog.GetMarker(5007, 10, "unlisted-area", "unlisted-tag") == prefix + "MiniMapMonster4",
                "Missing display row falls back to original extension");
            foreach (var pair in new[] { (11, "MiniMapGodMonster_1"), (12, "MiniMapGodMonster_2"), (13, "MiniMapGodMonster_3") })
                Require(catalog.GetMarker(5008, pair.Item1, "unlisted-area", "unlisted-tag") == prefix + pair.Item2,
                    "Original map override and IgnoreKey/empty-tag fallback: " + pair.Item1);
            var bamboo = catalog.GetDisplay(6000, 14, "Bamboo", null);
            Require(bamboo.DialogIconPrefab == "UI/WorldMap/WorldStageDialogIcon/14/6000_14_Bamboo_DialogIcon"
                && string.IsNullOrEmpty(bamboo.MiniMapMark) && catalog.GetMarker(6000, 14, "Bamboo", null) == prefix + "MiniMapEntry",
                "Original area row selected; empty marker override retains extension");
            Require(catalog.GetDisplay(6000, 14, "unlisted-area", null).DialogIconPrefab == "UI/WorldMap/WorldStageDialogIcon/14/6000_14_DialogIcon",
                "Original IgnoreKey area fallback retains the selected map");
            Require(catalog.GetDisplay(5101, 16, null, null).IconHeadPrefab == "WorldStageIconhead/5101_IconHead",
                "Original zero-map fallback selects the common NPC display");
            foreach (var map in new[] { 10, 11, 12, 13, 14, 16 })
                Require(catalog.GetMarker(6000, map, null, null) == prefix + "MiniMapEntry", "Shared lookup for map " + map);
            Require(catalog.GetDisplay(int.MaxValue, 16, null, null) == null
                && catalog.GetExtend(int.MaxValue) == null && catalog.GetMarker(int.MaxValue, 16, null, null) == null,
                "Missing original entity does not acquire an invented icon");
            catalog.GetExtend(6000).MiniMapMark = "changed-by-caller";
            Require(catalog.GetExtend(6000).MiniMapMark == prefix + "MiniMapEntry"
                && second.GetMarker(5007, 12, null, null) == prefix + "MiniMapMonster4_2" && source.text == original,
                "Instance-owned initialization and extension projection preserve the shared source");
            Require(catalog.LoadPrefab(catalog.GetMarker(5007, 12, null, null)) == assets.Load<GameObject>(prefix + "MiniMapMonster4_2.prefab"),
                "Original selected address resolves to the native prefab identity");
            Debug.Log("Shared original marker catalog: six maps, map/area overrides, fallback and independent instances passed.");
            return catalog;
        }
    }
}
