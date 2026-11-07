using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed class OfficialDemonResultAssets : ScriptableObject
    {
        public const string ResourcePath = "Combat/DemonResult/catalog";
        public GameObject Panel, AwardIcon, ItemIcon, Boss;
        public ItemIconConfig ItemConfig;
        public Sprite RewardIcon;
        public TextAsset Bindings;
        public static OfficialDemonResultAssets Load() => Resources.Load<OfficialDemonResultAssets>(ResourcePath)
            ?? throw new InvalidOperationException("Original demon result assets are unbound.");
    }
}
