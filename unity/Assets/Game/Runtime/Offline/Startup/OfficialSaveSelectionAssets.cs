using UnityEngine;

namespace Zjcs.Offline.Startup
{
    public sealed class OfficialSaveSelectionAssets : ScriptableObject
    {
        public GameObject panel, roleItem, blurMask;
        public TextAsset photography;
        public Sprite defaultPortrait;
        public const string ResourcePath = "SaveSelection/OfficialSaveSelectionAssets";
    }
}
