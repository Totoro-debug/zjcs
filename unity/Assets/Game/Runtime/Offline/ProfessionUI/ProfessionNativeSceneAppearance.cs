using System;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Native alternative to the SDK-specific Mecanim/Animation subclasses. The
    // caller supplies the completed pose after an original skin setup reset.
    // No animation clock, mixer, physics solver or cache is simulated here.
    [RequireComponent(typeof(ProfessionNativeSceneRenderer))]
    public sealed class ProfessionNativeSceneAppearance : AppearanceRenderer
    {
        private Action<ProfessionNativeAppearanceSkeleton> applyPose;
        public void Configure(OfflineProfessionSceneAppearanceBindings bindings, AppearanceRendererPartHandler parts,
            Action<ProfessionNativeAppearanceSkeleton> applyPose)
        {
            this.applyPose = applyPose ?? throw new ArgumentNullException(nameof(applyPose));
            base.Configure(bindings, Guid<SkinTextureAsset>(bindings.Resources.SkinGuid),
                Guid<YooAssetTextureLoader>(bindings.Resources.LoaderGuid), parts);
        }
        private static AssetGuidProxy<T> Guid<T>(string value) where T : UnityEngine.Object
            => JsonUtility.FromJson<AssetGuidProxy<T>>("{\"mAssetGuid\":\"" + value + "\"}");
        protected override void OnSkinUpdated()
        {
            if (applyPose == null) throw new InvalidOperationException("Bind a native completed-pose producer before applying character skins.");
            applyPose(GetComponent<ProfessionNativeSceneRenderer>().State);
            base.OnSkinUpdated();
        }
    }
}
