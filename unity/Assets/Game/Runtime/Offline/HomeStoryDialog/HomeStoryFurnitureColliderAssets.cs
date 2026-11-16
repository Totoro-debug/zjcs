using System;
using UnityEngine;

namespace Zjcs.Offline.HomeStoryDialog
{
    // Serialized references to the selected original furniture collision subtree.
    public sealed class HomeStoryFurnitureColliderAssets : ScriptableObject
    {
        public GameObject DiningTableUnrepairedTouch;
        public Vector3 DiningTableWorldPosition;

        public static HomeStoryFurnitureColliderAssets LoadDefault()
        {
            var assets = Resources.Load<HomeStoryFurnitureColliderAssets>("Home/HomeStoryFurnitureColliderAssets");
            if (!assets || !assets.DiningTableUnrepairedTouch)
                throw new InvalidOperationException("The original dining-table story collider is not packaged.");
            return assets;
        }
    }
}
