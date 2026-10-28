using System;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Independent geometry adapter on the original prop GameObject. The world
    // owner supplies the shared, verified Windows native animation library.
    public sealed class NativeMap13PropMecanim : MonoBehaviour
    {
        public string SourceKey, ControllerKey;
        public NativeMap13SkeletonData Data;
        public NativeMap13Renderer Profile;
        public Animator Animator;
        public NativeMap13PartsBinding Parts;
        public int UpdateTiming, UpdateWhenInvisible;
        public bool ClearStateOnDisable;
        private NativeMap13SkeletonView view;
        private NativeMap13MecanimPose pose;
        private MaterialPropertyBlock propertyBlock;
        public NativeMap13SkeletonView View => view;
        public NativeMap13ApplicationPose Skeleton { get; private set; }
        public event Action<NativeMap13PropMecanim> OnRebuild;
        public bool valid => view != null;
        public bool IsValid => valid;
        public int updateWhenInvisible => UpdateWhenInvisible;
        public int UpdateMode => UpdateWhenInvisible == 0 && !Parts.Renderers.Any(r => r && r.isVisible) ? 0 : 3;
        public float zSpacing => Profile.zSpacing;
        // The owning world must inject the shared verified animation library.
        // The application's lazy initialization probe cannot create that owner.
        public void Initialize(bool overwrite = false, bool quiet = false) { }

        public void Initialize(NativeMap13AnimatorLibrary library)
        {
            if (view != null) throw new InvalidOperationException("Original prop is already initialized.");
            if (!Data || !Animator || !Parts || UpdateTiming != 1 ||
                (UpdateWhenInvisible != 0 && UpdateWhenInvisible != 3) || ClearStateOnDisable)
                throw new InvalidOperationException($"Original prop lifecycle binding differs: data={Data}, animator={Animator}, parts={Parts}, timing={UpdateTiming}, invisible={UpdateWhenInvisible}, clear={ClearStateOnDisable}.");
            Animator.runtimeAnimatorController = library.Controller(ControllerKey);
            Animator.Rebind(); Animator.Update(0);
            view = new NativeMap13SkeletonView(Data, Profile, transform, "Native geometry payload");
            view.BindMeshTargets(Parts.Renderers, Parts.SeparatorSlots, Parts.SourceRenderer);
            Parts.SeparationChanged += OnSeparationChanged;
            view.SetSeparationEnabled(Parts.isActiveAndEnabled);
            pose = new NativeMap13MecanimPose(Animator, view, Data, Profile);
            pose.Sample(); CopyPropertyBlock();
            Skeleton = new NativeMap13ApplicationPose(Data.Source, view);
            OnRebuild?.Invoke(this);
        }

        // The pinned APK enum metadata defines InUpdate=1, invisible Nothing=0,
        // and FullUpdate=3. Each original renderer retains its own mode.
        // Sampling remains before the engine's Animator evaluation, as configured
        // in the original prefab; this deliberately preserves that frame order.
        private void Update()
        {
            if (pose == null || !Animator.enabled ||
                (UpdateWhenInvisible == 0 && !(Parts.isActiveAndEnabled ? Parts.Renderers.Any(r => r && r.isVisible) : Parts.SourceRenderer.isVisible))) return;
            pose.Sample(); CopyPropertyBlock();
        }
        private void CopyPropertyBlock()
        {
            if (!Parts.CopyPropertyBlock || !Parts.SourceRenderer) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Parts.SourceRenderer.GetPropertyBlock(propertyBlock);
            foreach (var renderer in Parts.Renderers) renderer.SetPropertyBlock(propertyBlock);
        }
        private void OnSeparationChanged(bool value) { view.SetSeparationEnabled(value); }
        private void OnDestroy()
        {
            if (Parts) Parts.SeparationChanged -= OnSeparationChanged;
            view?.Dispose(); view = null; pose = null;
        }
    }
}
