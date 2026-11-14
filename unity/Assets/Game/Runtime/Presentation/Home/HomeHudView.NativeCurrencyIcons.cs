using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeCurrencyUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        // The quick-buy/exchange prefabs use plain currency icons, without
        // source hints, merge actions, timed items or asynchronously loaded effects.
        private sealed class NativeCurrencyIcons : IProfessionItemIconContext, IProfessionItemPlayer, IProfessionItemColors
        {
            private readonly HomeHudView view;
            private readonly OfflineHomeCurrencyAssets assets;
            private readonly OfflineHomeCurrencyWindowPlayer player;
            public NativeCurrencyIcons(HomeHudView view, OfflineHomeCurrencyAssets assets, OfflineHomeCurrencyWindowPlayer player)
            { this.view = view; this.assets = assets; this.player = player; }
            public ItemIconConfig Config => assets.iconConfig;
            public UnityEngine.Object GrayConfig => null;
            public IProfessionItemColors Colors => this;
            public IProfessionItemPlayer Player => this;
            public IProfessionItemInfo GetItemInfo(int itemId) => new NativeCurrencyInfo(assets.ItemRow(itemId));
            public object Compact(long count) => CultureNumberArg.Compact(count);
            public object CultureText(string key) => assets.Text(key);
            public string GetItemNameKey(int itemId) => "item_" + itemId + "_name";
            public bool TryGetSourceData(int itemId, out IReadOnlyList<IProfessionItemSource> sources)
                => throw Unbound("source hints");
            public bool CanMerge(int itemId, bool recursion) => throw Unbound("merge");
            public object GetLocationInfos(int itemId) => throw Unbound("map sources");
            public bool CanGetItemFromMapLocation(object location) => throw Unbound("map sources");
            public void SetUIGray(UnityEngine.Object config, GameObject target, bool gray) => throw Unbound("gray effects");
            public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unbound("frame sound switches");
            public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
            {
                var root = UnityEngine.Object.Instantiate(prefab, parent, false);
                if ((flag & EUIInstantiateFlag.Position) != 0) root.transform.localPosition = Vector3.zero;
                if ((flag & EUIInstantiateFlag.Rotation) != 0) root.transform.localRotation = Quaternion.identity;
                if ((flag & EUIInstantiateFlag.Scale) != 0) root.transform.localScale = Vector3.one;
                if ((flag & EUIInstantiateFlag.AutoActive) != 0) root.SetActive(true);
                return root;
            }
            public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unbound("asynchronous icon effects");
            public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback)
                => view.ShowNativeCurrencyDetail(data.Id, required);
            public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback)
                => view.ShowNativeCurrencyDetail(info.ClassId, required);
            public long GetItemAmount(int itemId) => player.GetItemAmount(itemId);
            public event Action<IProfessionItemData, long, object> OnItemCountUpdate
            { add { player.OnItemCountUpdate += value; } remove { player.OnItemCountUpdate -= value; } }
            public Color Green => assets.Green;
            public Color White => assets.White;
            public Color LightRed => assets.LightRed;
            public Color GetTextColor(ThemeColorType theme, ColorType type) => assets.TextColor(theme, type);
            private static NotSupportedException Unbound(string name) => new NotSupportedException("Currency icon branch is outside the bound quick-buy/exchange scope: " + name);
        }

        private sealed class NativeCurrencyInfo : IProfessionItemInfo
        {
            private readonly JObject row;
            public NativeCurrencyInfo(JObject row) { this.row = row; }
            public int ClassId => (int)row["ClassId"];
            public string Icon => (string)row["RawIcon"];
            public string IconEffect => (string)row["IconEffect"];
            public Quality Quality => (Quality)(int)row["Quality"];
            public bool CanOverlap => (bool)row["CanOverlap"];
            public object GetFakeIcon() => null;
        }
        private sealed class NativeCurrencyItem : IProfessionItemData
        {
            public NativeCurrencyItem(int id, long count, IProfessionItemInfo info) { Id = id; Count = count; Info = info; }
            public int Id { get; }
            public long Count { get; }
            public IProfessionItemInfo Info { get; }
            public Quality GetQuality() => Info.Quality;
            public bool CanExpire() => false;
        }
    }
}
