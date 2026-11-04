using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Application interfaces only. Pose and geometry remain owned by the native actor.
    public sealed class PartnerNativeBattleRendererPort : MonoBehaviour,
        IProfessionFollowerRenderer, IProfessionAppearanceRendererLite
    {
        private ProfessionNativeAppearanceSkeleton skeleton;
        private Action update;
        private Material material;
        private int invisibleMode;
        private float spacing;
        private string dataName;
        private MeshRenderer visibleRenderer;
        public Component Component => this;
        public IProfessionAppearanceSkeleton Skeleton => skeleton;
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => skeleton;
        public string SkeletonDataName => dataName;
        public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
        public int SeparatorSlotCount => 0;
        public bool UseCustomZSpacing => false;
        public float ZSpacing => spacing;
        public int UpdateWhenInvisible => invisibleMode;
        public int UpdateMode => gameObject.activeInHierarchy && visibleRenderer.enabled
            && visibleRenderer.isVisible ? 3 : invisibleMode;
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;

        internal void Bind(ProfessionNativeAppearanceSkeleton value, Material sourceMaterial,
            int originalInvisibleMode, float originalSpacing, string originalDataName, Action updateMesh,
            MeshRenderer originalVisibleRenderer = null)
        {
            if (skeleton != null) throw new InvalidOperationException("Original partner renderer is already bound.");
            skeleton = value ?? throw new ArgumentNullException(nameof(value));
            material = sourceMaterial; invisibleMode = originalInvisibleMode; spacing = originalSpacing;
            dataName = originalDataName; update = updateMesh;
            visibleRenderer = originalVisibleRenderer ? originalVisibleRenderer : GetComponent<MeshRenderer>();
            OnAnimationRebuild?.Invoke(this);
        }
        public void Initialize(bool overwrite, bool quiet)
        {
            if (skeleton == null) throw new InvalidOperationException("Bind original partner pose before following a bone.");
            if (overwrite) throw new NotSupportedException("Recreate the native actor to replace its original skeleton.");
        }
        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            if (!ReferenceEquals(skin, skeleton.Skin)) throw new NotSupportedException("Bind original partner skin materials before switching skins.");
            yield return material;
        }
        public void UpdateAnimationAndMesh() { update(); OnCustomDemandLoading?.Invoke(this); }
    }
}
