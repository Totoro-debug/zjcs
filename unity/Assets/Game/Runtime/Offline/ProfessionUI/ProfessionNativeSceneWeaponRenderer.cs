using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent drawing endpoint for completed original primary-weapon poses.
    // Animation requests cross to the caller's pose producer; drawing never
    // resets time, approximates a transition or substitutes another animation.
    [RequireComponent(typeof(ProfessionNativeSceneMeshPart))]
    public sealed class ProfessionNativeSceneWeaponRenderer : MonoBehaviour,
        IProfessionAppearanceRendererLite, IProfessionFollowerRenderer, IProfessionPartAnimatedSkeleton
    {
        private ProfessionNativeSceneWeaponCatalog catalog;
        private ProfessionNativeSceneWeaponCatalog.Entry entry;
        private ProfessionNativeSceneRendererSettings settings;
        private ProfessionNativeMeshBatches batches;
        private Action<ProfessionNativeAppearanceSkeleton, string, bool> play;
        private bool fieldsApplied;
        public ProfessionNativeAppearanceSkeleton State { get; private set; }
        public Component Component => this;
        public string SkeletonDataName => "primaryWeapon";
        public IProfessionAppearanceSkeleton Skeleton => State;
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => State;
        public bool UseCustomZSpacing => false;
        public float ZSpacing => settings?.ZSpacing ?? 0;
        public int UpdateWhenInvisible => settings?.UpdateWhenInvisible ?? 0;
        public int UpdateMode => State != null && Main.MeshRenderer.enabled && Main.MeshRenderer.isVisible
            && gameObject.activeInHierarchy ? 3 : UpdateWhenInvisible;
        public int SeparatorSlotCount => 0;
        public string[] AnimationNames => State.Source.AnimationNames.ToArray();
        public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;
        private ProfessionNativeSceneMeshPart Main => GetComponent<ProfessionNativeSceneMeshPart>();

        public void Configure(ProfessionNativeSceneWeaponCatalog resources, string skin,
            Action<ProfessionNativeAppearanceSkeleton, string, bool> poseProducer)
        {
            if (State != null) throw new InvalidOperationException("Release the independent scene weapon before rebinding its sources.");
            catalog = resources ? resources : throw new ArgumentNullException(nameof(resources));
            entry = catalog.Find(skin);
            play = poseProducer ?? throw new ArgumentNullException(nameof(poseProducer));
        }

        public void Initialize(bool overwrite, bool quiet)
        {
            if (State != null && !overwrite) return;
            if (!catalog || entry == null || play == null) throw new InvalidOperationException("Bind original weapon sources and its pose producer first.");
            var nextSettings = ProfessionNativeSceneRendererSettings.LoadWeapon(entry.Hierarchy);
            if (nextSettings.InitialSkin != entry.InitialSkin || nextSettings.InitialAnimation != entry.InitialAnimation)
                throw new InvalidOperationException("Original independent weapon prefab and catalog disagree.");
            var next = new ProfessionNativeAppearanceSkeleton(catalog.Weapon.Model.Data);
            next.SetSkin(next.Data.FindSkin(nextSettings.InitialSkin)); next.SetToSetupPose(); next.UpdateCache();
            // AppearanceRendererLite owns the live material overrides. A
            // geometry rebuild must retain them until that owner releases or
            // replaces them; otherwise the next frame draws placeholder art.
            ClearGeometry();
            State = next; settings = nextSettings;
            batches = new ProfessionNativeMeshBatches(next.SlotCount);
            if (!fieldsApplied) { settings.Apply(Main.MeshRenderer); fieldsApplied = true; }
            Main.MeshRenderer.enabled = settings.Enabled;
            PlayAnimation(settings.InitialAnimation, settings.Loop);
            OnAnimationRebuild?.Invoke(this);
        }

        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            Initialize(false, false);
            if (!(skin is ProfessionNativeSkin native) || native.SlotCount != State.SlotCount
                || native.BoneCount != State.Source.BoneCount || native.ConstraintCount != State.Source.Constraints.Count)
                throw new ArgumentException("Original skin does not belong to this independent weapon.", nameof(skin));
            foreach (var attachment in native.Entries.Select(e => e.Attachment).OfType<ProfessionNativeRenderAttachment>())
            { CheckPage(attachment.Page); yield return entry.OriginalMaterial; }
        }

        private void CheckPage(string page)
        {
            if (!entry.OriginalMaterial || !entry.OriginalMaterial.mainTexture
                || entry.OriginalMaterial.mainTexture.name != page + "_placeholder")
                throw new NotSupportedException("Bind the actual original independent weapon atlas page: " + page);
        }

        public void PlayAnimation(string name, bool loop)
        {
            if (State == null) throw new InvalidOperationException("Initialize the independent weapon before requesting its animation.");
            if (!State.Source.AnimationNames.Contains(name)) throw new NotSupportedException("Original independent weapon animation is unavailable: " + name);
            play(State, name, loop);
        }

        public void UpdateAnimationAndMesh()
        {
            Initialize(false, false);
            batches.Build(State.Pose, State.CreateDrawSlots(), settings.UseClipping, settings.PmaVertexColors);
            Main.Assign(batches.Batches, batch =>
            {
                if (batch.Blend != 0) throw new NotSupportedException("Bind the original independent weapon slot blend material.");
                CheckPage(batch.Page);
                return CustomMaterialOverride.TryGetValue(entry.OriginalMaterial, out var replacement) ? replacement : entry.OriginalMaterial;
            });
            Main.MeshRenderer.enabled = settings.Enabled;
            OnCustomDemandLoading?.Invoke(this);
        }

        public void OnBecameVisible() => OnCustomDemandLoading?.Invoke(this);
        private void LateUpdate() { if (State != null) UpdateAnimationAndMesh(); }
        public void Release()
        {
            ClearGeometry();
            CustomMaterialOverride.Clear();
            if (Main) Main.MeshRenderer.enabled = false;
        }
        private void ClearGeometry()
        {
            batches?.Dispose(); batches = null; State = null; settings = null;
            var main = Main;
            // Keep an initialized renderer in its existing sorting group while
            // replacing geometry. Only an explicit release retires the draw.
            if (main) { main.Clear(); main.MeshRenderer.sharedMaterials = Array.Empty<Material>(); }
        }
        private void OnDestroy() => Release();
    }
}
