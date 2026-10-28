using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionAppearanceCharacterAsset : IDisposable { ScriptableObject Result { get; } }
    public interface IProfessionAppearanceCharacterBindings
    {
        AppearanceConfig Config { get; }
        IProfessionAppearanceCharacterAsset LoadAsset(string path);
        void SetPartSkins(Component handler, Dictionary<SkinType, string> skins);
        void ReleaseParts(Component handler, bool tryUnloadAllAssets);
    }
    public static class ProfessionAppearanceCharacterBindings
    {
        public static IProfessionAppearanceCharacterBindings Current { get; set; }
        public static IProfessionAppearanceCharacterBindings Required => Current ??
            throw new InvalidOperationException("Original appearance character resources/parts are unbound.");
    }
}
