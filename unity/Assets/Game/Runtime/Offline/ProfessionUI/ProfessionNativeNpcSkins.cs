using System;
using System.Collections.Generic;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSkeletonData
    {
        public string[] NpcSkinNames
        {
            get
            {
                var names = new string[skins.Length];
                for (int i = 0; i < skins.Length; i++) names[i] = skins[i].Name;
                return names;
            }
        }

        public ProfessionNativeSkin CreateCombinedNpcSkin(IEnumerable<string> names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            var combined = new ProfessionNativeSkin("combineSkin", SlotCount, BoneCount, Constraints.Count,
                System.Array.Empty<ProfessionNativeSkin.Entry>(), System.Array.Empty<int>(), System.Array.Empty<int>());
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                ProfessionNativeSkin skin = SkinCatalog.FindSkin(name) as ProfessionNativeSkin;
                if (skin != null) combined.AddSkin(skin);
            }
            return combined;
        }
    }
}
