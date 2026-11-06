using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IPlayerItemUIHandler
    {
        int ItemId { get; set; }
        bool ShowAddBtn { get; }
        void HandleClick(Vector2 mousePosition);
        void HandleAddBtn();
    }

    public interface IPlayerItemUIWithLimit
    {
        bool TryGetMaxCountLimit(out long maxLimitCount);
    }

    public interface IPlayerItemUICountProvider
    {
        long Count { get; }
        event Action<long, bool> OnItemCountUpdate;
        void Init();
        void Release();
    }

    // Original player/parser/resource/factory operations consumed by MePlayerItemUI.
    // The window's adapter supplies local profile data and item-detail navigation.
    public interface IPlayerItemUIHost
    {
        IPlayerItemUIHandler CreateHandler(PlayerItemType type, PlayerItemUIItemIdProvider provider);
        IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type, PlayerItemUIItemIdProvider provider);
        IDisposable LoadIcon(int itemId, Action<Sprite> loaded);
        Vector2 InputPosition { get; }
        object Compact(long count);
        string ColorFormat(string text, Color color);
    }
}
