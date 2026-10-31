using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public struct EffectPartEntry
    {
        private IProfessionAppearancePartAsset mHandle;
        public GameObject Go { get; }

        public EffectPartEntry(GameObject go, IProfessionAppearancePartAsset handle)
        {
            mHandle = handle;
            Go = go;
        }

        public void Release(bool tryUnloadAllAssets = false)
        {
            if (Go != null) Object.Destroy(Go);
            mHandle?.Release(tryUnloadAllAssets);
        }
    }
}
