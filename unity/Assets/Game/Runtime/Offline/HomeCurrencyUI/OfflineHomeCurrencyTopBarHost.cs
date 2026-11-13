using System;
using UnityEngine;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // The owner finishes its window/action construction before activating any
    // currency widget. Counts and handlers share that owner's actual profile.
    public sealed class OfflineHomeCurrencyTopBarHost : IPlayerItemUIHost
    {
        private readonly OfflineHomeCurrencyPlayer player;
        private readonly OfficialHomeTopBarAssetCatalog assets;
        private readonly Func<OfflineHomeCurrencyActions> actions;
        private readonly Func<Vector2> pointer;
        public OfflineHomeCurrencyTopBarHost(OfflineHomeCurrencyPlayer player,
            OfficialHomeTopBarAssetCatalog assets, Func<OfflineHomeCurrencyActions> actions, Func<Vector2> pointer)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
        }
        public IPlayerItemUIHandler CreateHandler(PlayerItemType type, PlayerItemUIItemIdProvider provider)
            => (actions() ?? throw new InvalidOperationException("Bind currency windows/actions before activation.")).CreateHandler(type, provider);
        public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type, PlayerItemUIItemIdProvider provider)
            => player.CreateCountProvider(type, provider);
        public Vector2 InputPosition => pointer();
        public object Compact(long count) => CultureNumberArg.Compact(count);
        // Original Common.LanguageUtils.SetTextColorFormatStr.
        public string ColorFormat(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        public IDisposable LoadIcon(int itemId, Action<Sprite> loaded)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var sprite = assets.Icon(itemId); loaded(sprite); return new IconLease(sprite);
        }
        private sealed class IconLease : IDisposable
        {
            private Sprite sprite;
            public IconLease(Sprite sprite) { this.sprite = sprite; }
            public void Dispose() { sprite = null; }
        }
    }
}
