using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed class HomeNativeEnvironmentAssets : ScriptableObject
    {
        public GameObject Day;
        public GameObject Night;
        public AnimationClip DayClip;
        public AnimationClip NightClip;
        public float Duration;
        public MoonSprites[] Moons;

        [System.Serializable]
        public sealed class MoonSprites
        {
            public int Index;
            public Sprite Moon;
            public Sprite Outline;
            public Sprite Glow;
        }
    }
}
