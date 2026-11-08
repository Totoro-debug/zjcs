using System;
using TMPro;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed class OfficialArenaResultAssets : ScriptableObject
    {
        public GameObject Panel;
        public TextAsset Bindings;
        public OfflineProfessionAudioCatalog Audio;
        public TMP_FontAsset Font;
        public static OfficialArenaResultAssets Load() => Resources.Load<OfficialArenaResultAssets>("Combat/ArenaResult/catalog")
            ?? throw new InvalidOperationException("Original arena result assets are unbound.");
    }
}
