using UnityEngine;

namespace Zjcs.Presentation.Home
{
    // The selected Background prefab's original ClipDatas entries.
    public sealed class HomeNativeBackdropAssets : ScriptableObject
    {
        public GameObject Root;
        public AnimationClip Clip;
        public ClipBinding[] Additional;

        [System.Serializable]
        public sealed class ClipBinding
        {
            public string Name;
            public GameObject Root;
            public AnimationClip Clip;
        }
    }
}
