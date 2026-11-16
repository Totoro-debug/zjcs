using System;
using UnityEngine;

namespace Zjcs.Offline.HomeStory
{
    public sealed class HomeStoryCharacterAssets : ScriptableObject
    {
        public GameObject CharacterGroup;

        public static HomeStoryCharacterAssets LoadDefault()
        {
            var assets = Resources.Load<HomeStoryCharacterAssets>("Home/HomeStoryCharacterAssets");
            if (!assets || !assets.CharacterGroup)
                throw new InvalidOperationException("The original story character group is not bound.");
            return assets;
        }
    }
}
