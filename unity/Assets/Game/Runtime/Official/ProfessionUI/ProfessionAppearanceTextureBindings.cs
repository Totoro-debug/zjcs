using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The original application proxy consumes these operations. No package
    // loader or skeleton SDK implementation crosses this boundary.
    public interface IProfessionUnsafeAssetHandle<T> where T : UnityEngine.Object
    {
        bool IsValid { get; }
        int Status { get; }
        T Result { get; }
        void WaitForAsyncComplete();
        void Release();
        event Action<IProfessionUnsafeAssetHandle<T>> Completed;
    }
    public interface IProfessionAppearanceTextureBindings
    {
        IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object;
        Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex);
    }
    public static class ProfessionAppearanceTextureBindings
    {
        public static IProfessionAppearanceTextureBindings Current { get; set; }
        public static IProfessionAppearanceTextureBindings Required => Current ??
            throw new InvalidOperationException("Original appearance texture/atlas resources are unbound.");
    }
}
