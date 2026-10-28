using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Implemented by the real appearance backend. These boundaries do not
    // instantiate a character, invent renderers or replace an original prefab.
    public interface IProfessionPortraitPhotographerBindings
    {
        PlayerPortraitSettings Settings { get; }
        ICharacterPortraitBuilder CreateBuilder(ProfessionPortraitKind type);
        IProfessionPortraitRendererGraph GetRendererGraph(Component animation);
        void ReleaseAppearance(Component appearance, bool tryUnloadAllAssets);
        string DescribeAppearance(object appearance);
    }

    public interface IProfessionPortraitRendererGraph
    {
        Component Root { get; }
        bool HasSeparator { get; }
        IReadOnlyList<MeshRenderer> SeparatedRenderers { get; }
        // Null means there is no part-handler component. Order is the original
        // handler's LoadedSkeletonDict enumeration order.
        IReadOnlyList<ProfessionPortraitRenderPart> LoadedParts { get; }
    }

    public readonly struct ProfessionPortraitRenderPart
    {
        public readonly Component SkeletonTarget;
        public readonly GameObject GameObject;
        public ProfessionPortraitRenderPart(Component skeletonTarget, GameObject gameObject)
        { SkeletonTarget = skeletonTarget; GameObject = gameObject; }
    }
}
