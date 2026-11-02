using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.World
{
    // Preserve the original instance, renderer and timing. The independent
    // character geometry backend also consumes original world skeletons.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MainlineGroundAnimation : MonoBehaviour
    {
        public MainlineGroundSkeletonData skeletonDataAsset;
        public string initialSkinName;
        public bool initialFlipX, initialFlipY, unscaledTime, loop, pmaVertexColors, useClipping;
        public bool tintBlack, addNormals, calculateTangents, clearStateOnDisable;
        public float timeScale = 1, zSpacing;
        public int updateWhenInvisible, updateTiming, maskInteraction;
        [SerializeField] private string _animationName;
        private ProfessionNativeAppearanceSkeleton skeleton;
        private ProfessionNativeAnimationChannels channels;
        private ProfessionNativeMeshBatches batches;
        private MainlineGroundPathAnimation paths;
        private MeshRenderer target;
        private Mesh mesh;
        private float elapsed;

        public float Elapsed => elapsed;
        public string AnimationName => _animationName;
        public int VertexCount => mesh ? mesh.vertexCount : 0;

        private void Awake()
        {
            if (!skeletonDataAsset || initialFlipX || initialFlipY || tintBlack || addNormals || calculateTangents ||
                clearStateOnDisable || zSpacing != 0 || maskInteraction != 0 || updateTiming != 1 ||
                (updateWhenInvisible != 0 && updateWhenInvisible != 3))
                throw new InvalidDataException("Restore additional original world skeleton instance settings.");
            var data = skeletonDataAsset.Load();
            skeleton = new ProfessionNativeAppearanceSkeleton(data);
            var skin = skeleton.Data.FindSkin(string.IsNullOrEmpty(initialSkinName) ? "default" : initialSkinName);
            if (skin == null) throw new InvalidDataException("Original world skeleton initial skin is missing.");
            skeleton.SetSkin(skin); skeleton.SetToSetupPose();
            if (!string.IsNullOrEmpty(_animationName))
                channels = new ProfessionNativeAnimationChannels(data, _animationName);
            paths = new MainlineGroundPathAnimation(skeleton, channels);
            batches = new ProfessionNativeMeshBatches(data.SlotCount);
            target = GetComponent<MeshRenderer>();
            mesh = new Mesh { name = name + " original world skeleton" };
            mesh.MarkDynamic(); GetComponent<MeshFilter>().sharedMesh = mesh;
            target.sharedMaterial = skeletonDataAsset.Material;
            Render();
        }

        private void Update()
        {
            if (!target || (updateWhenInvisible == 0 && !target.isVisible)) return;
            elapsed += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * timeScale;
            Render();
        }

        private void Render()
        {
            float time = channels == null ? 0 : loop && channels.Duration > 0
                ? Mathf.Repeat(elapsed, channels.Duration) : Mathf.Min(elapsed, channels.Duration);
            if (channels != null) skeleton.EvaluateNativeAnimation(channels, time, false);
            else skeleton.SetToSetupPose();
            paths.Apply(time);
            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), useClipping, pmaVertexColors);
            if (batches.Batches.Any(batch => batch.Blend != 0) || batches.Batches.Select(batch => batch.Page).Distinct().Count() > 1)
                throw new InvalidDataException("Bind additional original world skeleton blend materials/pages.");
            mesh.Clear();
            if (batches.Batches.Count > 0)
                mesh.CombineMeshes(batches.Batches.Select(batch => new CombineInstance { mesh = batch.Mesh,
                    transform = Matrix4x4.identity }).ToArray(), true, false);
        }

        private void OnDestroy()
        {
            batches?.Dispose();
            if (mesh) Destroy(mesh);
        }
    }
}
