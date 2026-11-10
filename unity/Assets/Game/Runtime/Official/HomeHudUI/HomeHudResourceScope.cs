using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zjcs.Official.HomeHudUI
{
    public interface IHomeHudAssetHandle<T> : IDisposable where T : Object
    {
        bool IsValid { get; }
        int Status { get; }
        T Result { get; }
        T WaitForAsyncComplete();
        event Action<IHomeHudAssetHandle<T>> Completed;
    }

    public interface IHomeHudResourceLoader
    {
        IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T : Object;
        IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object;
    }

    public sealed class HomeHudResourceScope : MonoBehaviour
    {
        public IHomeHudResourceLoader Loader { get; set; }
        public static IHomeHudResourceLoader ActiveSceneLoader { get; set; }

        public static IHomeHudResourceLoader Resolve(GameObject source)
            => source == null ? ActiveSceneLoader : source.GetComponentInParent<HomeHudResourceScope>()?.Loader;
    }
}
