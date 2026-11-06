using System;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Independent MeshRenderer endpoint for the serialized detail effect. The
    // source prefab selects an empty animation name and normal atlas blending.
    public sealed class PartnerNativeDetailMeshActor : MonoBehaviour
    {
        [SerializeField] private PartnerNativeUiSkeletonAsset skeletonDataAsset;
        [SerializeField] private string initialSkinName, _animationName;
        [SerializeField] private bool initialFlipX, initialFlipY, unscaledTime, loop;
        [SerializeField] private bool useClipping = true, pmaVertexColors = true;
        [SerializeField] private bool tintBlack, addNormals, calculateTangents;
        [SerializeField] private float timeScale = 1, zSpacing;
        [SerializeField] private int maskInteraction, updateTiming = 1;
        [SerializeField] private string[] separatorSlotNames = Array.Empty<string>();
        private ProfessionNativeAppearanceSkeleton skeleton;
        private ProfessionNativeMeshBatches batches;
        private PartnerNativeUiPhysicsPose physics;
        private ProfessionNativeAnimationChannels animation;
        private MeshFilter filter;
        private MeshRenderer rendererNative;
        private Material[] originalMaterials;
        private Mesh combined;
        private float elapsed;
        private void Awake()
        {
            if (!skeletonDataAsset) return; // Two original effect nodes have no data.
            if (initialFlipX || initialFlipY || tintBlack || addNormals || calculateTangents ||
                maskInteraction != 0 || updateTiming != 1 || separatorSlotNames.Length != 0)
                throw new NotSupportedException("Bind additional original detail mesh settings explicitly.");
            filter = GetComponent<MeshFilter>(); rendererNative = GetComponent<MeshRenderer>();
            if (!filter || !rendererNative) throw new InvalidOperationException("Restore the original detail effect mesh components.");
            originalMaterials = rendererNative.sharedMaterials;
            var data = skeletonDataAsset.Data;
            skeleton = new ProfessionNativeAppearanceSkeleton(data);
            var skin = data.SkinCatalog.FindSkin(string.IsNullOrEmpty(initialSkinName) ? "default" : initialSkinName);
            if (skin == null) throw new InvalidOperationException("Restore the original detail effect skin.");
            skeleton.SetSkin(skin); skeleton.SetToSetupPose(); skeleton.UpdateCache();
            batches = new ProfessionNativeMeshBatches(data.SlotCount);
            physics = new PartnerNativeUiPhysicsPose(data);
            if (!string.IsNullOrEmpty(_animationName))
                animation = new ProfessionNativeAnimationChannels(data, _animationName, originalBezierSegments: true);
            combined = new Mesh { name = "Original detail effect native mesh" };
            Draw(0);
        }
        private void LateUpdate()
        {
            if (skeleton != null) Draw((unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * timeScale);
        }
        private void Draw(float delta)
        {
            elapsed += delta;
            var layers = animation == null ? Array.Empty<ProfessionNativeOrderedConstraintPose.AnimationLayer>() :
                new[] { new ProfessionNativeOrderedConstraintPose.AnimationLayer(animation, elapsed, loop, 1) };
            if (animation == null) ProfessionNativeOrderedConstraintPose.EvaluateSetupPose(skeleton);
            else physics.Apply(skeleton, layers, delta);
            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), useClipping, pmaVertexColors, zSpacing);
            var materials = batches.Batches.Select(batch => {
                if (batch.Blend != 0) throw new NotSupportedException("Bind the original effect blend material before use.");
                var texture = skeletonDataAsset.TextureForPage(batch.Page);
                return originalMaterials.Single(material => material && material.mainTexture == texture);
            }).ToArray();
            combined.Clear();
            if (batches.Batches.Count > 0)
                combined.CombineMeshes(batches.Batches.Select(batch => new CombineInstance {
                    mesh = batch.Mesh, transform = Matrix4x4.identity }).ToArray(), false, false);
            filter.sharedMesh = combined; rendererNative.sharedMaterials = materials;
        }
        private void OnDestroy()
        {
            batches?.Dispose();
            if (combined) Destroy(combined);
        }
    }
}
