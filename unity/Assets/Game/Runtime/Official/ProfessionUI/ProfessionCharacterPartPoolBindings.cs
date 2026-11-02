using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Framework operations used by the original application pool. The native
    // prefab definition is its verified exported TextAsset, never a dummy GO.
    public interface IProfessionCharacterPartPrefab : IDisposable
    {
        TextAsset Result { get; }
        void Release(bool tryUnloadAllAssets);
    }
    public interface IProfessionCharacterPartPoolBindings
    {
        bool UsePool { get; }
        IProfessionCharacterPartPrefab LoadPrefab(string path);
        GameObject Instantiate(IProfessionCharacterPartPrefab source, Transform parent);
        void PrepareRent(GameObject instance, Transform parent);
        void PrepareReturn(GameObject instance);
        void Destroy(GameObject instance, bool tryUnloadAllAssets);
    }
}
