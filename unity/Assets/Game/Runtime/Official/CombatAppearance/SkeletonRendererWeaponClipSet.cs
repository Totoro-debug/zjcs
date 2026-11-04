using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
    // Original weapon material/particle clipping, with the renderer SDK behind its port.
    public sealed class SkeletonRendererWeaponClipSet : MonoBehaviour, ISkeletonClipSet
    {
        [SerializeField] private Component mAppearanceRenderer;
        private Material mOriginalMat;
        private Material mOverrideMat;
        private ParticleSystemRenderer[] mParticleSystemRenderers;
        private MaterialPropertyBlock mParticleMaterialPropertyBlock;
        private bool mSelfCreateMaterials;

        public void SetClipHeight(float height)
        {
            if (!mOverrideMat)
            {
                var partHandler = ((AppearanceRenderer)mAppearanceRenderer).PartHandler;
                if (partHandler && partHandler.LoadedSkeletonDict.TryGetValue(SkinType.pWeapon, out var value))
                {
                    GameObject target = value.Part.SkeletonTarget;
                    if (target.TryGetComponent<AppearanceRendererLite>(out var appearance))
                        mOverrideMat = appearance.GetIndependentMaterials()[0];
                    else if (target.TryGetComponent<IProfessionAppearanceRendererLite>(out var renderer))
                    {
                        mOriginalMat = renderer.Component.GetComponent<MeshRenderer>().sharedMaterial;
                        mOverrideMat = new Material(mOriginalMat);
                        renderer.CustomMaterialOverride[mOriginalMat] = mOverrideMat;
                        mSelfCreateMaterials = true;
                    }
                    mParticleSystemRenderers = value.Part.GameObject.GetComponentsInChildren<ParticleSystemRenderer>();
                    mParticleMaterialPropertyBlock = new MaterialPropertyBlock();
                }
            }
            if (!mOverrideMat) return;
            float y = transform.TransformPoint(new Vector2(0f, height)).y;
            mOverrideMat.SetFloat(AppearanceMaterialUtils.sWorldClipHeight, y);
            if (mParticleSystemRenderers != null)
            {
                mParticleMaterialPropertyBlock.SetFloat(AppearanceMaterialUtils.sWorldClipHeight, y);
                foreach (var renderer in mParticleSystemRenderers)
                    renderer.SetPropertyBlock(mParticleMaterialPropertyBlock);
            }
        }
        private void OnDestroy() { if (mSelfCreateMaterials) Destroy(mOverrideMat); }
    }
}
