// Selected original currency-window implementation; typed local service boundaries.
using Object=UnityEngine.Object;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
using StateSwitcher=Zjcs.Official.WorldUI.WorldStateSwitcher<bool>;
using PlayerItemDataWrap=Zjcs.Official.ProfessionUI.IProfessionItemData;
using AwardSourceData=System.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeCurrencyWindowsUI
{
public struct HomeCurrencyDisposableBag : IDisposable
{
    IDisposable[] items;
    bool isDisposed;
    int count;

    public HomeCurrencyDisposableBag(int capacity)
    {
        this = default;
        items = new IDisposable[capacity];
    }

    public T Add<T>(T item) where T : IDisposable
    {
        if (isDisposed)
        {
            item.Dispose();
            return item;
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
        return item;
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
