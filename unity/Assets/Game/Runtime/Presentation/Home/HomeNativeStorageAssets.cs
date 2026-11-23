using UnityEngine;
using Zjcs.Offline.NativeSpine;

namespace Zjcs.Presentation.Home
{
    internal sealed class HomeNativeStorageAssets : ScriptableObject
    {
        [SerializeField] NativeSpineAppearanceCatalog weaponAppearance;
        public NativeSpineAppearanceCatalog WeaponAppearance => weaponAppearance;
    }
}
