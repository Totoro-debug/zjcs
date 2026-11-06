using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent Canvas backend for original application components. Serialized
    // fields retain the original prefab values; no Spine runtime code is used.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class ProfessionNativeUiGraphic : MaskableGraphic,
        IProfessionAppearanceUIGraphic, IProfessionOffScreenGraphic, IProfessionFollowerGraphic, IProfessionAppearancePartGraphic,
        IProfessionAppearanceUILiteGraphic
    {
        [Serializable] public sealed class MeshSettings
        {
            public bool useClipping = true, useCustomSpacing, tintBlack, canvasGroupCompatible,
                pmaVertexColors, addNormals, calculateTangents, immutableTriangles;
            public float zSpacing;
        }
        [Serializable] public sealed class Generator { public MeshSettings settings = new MeshSettings(); }
        public ScriptableObject skeletonDataAsset;
        public Material additiveMaterial, multiplyMaterial, screenMaterial;
        [SerializeField] private Color m_SkeletonColor = Color.white;
        public string initialSkinName = "default", startingAnimation;
        public bool initialFlipX, initialFlipY, startingLoop = true, freeze, unscaledTime;
        public float timeScale = 1, referenceScale = 1, layoutScale = 1;
        public int layoutScaleMode, updateWhenInvisible = 3, updateTiming = 1;
        public Vector2 referenceSize = new Vector2(100, 100), pivotOffset;
        public bool allowMultipleCanvasRenderers, enableSeparatorSlots, updateSeparatorPartLocation = true,
            updateSeparatorPartScale, disableMeshAssignmentOnOverride = true, useCustomDemandLoading;
        public string[] separatorSlotNames = Array.Empty<string>();
        public List<Transform> separatorParts = new List<Transform>();
        public Vector2 physicsPositionInheritanceFactor;
        public float physicsRotationInheritanceFactor;
        public Transform physicsMovementRelativeTo;
        public Generator meshGenerator = new Generator();
        [SerializeField, FormerlySerializedAs("canvasRenderers")] private List<CanvasRenderer> renderers = new List<CanvasRenderer>();
        private readonly List<ProfessionNativeUiBatchGraphic> graphics = new List<ProfessionNativeUiBatchGraphic>();
        private IProfessionNativeUiPoseSource source;
        private ProfessionNativeMeshBatches batches;
        public ProfessionNativeAppearanceSkeleton State { get; private set; }
        public Component Instance => this;
        public float MeshScale => (canvas ? canvas.referencePixelsPerUnit : 100f) * referenceScale * layoutScale;
        public List<CanvasRenderer> canvasRenderers => renderers;
        public List<IProfessionSkinClipSlot> separatorSlots { get; } = new List<IProfessionSkinClipSlot>();
        public List<ProfessionAppearancePartInstruction> separatorPartInstructions { get; } = new List<ProfessionAppearancePartInstruction>();
        public List<Transform> SeparatorParts => separatorParts;
        IProfessionAppearancePartSkeleton IProfessionAppearancePartGraphic.Skeleton => State;
        bool IProfessionAppearancePartGraphic.enableSeparatorSlots { get => enableSeparatorSlots; set => enableSeparatorSlots = value; }
        public IProfessionAppearanceSkeleton Skeleton => State;
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => State;
        bool IProfessionAppearanceUIGraphic.allowMultipleCanvasRenderers { get => allowMultipleCanvasRenderers; set => allowMultipleCanvasRenderers = value; }
        ScriptableObject IProfessionAppearanceUIGraphic.skeletonDataAsset { get => skeletonDataAsset; set => skeletonDataAsset = value; }
        string IProfessionAppearanceUIGraphic.startingAnimation => startingAnimation;
        int IProfessionOffScreenGraphic.updateWhenInvisible { get => updateWhenInvisible; set => updateWhenInvisible = value; }
        public bool UseCustomZSpacing => meshGenerator.settings.useCustomSpacing;
        public float ZSpacing => meshGenerator.settings.zSpacing;
        public int UpdateWhenInvisible => updateWhenInvisible;
        public int UpdateMode => canvasRenderer.cull ? updateWhenInvisible : 3;
        public IProfessionAppearanceAnimationState AnimationState => State != null ? source?.AnimationState : null;
        public Material[] MaterialsMultipleCanvasRenderers { get; private set; } = Array.Empty<Material>();
        public Texture[] TexturesMultipleCanvasRenderers { get; private set; } = Array.Empty<Texture>();
        private Texture[] placeholderTextures = Array.Empty<Texture>();
        Texture[] IProfessionAppearanceUILiteGraphic.TexturesMultipleCanvasRenderers => placeholderTextures;
        public Texture OverrideTexture { get; set; }
        public Dictionary<Texture, Texture> CustomTextureOverride { get; } = new Dictionary<Texture, Texture>();
        public override Texture mainTexture => OverrideTexture ? OverrideTexture :
            TexturesMultipleCanvasRenderers.FirstOrDefault() ?? base.mainTexture;
        string IProfessionAppearanceUILiteGraphic.SkeletonDataName => skeletonDataAsset ?
            skeletonDataAsset.name.Replace("_SkeletonData", "") : throw new InvalidOperationException("Original UI skeleton is unbound.");
        bool IProfessionAppearanceUILiteGraphic.allowMultipleCanvasRenderers => allowMultipleCanvasRenderers;
        void IProfessionAppearanceUILiteGraphic.UpdateMesh() => ApplyAnimation();
        public event Action<IProfessionAppearanceUIGraphic> OnCustomDemandLoading;
        public event Action<int, Mesh[], Material[], Texture[]> AssignMeshOverrideMultipleRenderers;
        public event Action<IProfessionSkinClipGraphic> OnMeshAndMaterialsUpdated;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;

        public void Configure(IProfessionNativeUiPoseSource value)
        {
            if (State != null) throw new InvalidOperationException("Release the original UI skeleton before rebinding it.");
            source = value ?? throw new ArgumentNullException(nameof(value));
        }
        public void BindOriginalCanvasRenderers(Transform originalRoot)
        {
            if (State != null || graphics.Count != 0)
                throw new InvalidOperationException("Bind the serialized original Canvas renderers before initialization.");
            if (!originalRoot || !transform.IsChildOf(originalRoot))
                throw new ArgumentException("Bind the exact original actor prefab root.");
            foreach (var renderer in renderers)
            {
                var item = renderer ? renderer.GetComponent<ProfessionNativeUiBatchGraphic>() : null;
                if (!item || !item.transform.IsChildOf(originalRoot) || graphics.Contains(item))
                    throw new InvalidOperationException("Restore each original submesh graphic and its CanvasRenderer binding.");
                graphics.Add(item);
            }
        }
        public void Initialize(bool overwrite)
        {
            if (State != null && !overwrite) return;
            // Original avatar prototypes intentionally have no data until their
            // PlayerInfoUI selects a player. NPC cards use the original sprite
            // branch; a null asset must remain an empty graphic in that branch.
            if (!skeletonDataAsset) { Release(); return; }
            if (source == null || source.Asset != skeletonDataAsset)
                throw new InvalidOperationException("Bind this exact original UI skeleton's pose source before initialization.");
            ValidateSettings();
            Release(); State = source.CreateSkeleton(initialSkinName);
            if (State == null) throw new InvalidOperationException("Original UI pose source returned no skeleton.");
            batches = new ProfessionNativeMeshBatches(State.SlotCount);
            if (!string.IsNullOrEmpty(startingAnimation)) source.Play(State, startingAnimation, startingLoop);
            source.Apply(State, 0); Build(); OnAnimationRebuild?.Invoke(this);
        }
        private void ValidateSettings()
        {
            var settings = meshGenerator.settings;
            if (initialFlipX || initialFlipY || layoutScaleMode != 0 ||
                settings.tintBlack || (settings.canvasGroupCompatible && settings.pmaVertexColors) || settings.addNormals || settings.calculateTangents ||
                ((physicsPositionInheritanceFactor != Vector2.zero || physicsRotationInheritanceFactor != 0)
                    && source is not IProfessionNativeUiPhysicsMovement) ||
                updateTiming != 1)
                throw new NotSupportedException("Restore additional original UI graphic flip/layout/separator/vertex/physics settings before using this prefab.");
        }
        public void PlayAnimation(string name, bool loop)
        {
            Initialize(false);
            if (State == null) throw new InvalidOperationException("Original UI skeleton is not selected.");
            source.Play(State, name, loop);
        }
        public void ApplyAnimation()
        {
            if (State == null) return;
            source.Apply(State, 0); Build();
        }
        private void LateUpdate()
        {
            if (State == null || freeze || UpdateMode < 1) return;
            (source as IProfessionNativeUiPhysicsMovement)?.UpdatePhysicsMovement();
            source.Apply(State, (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * timeScale);
            if (UpdateMode >= 2) Build();
        }
        private void Build()
        {
            // Original single-Canvas mode uses its assigned UI material for all
            // slots. Separate blend materials belong to the multiple mode.
            batches.Build(State.Pose, OriginalSeparatedDrawSlots(), meshGenerator.settings.useClipping,
                meshGenerator.settings.pmaVertexColors,
                zSpacing: meshGenerator.settings.useCustomSpacing ? meshGenerator.settings.zSpacing : 0,
                allowConcaveClipping: true, singleMaterial: !allowMultipleCanvasRenderers, mergePmaAdditive: true);
            int count = batches.Batches.Count;
            if (!allowMultipleCanvasRenderers && count > 1)
                throw new NotSupportedException("Original UI graphic requires multiple CanvasRenderers for these atlas/blend batches.");
            var materials = new Material[count]; var textures = new Texture[count]; var meshes = new Mesh[count];
            var placeholders = new Texture[count];
            for (int i = 0; i < count; i++)
            {
                var batch = batches.Batches[i]; meshes[i] = batch.Mesh;
                materials[i] = source.MaterialForPage(batch.Page, batch.Blend);
                var originalTexture = source.TextureForPage(batch.Page);
                placeholders[i] = originalTexture;
                textures[i] = !allowMultipleCanvasRenderers && OverrideTexture ? OverrideTexture :
                    originalTexture && CustomTextureOverride.TryGetValue(originalTexture, out var loaded) ? loaded : originalTexture;
                if (!materials[i] || !textures[i]) throw new InvalidOperationException("Original UI atlas page/material is unbound: " + batch.Page);
            }
            MaterialsMultipleCanvasRenderers = materials; TexturesMultipleCanvasRenderers = textures;
            placeholderTextures = placeholders;
            if (useCustomDemandLoading) OnCustomDemandLoading?.Invoke(this);
            SyncSubmeshGraphicsWithCanvasRenderers();
            BindOriginalSeparatorParents();
            for (int i = 0; i < count; i++)
            {
                var sourceToBatch = graphics[i].transform.worldToLocalMatrix * transform.localToWorldMatrix;
                graphics[i].Assign(meshes[i], materials[i], textures[i], MeshScale, pivotOffset, color * m_SkeletonColor, sourceToBatch);
                graphics[i].Rebuild(CanvasUpdate.PreRender);
            }
            AssignMeshOverrideMultipleRenderers?.Invoke(count, meshes, materials, textures);
            OnMeshAndMaterialsUpdated?.Invoke(this);
        }
        public void SyncSubmeshGraphicsWithCanvasRenderers()
        {
            int count = batches?.Batches.Count ?? 0;
            while (graphics.Count < count)
            {
                var item = new GameObject("OriginalNativeCanvasBatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfessionNativeUiBatchGraphic));
                var rect = (RectTransform)item.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.pivot = rectTransform.pivot;
                var graphic = item.GetComponent<ProfessionNativeUiBatchGraphic>(); graphics.Add(graphic); renderers.Add(graphic.canvasRenderer);
            }
            for (int i = 0; i < graphics.Count; i++)
            {
                graphics[i].gameObject.SetActive(i < count);
                if (i >= count) graphics[i].Clear();
            }
        }
        public void PrepareInstructionsAndRenderers(bool isOnValidate)
        {
            UpdateOriginalSeparatorInstructions();
            SyncSubmeshGraphicsWithCanvasRenderers();
        }
        public void MatchRectTransformWithBounds()
        {
            if (batches == null || batches.Batches.Count == 0) return;
            var bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches.Skip(1)) bounds.Encapsulate(batch.Mesh.bounds);
            rectTransform.sizeDelta = (Vector2)bounds.size * MeshScale;
            // Preserve the origin of the source skeleton while adjusting its
            // hit/layout rectangle; changing its bounds must not shift geometry.
            var size = bounds.size;
            if (size.x != 0 && size.y != 0) rectTransform.pivot = new Vector2(-bounds.min.x / size.x, -bounds.min.y / size.y);
            foreach (var graphic in graphics) graphic.rectTransform.pivot = rectTransform.pivot;
        }
        protected override void OnPopulateMesh(VertexHelper helper) => helper.Clear();
        protected override void Awake() { base.Awake(); Initialize(false); }
        protected override void OnEnable()
        {
            base.OnEnable();
            if (State != null) Build();
        }
        protected override void OnDisable()
        {
            foreach (var graphic in graphics) if (graphic) graphic.Clear();
            base.OnDisable();
        }
        public void Release()
        {
            foreach (var graphic in graphics) if (graphic) graphic.Clear();
            batches?.Dispose(); batches = null; State = null;
            separatorPartInstructions.Clear();
            MaterialsMultipleCanvasRenderers = Array.Empty<Material>(); TexturesMultipleCanvasRenderers = Array.Empty<Texture>();
            placeholderTextures = Array.Empty<Texture>();
        }
        protected override void OnDestroy() { Release(); base.OnDestroy(); }
    }
}
