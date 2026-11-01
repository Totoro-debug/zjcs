using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
    // Original application operations at the renderer/Mecanim/resource boundary.
    // Animation SDK algorithms and the cache implementation are not embedded here.
    public interface IProfessionAppearanceSceneRenderer : IProfessionAppearanceRendererLite
    {
        ScriptableObject skeletonDataAsset { get; set; }
        event Action<Material[]> OnGetUpdatedSharedMaterials;
        void LateUpdateMesh();
    }
    public interface IProfessionAppearanceSceneMecanim : IProfessionAppearanceSceneRenderer
    {
        Animator Animator { get; }
        void Update(float deltaTime);
    }
    public interface IProfessionAppearanceSceneCache
    {
        void OnSkinUpdate();
        void Clear();
        void OnDestroy();
    }
    public interface IProfessionAppearanceSceneBindings : IProfessionAppearanceSpineBindings
    {
        IAppearanceAssetManager AssetManager { get; }
        IProfessionAppearanceCharacterAsset LoadAssetByGuid(string guid);
        IProfessionAppearanceSceneCache CreateMecanimCache(IProfessionAppearanceSceneMecanim mecanim);
    }
    public static class ProfessionAppearanceSceneBindings
    {
        public static IProfessionAppearanceSceneBindings Current { get; set; }
        public static IProfessionAppearanceSceneBindings Required => Current ??
            throw new InvalidOperationException("Original scene appearance resources, renderer and Mecanim cache are unbound.");
    }
}
