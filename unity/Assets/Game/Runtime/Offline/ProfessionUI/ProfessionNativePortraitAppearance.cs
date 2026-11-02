using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // A portrait is captured at the requested animation's frame zero after the
    // original builder refreshes its skins. It never advances an animation clock.
    [RequireComponent(typeof(ProfessionNativeSceneRenderer))]
    public sealed class ProfessionNativePortraitAppearance : AppearanceRenderer,
        ICharacterPortraitAnimation, IProfessionPortraitRendererGraph
    {
        private ProfessionNativeAnimationChannels animation;
        private bool loop;
        private ProfessionNativeSceneRenderer Renderer => GetComponent<ProfessionNativeSceneRenderer>();
        public Component Root => Renderer;
        public bool HasSeparator => Renderer.Separator != null;
        public IReadOnlyList<MeshRenderer> SeparatedRenderers => Renderer.Separator is DynamicSkeletonRenderSeparator separator
            ? separator.PartsRenderers.Select(value => value.MeshRenderer).ToArray() : Array.Empty<MeshRenderer>();
        public IReadOnlyList<ProfessionPortraitRenderPart> LoadedParts => PartHandler.LoadedSkeletonDict.Values
            .Select(value => new ProfessionPortraitRenderPart(value.Part.SkeletonTarget.transform, value.Part.GameObject)).ToArray();

        public void Configure(OfflineProfessionSceneAppearanceBindings bindings, AppearanceRendererPartHandler parts)
        {
            base.Configure(bindings, Guid<SkinTextureAsset>(bindings.Resources.SkinGuid),
                Guid<YooAssetTextureLoader>(bindings.Resources.LoaderGuid), parts);
        }
        private static AssetGuidProxy<T> Guid<T>(string value) where T : UnityEngine.Object
            => JsonUtility.FromJson<AssetGuidProxy<T>>("{\"mAssetGuid\":\"" + value + "\"}");

        public void PlayAnimation(string name, bool repeat)
        {
            if (Renderer.State == null) throw new InvalidOperationException("Refresh the original portrait appearance before selecting its pose.");
            animation = new ProfessionNativeAnimationChannels(Renderer.State.Source, name);
            loop = repeat;
        }

        public void PrepareForCharacterPortrait()
        {
            if (animation == null) throw new InvalidOperationException("Select the original portrait animation first.");
            // The original synchronous capture applies the selected animation
            // and Update(0f). Complete its frame-zero animation/IK/transform
            // channels without advancing an animation or physics clock.
            ProfessionNativeConstraintPose.EvaluateBeforePhysics(Renderer.State, animation, 0, loop);
            Renderer.LateUpdateMesh();
            var clip = GetComponent<AppearanceSkinClip>();
            if (clip) clip.UpdateClipMaterials();
            foreach (var value in PartHandler.LoadedSkeletonDict.Values)
            {
                if (value.Part.SkeletonFollower is BaseSkeletonFollower follower) follower.ForceUpdate();
                else throw new NotSupportedException("Bind the original portrait part follower.");
                var weapon = value.Part.SkeletonTarget.GetComponent<ProfessionNativeSceneWeaponRenderer>();
                if (weapon) weapon.UpdateAnimationAndMesh();
                else if (value.Part.HasAnim)
                    throw new NotSupportedException("Bind the original independent portrait part animation.");
            }
        }

        public override void Release(bool tryUnloadAllAssets)
        {
            animation = null;
            base.Release(tryUnloadAllAssets);
            // Original AppearanceCharacter.SetSkins first calls Release(false)
            // after Init. Retain the initialized skeleton/data, as the original
            // AppearanceRenderer does; the renderer owns its final destruction.
        }
    }
}
