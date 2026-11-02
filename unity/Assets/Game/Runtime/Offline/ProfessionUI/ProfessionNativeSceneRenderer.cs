using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent Unity drawing endpoint. An explicitly bound pose producer owns
    // animation, mixing and constraints; no sampled-pose fallback is installed.
    [RequireComponent(typeof(ProfessionNativeSceneMeshPart))]
    public sealed partial class ProfessionNativeSceneRenderer : MonoBehaviour,
        IProfessionAppearanceSceneRenderer, IProfessionAppearancePartRenderer,
        IProfessionSceneSeparatorRenderer, IProfessionAppearanceMaterialReplacement,
        IProfessionFollowerRenderer
    {
        private ProfessionNativeSceneAppearanceResources resources;
        private ProfessionNativeSceneCharacterAsset initializedAsset;
        private ProfessionNativeMeshBatches batches;
        private ProfessionNativeSceneMeshSeparator backend;
        private DynamicSkeletonRenderSeparator separator;
        private ProfessionNativeSceneRendererSettings settings;
        private TextAsset appliedHierarchy;
        private TextAsset portraitHierarchy;
        private readonly Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
        public ScriptableObject skeletonDataAsset { get; set; }
        public ProfessionNativeAppearanceSkeleton State { get; private set; }
        public Component Component => this;
        public GameObject GameObject => gameObject;
        public IProfessionAppearanceSkeleton Skeleton => State;
        IProfessionSkinClipSkeleton IProfessionAppearancePartRenderer.Skeleton => State;
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => State;
        // The verified current prefab has zero spacing. The original follower
        // returns before consulting either custom depth or draw-order depth.
        public bool UseCustomZSpacing => false;
        public float ZSpacing => settings?.ZSpacing ?? 0;
        public int UpdateWhenInvisible => settings?.UpdateWhenInvisible ?? 0;
        public int UpdateMode => State != null && HasVisibleGeometry() ? 3 : UpdateWhenInvisible;
        public string SkeletonDataName => initializedAsset ? "character_" + (State.Source.Sex == "male" ? "Male" : "Female") + "_1" : null;
        public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
        public List<IProfessionSkinClipSlot> SeparatorSlots { get; } = new List<IProfessionSkinClipSlot>();
        public int SeparatorSlotCount => SeparatorSlots.Count;
        public IProfessionAppearancePartSeparator Separator => separator;
        public event Action<Material[]> OnGetUpdatedSharedMaterials;
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;
        private ProfessionNativeSceneMeshPart Main => GetComponent<ProfessionNativeSceneMeshPart>();

        private bool HasVisibleGeometry()
        {
            // The whole renderer is disabled when the original separator owns
            // the draw. Testing it alone would freeze every attached part.
            if (backend != null)
                return backend.PartsRenderers.Any(part => part.GameObject.activeInHierarchy
                    && part.MeshRenderer.enabled && part.MeshRenderer.isVisible);
            var main = Main.MeshRenderer;
            return main.enabled && main.isVisible && gameObject.activeInHierarchy;
        }

        public void Configure(ProfessionNativeSceneAppearanceResources catalog)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            if (State != null && resources != catalog) throw new InvalidOperationException("Release the native scene renderer before changing its source catalog.");
            resources = catalog;
        }

        public void ConfigurePortrait(ProfessionNativeSceneAppearanceResources catalog, TextAsset hierarchy)
        {
            if (!hierarchy) throw new ArgumentNullException(nameof(hierarchy));
            if (State != null) throw new InvalidOperationException("Configure the original studio before initializing its actor.");
            Configure(catalog);
            _ = ProfessionNativeSceneRendererSettings.LoadPortrait(hierarchy);
            portraitHierarchy = hierarchy;
        }

        public void Initialize(bool overwrite, bool quiet)
        {
            if (!overwrite && State != null && initializedAsset == skeletonDataAsset) return;
            if (!resources) throw new InvalidOperationException("Bind the current original scene resources before initialization.");
            var asset = skeletonDataAsset as ProfessionNativeSceneCharacterAsset;
            if (!asset) throw new NotSupportedException("The native scene renderer requires a verified current scene character asset.");
            resources.FindModel(asset);
            var hierarchy = portraitHierarchy ? portraitHierarchy : asset.Hierarchy;
            if (appliedHierarchy && appliedHierarchy != hierarchy)
                throw new NotSupportedException("Rebuild the original scene hierarchy before switching to a different renderer prefab.");
            var nextSettings = portraitHierarchy ? ProfessionNativeSceneRendererSettings.LoadPortrait(hierarchy)
                : ProfessionNativeSceneRendererSettings.Load(hierarchy);
            var next = new ProfessionNativeAppearanceSkeleton(asset.Data);
            var initial = next.Data.FindSkin(nextSettings.InitialSkin);
            if (initial == null) throw new InvalidOperationException("Original scene initial skin is unavailable.");
            next.SetSkin(initial); next.SetToSetupPose(); next.UpdateCache();
            bool keepSeparator = separator && backend != null;
            var reboundSlots = keepSeparator ? SeparatorSlots.Select(slot =>
            {
                var order = State.DrawOrder;
                int draw = Array.FindIndex(order, candidate => ReferenceEquals(candidate, slot));
                if (draw < 0) throw new ArgumentException("Original separator slot does not belong to the previous scene skeleton.");
                string name = State.SlotPose.Name(State.SlotPose.DrawSlot(draw));
                return next.FindSlot(name) ?? throw new NotSupportedException("Bind the original separator slot in the replacement scene skeleton: " + name);
            }).ToArray() : Array.Empty<IProfessionSkinClipSlot>();
            ReleaseSkeleton(keepSeparator);
            initializedAsset = asset; settings = nextSettings; State = next;
            SeparatorSlots.AddRange(reboundSlots);
            batches = new ProfessionNativeMeshBatches(next.SlotCount);
            if (!appliedHierarchy) { settings.Apply(Main.MeshRenderer); appliedHierarchy = hierarchy; }
            Main.MeshRenderer.enabled = settings.Enabled;
            OnAnimationRebuild?.Invoke(this);
            ResumeSceneParts();
        }

        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            Initialize(false, false);
            if (!(skin is ProfessionNativeSkin native) || native.SlotCount != State.SlotCount
                || native.BoneCount != State.Source.BoneCount || native.ConstraintCount != State.Source.Constraints.Count)
                throw new ArgumentException("Original skin does not match the initialized scene model.", nameof(skin));
            foreach (var entry in native.Entries)
                if (entry.Attachment is ProfessionNativeRenderAttachment render)
                    yield return resources.MaterialForPage(initializedAsset, render.Page);
        }

        public void UpdateAnimationAndMesh() { UpdateBoundAnimatorPose(); LateUpdateMesh(); }
        private void LateUpdate() { if (State != null) { UpdateBoundAnimatorPose(); LateUpdateMesh(); } }

        public void LateUpdateMesh()
        {
            Initialize(false, false);
            var order = State.DrawOrder;
            var cuts = new SortedSet<int>();
            var slotIndices = new List<int>();
            foreach (var slot in SeparatorSlots)
            {
                int draw = Array.FindIndex(order, candidate => ReferenceEquals(candidate, slot));
                if (draw < 0 || !cuts.Add(draw)) throw new ArgumentException("Original separator slots must be unique identities from the current scene skeleton.");
                slotIndices.Add(State.SlotPose.DrawSlot(draw));
            }
            if (cuts.Count > 0 && backend == null) throw new InvalidOperationException("Initialize original separator parts before drawing split geometry.");
            if (sceneParts.Count > 0 && backend == null) throw new InvalidOperationException("Bind original separator slots before drawing independent scene parts.");
            batches.Build(State.Pose, State.CreateDrawSlots(slotIndices), settings.UseClipping, settings.PmaVertexColors, settings.ZSpacing);
            if (backend != null)
            {
                backend.Render(batches.Batches, cuts.ToArray(), MaterialFor);
                ApplySlotDrawOrder(cuts.ToArray());
                Main.MeshRenderer.enabled = !settings.DisableRenderingOnOverride && settings.Enabled;
            }
            else
            {
                Main.Assign(batches.Batches, MaterialFor, materials => OnGetUpdatedSharedMaterials?.Invoke(materials));
                Main.MeshRenderer.enabled = settings.Enabled;
            }
            OnCustomDemandLoading?.Invoke(this);
        }

        private void ApplySlotDrawOrder(int[] cuts)
        {
            // Keep each fragment contiguous in Unity's transparent renderer list.
            // Equal renderer orders otherwise permit material/submesh interleaving.
            // The original renderer fields stay intact; odd keys are the insertion
            // positions immediately after original slots for native part adapters.
            var first = backend.PartsRenderers[0].MeshRenderer;
            for (int i = 0; i < backend.PartsRenderers.Count; i++)
            {
                var part = backend.PartsRenderers[i];
                var renderer = part.MeshRenderer;
                if (renderer.sortingLayerID != first.sortingLayerID || renderer.sortingOrder != first.sortingOrder)
                    throw new NotSupportedException("Bind divergent original part sorting before drawing this native scene character.");
                var group = part.GameObject.GetComponent<SortingGroup>();
                if (!group) group = part.GameObject.AddComponent<SortingGroup>();
                group.enabled = i <= cuts.Length;
                group.sortingLayerID = renderer.sortingLayerID;
                int order = checked(first.sortingOrder + 2 * (i == 0 ? 0 : i <= cuts.Length ? cuts[i - 1] + 1 : State.SlotCount));
                if (order < short.MinValue || order > short.MaxValue)
                    throw new NotSupportedException("Original native slot ordering exceeds Unity's local sorting range.");
                group.sortingOrder = order;
            }
            UpdateScenePartOrder(State.DrawOrder, cuts, first);
            SortingGroup.UpdateAllSortingGroups();
        }

        private Material MaterialFor(ProfessionNativeMeshBatches.Batch batch)
        {
            if (batch.Blend != 0) throw new NotSupportedException("Bind the original non-normal slot blend material before rendering this scene attachment.");
            Material original = resources.MaterialForPage(initializedAsset, batch.Page);
            Material result = CustomMaterialOverride.TryGetValue(original, out var value) ? value : original;
            if (!result) throw new InvalidOperationException("Original scene material replacement is unbound.");
            return result;
        }

        public IProfessionAppearancePartSeparator AddSeparator(int sortingLayer, int extraParts, int sortingOrderIncrement, int sortingOrder)
        {
            Initialize(false, false);
            if (separator) throw new InvalidOperationException("Reinitialize the existing original scene separator instead of adding another.");
            return separator = DynamicSkeletonRenderSeparator.AddDynamicToSkeletonRenderer(this, sortingLayer, extraParts, sortingOrderIncrement, sortingOrder);
        }

        public IProfessionSceneSeparatorBackend CreateSeparatorBackend()
        {
            Initialize(false, false);
            if (backend != null) throw new InvalidOperationException("The native scene renderer already owns a separator backend.");
            backend = new ProfessionNativeSceneMeshSeparator(() => Main.MeshRenderer.enabled = !settings.DisableRenderingOnOverride && settings.Enabled);
            return backend;
        }

        public void OnBecameVisible() => OnCustomDemandLoading?.Invoke(this);
        public void OnBecameInvisible() { } // Pose scheduling belongs to its producer.

        public void AddRuntimeReplacementMaterial(Material originalMaterial, Material runtimeMaterial)
        {
            if (!originalMaterial || !runtimeMaterial) throw new ArgumentNullException();
            if (replacements.TryGetValue(originalMaterial, out var previous)) ReplaceExisting(previous, runtimeMaterial);
            replacements[originalMaterial] = runtimeMaterial;
            ReplaceExisting(originalMaterial, runtimeMaterial);
        }
        public void ResetRuntimeReplacementMaterials()
        {
            foreach (var pair in replacements) ReplaceExisting(pair.Value, pair.Key);
            replacements.Clear();
        }
        private void ReplaceExisting(Material source, Material target)
        {
            ReplaceExisting(Main.MeshRenderer, source, target);
            if (backend != null) foreach (var part in backend.PartsRenderers) ReplaceExisting(part.MeshRenderer, source, target);
        }
        private static void ReplaceExisting(MeshRenderer renderer, Material source, Material target)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) if (materials[i] == source) materials[i] = target;
            renderer.sharedMaterials = materials;
        }

        public void Release() => ReleaseSkeleton(false);

        private void ReleaseSkeleton(bool keepSeparator)
        {
            SuspendSceneParts();
            if (keepSeparator)
            {
                // The original body draws before its part handler refreshes.
                // Retain separator subscriptions/pool across a skeleton rebuild,
                // but never leave old geometry or material identities attached.
                backend.ClearSkeletonGeometry();
            }
            else
            {
                backend?.Dispose(); backend = null;
                if (separator) { if (Application.isPlaying) Destroy(separator); else DestroyImmediate(separator); }
                separator = null;
            }
            SeparatorSlots.Clear();
            batches?.Dispose(); batches = null;
            CustomMaterialOverride.Clear(); replacements.Clear();
            var main = Main;
            if (main)
            {
                main.Clear(); main.MeshRenderer.sharedMaterials = Array.Empty<Material>();
                main.MeshRenderer.enabled = false;
            }
            State = null; initializedAsset = null; settings = null;
            ClearAnimatorNotification();
        }
        private void OnDestroy() { Release(); DisposeSceneParts(); }
    }
}
