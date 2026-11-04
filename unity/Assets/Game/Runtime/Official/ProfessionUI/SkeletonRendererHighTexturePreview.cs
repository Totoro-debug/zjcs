using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Original application preview used by the two G_073 weapon prefabs.
    // Those prefabs have an explicit Low loader and no AppearanceCombineSkin;
    // primaryWeapon has no default skin. Preserve those original branches.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SkeletonRendererHighTexturePreview : MonoBehaviour
    {
        [SerializeField] private Component mSkeletonRenderer;
        [SerializeField] private YooAssetTextureLoader mTextureLoader;
        private IProfessionAppearanceRendererLite Renderer => (IProfessionAppearanceRendererLite)mSkeletonRenderer;
        private void Awake() => mSkeletonRenderer = GetComponent<IProfessionAppearanceRendererLite>() as Component;
        private void OnEnable() { TryUpdateMaterials(); TryUpdateMaterials(); }
        private void OnDestroy()
        {
            if (mSkeletonRenderer) Renderer.CustomMaterialOverride.Clear();
        }
        public void TryUpdateMaterials()
        {
            if (!mTextureLoader) return;
            Renderer.Initialize(false, false);
            var overrides = Renderer.CustomMaterialOverride;
            overrides.Clear(); mTextureLoader.TryInit();
            var skin = Renderer.Skeleton?.Skin;
            if (skin == null) return;
            foreach (var original in Renderer.SkinMaterials(skin))
            {
                if (overrides.ContainsKey(original)) continue;
                var material = new Material(original); material.name += "_instance";
                overrides[original] = material;
                if (!mTextureLoader.PlaceholderTextureIndexDict.TryGetValue(material.mainTexture, out var index)) continue;
                var mapping = mTextureLoader.placeholderMap[index];
                material.mainTexture = mapping.targetTextureReference.AssetReference.Load();
                if (material.HasTexture(AppearanceMaterialUtils.sDyeTexId))
                    material.SetTexture(AppearanceMaterialUtils.sDyeTexId, mapping.dyeTextureReference.AssetReference.Load());
            }
        }
    }
}
