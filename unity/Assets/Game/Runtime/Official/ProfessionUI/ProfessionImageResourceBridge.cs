using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
    // The resource boundary preserves the consumed official operation contract.
    public interface IProfessionAssetOperation<T> : IDisposable where T : UnityEngine.Object
    {
        bool IsValid { get; }
        int Status { get; }
        T Result { get; }
        event Action<IProfessionAssetOperation<T>> Completed;
    }
    public interface IProfessionImageResourceLoader
    {
        IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object;
        IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object;
        IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object;
        IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object;
    }
    public static class ProfessionImageResourceBridge
    {
        public static Func<Image, IProfessionImageResourceLoader> Provider { get; set; }
        public static IProfessionImageResourceLoader Resolve(Image image)
        {
            if (Provider == null) throw new InvalidOperationException("Original profession image resource provider is unbound.");
            return Provider(image);
        }
    }
    // APK R3.DisposableBag.Clear 0x108d1bc0 / Dispose 0x108d1cd4:
    // ascending disposal using the current count; clear storage/count afterwards,
    // and set the disposed flag after Clear. Preserve reentrant additions.
    internal sealed class ProfessionResourceOperationBag : IDisposable
    {
        private List<IDisposable> values;
        private int count;
        private bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else
            {
                if (values == null) values = new List<IDisposable>();
                values.Add(value); count++;
            }
            return value;
        }
        public void Clear()
        {
            if (values == null) return;
            for (int i = 0; i < count; i++) values[i]?.Dispose();
            values = null; count = 0;
        }
        public void Dispose() { Clear(); disposed = true; }
    }
}
