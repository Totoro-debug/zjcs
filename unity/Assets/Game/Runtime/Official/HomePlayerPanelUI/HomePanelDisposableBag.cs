// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Threading;
public struct HomePanelDisposableBag : IDisposable
{
    IDisposable[] items;
    bool isDisposed;
    int count;

    public HomePanelDisposableBag(int capacity)
    {
        this = default;
        items = new IDisposable[capacity];
    }

    public void Add(IDisposable item)
    {
        if (isDisposed)
        {
            item.Dispose();
            return;
        }

        if (items == null)
        {
            items = new IDisposable[4];
        }
        else if (count == items.Length)
        {
            Array.Resize(ref items, count * 2);
        }

        items[count++] = item;
    }

    public void Clear()
    {
        if (items != null)
        {
            for (int i = 0; i < count; i++)
            {
                items[i]?.Dispose();
            }

            items = null;
            count = 0;
        }
    }

    public void Dispose()
    {
        Clear();
        isDisposed = true;
    }
}
}
