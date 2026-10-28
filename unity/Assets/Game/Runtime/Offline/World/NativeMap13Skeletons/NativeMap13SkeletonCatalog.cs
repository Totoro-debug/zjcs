using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    public sealed class NativeMap13SkeletonCatalog : ScriptableObject
    {
        [Serializable] public sealed class Model { public string key; public NativeMap13SkeletonData data; }
        [Serializable] public sealed class Profile
        {
            public string cab, path_id, model, managed_type;
            public NativeMap13Renderer fields;
        }
        public Model[] models;
        public Profile[] renderers;
        public NativeMap13SkeletonData Require(string key)
        {
            foreach (var model in models) if (model.key == key && model.data) return model.data;
            throw new InvalidOperationException("Original Map13 skeleton is not bound: " + key);
        }
    }
}
