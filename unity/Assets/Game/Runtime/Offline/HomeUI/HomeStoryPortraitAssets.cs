using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    // Preserve the GUID-based loads in the original PortraitFace components.
    public sealed class HomeStoryPortraitAssets : ScriptableObject, IHomeHudResourceLoader
    {
        [Serializable] public sealed class SpriteBinding { public string Guid; public Sprite Sprite; }
        public GameObject Portrait8402;
        public GameObject Portrait8411;
        public SpriteBinding[] Sprites;
        private Dictionary<string, Sprite> sprites;

        public static HomeStoryPortraitAssets LoadDefault()
        {
            var assets = Resources.Load<HomeStoryPortraitAssets>("Home/HomeStoryPortraitAssets");
            if (!assets || !assets.Portrait8402 || !assets.Portrait8411 || assets.Sprites == null)
                throw new InvalidOperationException("The original story portrait assets are not bound.");
            return assets;
        }

        public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T : Object
        {
            if (sprites == null)
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (var entry in Sprites)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Guid) || !entry.Sprite)
                        throw new InvalidOperationException("An original story portrait Sprite is not bound.");
                    sprites.Add(entry.Guid, entry.Sprite);
                }
            }
            if (!sprites.TryGetValue(guid, out var sprite) || !(sprite is T value))
                throw new InvalidOperationException("Unbound original story portrait resource: " + guid);
            return new Handle<T>(value);
        }

        public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object
            => LoadAssetByGuid<T>(guid);

        private sealed class Handle<T> : IHomeHudAssetHandle<T> where T : Object
        {
            private bool valid = true;
            public T Result { get; }
            public bool IsValid => valid && Result;
            public int Status => 2;
            public event Action<IHomeHudAssetHandle<T>> Completed { add { value?.Invoke(this); } remove { } }
            public Handle(T result) { Result = result; }
            public T WaitForAsyncComplete() => Result;
            public void Dispose() => valid = false;
        }
    }
}
