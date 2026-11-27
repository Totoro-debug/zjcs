using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.TreasureUI
{
    // Packaged original GUID bindings and text. This catalog supplies assets, never layout or rules.
    public sealed class OfflineTreasureSlotsAssets : ScriptableObject,IHomeHudResourceLoader
    {
        [Serializable] public sealed class SpriteBinding {public string Guid;public Sprite Sprite;}
        [Serializable] public sealed class TextBinding {public string Key,Value;}
        public SpriteBinding[] Sprites;
        public TextBinding[] Texts;
        private Dictionary<string,Sprite> sprites;
        private Dictionary<string,string> texts;
        public static OfflineTreasureSlotsAssets LoadDefault()
            =>Resources.Load<OfflineTreasureSlotsAssets>("Treasure/NativeSlots/OfflineTreasureSlotsAssets")
                ??throw new InvalidOperationException("Original treasure slot background bindings are unavailable.");
        public string Text(string key)
        {
            if(texts==null){texts=new Dictionary<string,string>();foreach(var row in Texts)texts.Add(row.Key,row.Value);}
            return texts.TryGetValue(key,out var value)?value:key;
        }
        public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string key) where T:Object
        {
            if(sprites==null){sprites=new Dictionary<string,Sprite>();foreach(var row in Sprites)sprites.Add(row.Guid,row.Sprite);}
            if(!sprites.TryGetValue(key,out var sprite)||!(sprite is T value))
                throw new InvalidOperationException("Unbound original treasure slot resource: "+key);
            return new Handle<T>(value);
        }
        public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string key) where T:Object=>LoadAssetByGuid<T>(key);
        private sealed class Handle<T>:IHomeHudAssetHandle<T> where T:Object
        {
            private T value;public Handle(T value)=>this.value=value;
            public bool IsValid=>value!=null;
            public int Status=>IsValid?2:0;
            public T Result=>value!=null?value:throw new ObjectDisposedException(nameof(Handle<T>));
            public T WaitForAsyncComplete()=>Result;
            public event Action<IHomeHudAssetHandle<T>> Completed{add{if(IsValid)value?.Invoke(this);else throw new ObjectDisposedException(nameof(Handle<T>));}remove{}}
            public void Dispose()=>value=null;
        }
    }
}
