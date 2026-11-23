using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.NativeSpine
{
    // SDK boundary for the original application controllers. Spine owns all
    // animation, constraints, skin attachments, geometry and material overrides.
    [DisallowMultipleComponent]
    public sealed class NativeSpineAppearanceAdapter : MonoBehaviour,
        IProfessionAppearanceRendererLite, IProfessionFollowerRenderer
    {
        [SerializeField] SkeletonRenderer source;
        [SerializeField] string skeletonDataName;
        SkeletonRenderer attached;
        ISkeletonAnimation animationSource;
        SkeletonView view;
        public SkeletonRenderer Source => source;
        public Component Component => source;
        public string SkeletonDataName => !string.IsNullOrEmpty(skeletonDataName) ? skeletonDataName :
            throw new InvalidOperationException("Bind the original appearance configuration's skeleton identity before activation.");
        public Dictionary<Material, Material> CustomMaterialOverride => source.CustomMaterialOverride;
        public int SeparatorSlotCount => source.separatorSlots.Count;
        public bool UseCustomZSpacing => false;
        public int UpdateWhenInvisible => (int)source.updateWhenInvisible;
        public int UpdateMode => (int)source.UpdateMode;
        public float ZSpacing => source.zSpacing;
        public IProfessionAppearanceSkeleton Skeleton { get { EnsureView(); return view; } }
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton { get { EnsureView(); return view; } }
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;

        public void Bind(SkeletonRenderer renderer, string originalSkeletonName)
        {
            if (!renderer) throw new ArgumentNullException(nameof(renderer));
            if (string.IsNullOrEmpty(originalSkeletonName)) throw new ArgumentException(nameof(originalSkeletonName));
            Detach(); source = renderer; skeletonDataName = originalSkeletonName; Attach(); EnsureView();
        }
        void Awake() => Attach();
        void OnDestroy() => Detach();
        void Attach()
        {
            if (attached == source && attached) return;
            Detach();
            if (!source) throw new InvalidOperationException("Bind the original Spine renderer before activating its application adapter.");
            attached = source;
            animationSource = source as ISkeletonAnimation;
            attached.OnRebuild += Rebuild;
            attached.OnMeshAndMaterialsUpdated += Demand;
            if (animationSource != null) animationSource.OnAnimationRebuild += AnimationRebuild;
        }
        void Detach()
        {
            if (attached)
            {
                attached.OnRebuild -= Rebuild;
                attached.OnMeshAndMaterialsUpdated -= Demand;
                if (animationSource != null) animationSource.OnAnimationRebuild -= AnimationRebuild;
            }
            attached = null; animationSource = null; view = null;
        }
        void EnsureView()
        {
            Attach();
            var skeleton = source.Skeleton;
            if (skeleton == null) throw new InvalidOperationException("The original Spine skeleton did not initialize.");
            if (view == null || !ReferenceEquals(view.Value, skeleton)) view = new SkeletonView(skeleton);
        }
        void Rebuild(SkeletonRenderer renderer)
        {
            EnsureView();
            if (animationSource == null) OnAnimationRebuild?.Invoke(this);
        }
        void AnimationRebuild(ISkeletonAnimation renderer) { EnsureView(); OnAnimationRebuild?.Invoke(this); }
        void Demand(SkeletonRenderer renderer) => OnCustomDemandLoading?.Invoke(this);
        public void Initialize(bool overwrite, bool quiet) { Attach(); source.Initialize(overwrite, quiet); EnsureView(); }
        public void UpdateAnimationAndMesh()
        {
            if (source.TryGetComponent<SkeletonMecanim>(out var mecanim)) mecanim.ApplyAnimation();
            else if (source.TryGetComponent<SkeletonAnimation>(out var skeletonAnimation)) skeletonAnimation.ApplyAnimation();
            source.LateUpdateMesh();
        }
        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            if (!(skin is SkinView native)) throw new ArgumentException("The skin must belong to the bound Spine SDK.", nameof(skin));
            foreach (var entry in native.Value.Attachments)
            {
                if (entry.Attachment is IHasTextureRegion attachment && attachment.Region is AtlasRegion region)
                {
                    if (!(region.page.rendererObject is Material material))
                        throw new InvalidOperationException("The original Spine atlas page material is unbound.");
                    yield return material;
                }
            }
        }

        sealed class SkeletonView : IProfessionAppearanceSkeleton, IProfessionFollowerSkeleton
        {
            public readonly Spine.Skeleton Value;
            readonly DataView data;
            readonly Dictionary<Spine.Skin, SkinView> skins = new Dictionary<Spine.Skin, SkinView>();
            readonly Dictionary<Spine.Bone, BoneView> bones = new Dictionary<Spine.Bone, BoneView>();
            readonly Dictionary<Spine.Slot, SlotView> slots = new Dictionary<Spine.Slot, SlotView>();
            public SkeletonView(Spine.Skeleton value) { Value = value; data = new DataView(this); }
            public IProfessionAppearanceSkeletonData Data => data;
            public IProfessionAppearanceSkin Skin => Wrap(Value.Skin);
            public int SlotCount => Value.Slots.Count;
            public string SetupAttachmentName(int slot) => Value.Slots.Items[slot].Data.AttachmentName;
            public void SetSlotAttachment(int slot, object attachment) => Value.Slots.Items[slot].Attachment = (Attachment)attachment;
            public void SetSkin(IProfessionAppearanceSkin skin) => Value.SetSkin(skin == null ? null : ((SkinView)skin).Value);
            public void SetToSetupPose() => Value.SetToSetupPose();
            public void UpdateCache() => Value.UpdateCache();
            public object GetAttachment(int slotIndex, string name) => Value.GetAttachment(slotIndex, name);
            public IProfessionFollowerBone FindBone(string name) => Wrap(Value.FindBone(name));
            public IProfessionFollowerSlot FindSlot(string name) => Wrap(Value.FindSlot(name));
            public IProfessionFollowerSlot[] DrawOrder
            {
                get
                {
                    var result = new IProfessionFollowerSlot[Value.DrawOrder.Count];
                    for (int i = 0; i < result.Length; i++) result[i] = Wrap(Value.DrawOrder.Items[i]);
                    return result;
                }
            }
            SkinView Wrap(Spine.Skin value)
            {
                if (value == null) return null;
                if (!skins.TryGetValue(value, out var result)) skins.Add(value, result = new SkinView(value));
                return result;
            }
            BoneView Wrap(Spine.Bone value)
            {
                if (value == null) return null;
                if (!bones.TryGetValue(value, out var result)) bones.Add(value, result = new BoneView(value));
                return result;
            }
            SlotView Wrap(Spine.Slot value)
            {
                if (value == null) return null;
                if (!slots.TryGetValue(value, out var result)) slots.Add(value, result = new SlotView(value, Wrap(value.Bone)));
                return result;
            }
            sealed class DataView : IProfessionAppearanceSkeletonData
            {
                readonly SkeletonView owner;
                public DataView(SkeletonView owner) => this.owner = owner;
                public IProfessionAppearanceSkin FindSkin(string name) => owner.Wrap(owner.Value.Data.FindSkin(name));
            }
        }
        sealed class SkinView : IProfessionAppearanceSkin
        {
            public readonly Spine.Skin Value;
            public SkinView(Spine.Skin value) => Value = value;
            public void Clear() => Value.Clear();
            public void AddSkin(IProfessionAppearanceSkin skin) => Value.AddSkin(((SkinView)skin).Value);
            public object GetAttachment(int slot, string name) => Value.GetAttachment(slot, name);
        }
        sealed class BoneView : IProfessionFollowerBone
        {
            readonly Spine.Bone value;
            public BoneView(Spine.Bone value) => this.value = value;
            public bool Active => value.Active;
            public float WorldX => value.WorldX;
            public float WorldY => value.WorldY;
            public float WorldRotationX => value.WorldRotationX;
            public float ScaleX => value.ScaleX;
            public float ScaleY => value.ScaleY;
            public float A => value.A;
            public float B => value.B;
            public float C => value.C;
            public float D => value.D;
        }
        sealed class SlotView : IProfessionFollowerSlotAlpha
        {
            readonly Spine.Slot value;
            public SlotView(Spine.Slot value, BoneView bone) { this.value = value; Bone = bone; }
            public IProfessionFollowerBone Bone { get; }
            public int Index => value.Data.Index;
            public float zOrder => throw new NotSupportedException("Bind original custom slot depths before enabling custom Z spacing.");
            public object Attachment => value.Attachment;
            public float Alpha => value.A;
        }
    }
}
