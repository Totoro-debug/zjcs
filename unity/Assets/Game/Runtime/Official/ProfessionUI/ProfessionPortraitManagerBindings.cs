using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Dependencies of the original background service. Implementations must bind
    // the original studio prefab and appearance data; this is not a renderer.
    public interface IProfessionPortraitStudio
    {
        Component Instance { get; }
        void Init(CharacterPortraitTexturePool pool);
        CharacterPortraitTexture2D CapturePortraitTexture(ICharacterPortraitRequestor requestor, out byte[] bytes);
    }

    public readonly struct ProfessionPortraitSkinHash
    {
        public readonly string SkinName;
        public readonly long HashValue;
        public ProfessionPortraitSkinHash(string name, long value) { SkinName = name; HashValue = value; }
    }

    public readonly struct ProfessionPortraitSkinName
    {
        public readonly int Id;
        public readonly string SkinName;
        public ProfessionPortraitSkinName(int id, string name) { Id = id; SkinName = name; }
    }

    public interface IProfessionPortraitManagerBindings
    {
        PlayerPortraitSettings Settings { get; }
        ICharacterPortraitPersister Persister { get; }
        GAction LowMemory { get; }
        IReadOnlyList<ProfessionPortraitSkinHash> AppearanceHashes { get; }
        IReadOnlyList<ProfessionPortraitSkinName> SkinNames { get; }
        IProfessionPortraitAppearanceReader AppearanceReader { get; }
        IProfessionPortraitStudio InstantiateStudio();
    }
}
