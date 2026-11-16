using UnityEngine;

namespace Zjcs.Offline.HomeUI
{
    // Bindings for the recovered original dialogue option subtree. The complete
    // dialogue panel owns its lifetime and supplies the original services.
    public sealed class HomeStoryOptionAssets : ScriptableObject
    {
        public GameObject OptionSlots;
        public Sprite IconArrow1;

        public static HomeStoryOptionAssets LoadDefault()
        {
            var assets = Resources.Load<HomeStoryOptionAssets>("Home/HomeStoryOptionAssets");
            if (!assets || !assets.OptionSlots || !assets.IconArrow1)
                throw new System.InvalidOperationException("The original story option assets are not bound.");
            return assets;
        }
    }
}
