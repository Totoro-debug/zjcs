using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFurniture
{
    public interface IHomeFurniturePrefabHandle : IDisposable { GameObject Result { get; } }

    public interface IHomeWeaponRackServices
    {
        IProfessionPlayerAppearanceSource MainPlayer { get; }
        IProfessionPlayerAppearanceSource PlayerData { get; }
        ProfessionAppearanceResolver Appearance { get; }
        ProfessionAppearanceTables Tables { get; }
        IDisposable SubscribeHomeChange(Action action);
        IHomeFurniturePrefabHandle LoadPrefab(string originalLocation);
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
    }

    public interface IHomeFurnitureViewModes
    {
        HomeViewMode HomeViewMode { get; }
        IDisposable Subscribe(Action<HomeViewMode> action);
    }

    public interface IHomeFurnitureActionEvents
    {
        IDisposable Subscribe(Action<ActionEventData> action);
    }

    // Local resource handles and the home-change subscription retain the same
    // controller lifetime and acquisition order as the original DisposableBag.
    internal sealed class HomeFurnitureLifetime : IDisposable
    {
        readonly List<IDisposable> values = new List<IDisposable>();
        public T Add<T>(T value) where T : IDisposable { values.Add(value); return value; }
        public void Dispose()
        {
            foreach (IDisposable value in values) value?.Dispose();
            values.Clear();
        }
    }
}
