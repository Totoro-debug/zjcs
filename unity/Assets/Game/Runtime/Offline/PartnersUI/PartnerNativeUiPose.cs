using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Track zero for the original detail sequence and illustration calls. The
    // independent geometry pipeline owns poses, mixing and ordered constraints.
    public sealed class PartnerNativeUiPose : IProfessionNativeUiPoseSource, IPartnerSequenceGraphic,
        IPartnerSequenceAnimationState, IPartnerSequenceSkeleton, IProfessionAppearanceAnimationState,
        IProfessionNativeUiPhysicsMovement, IDisposable
    {
        private sealed class Clip : IPartnerSequenceAnimation, IProfessionAppearanceAnimation
        {
            internal readonly ProfessionNativeAnimationChannels Channels;
            internal Clip(ProfessionNativeAnimationChannels channels) { Channels = channels; }
            public string Name => Channels.Name;
            public float Duration => Channels.Duration;
        }
        private sealed class Track : IPartnerSequenceTrack, IProfessionAppearanceTrack
        {
            internal readonly Clip Clip;
            internal float Time;
            internal long Completed;
            internal Track Previous;
            internal Clip Resume;
            internal Track(Clip clip, bool loop, float mix) { Clip = clip; Loop = loop; MixDuration = mix; }
            public IPartnerSequenceAnimation Animation => Clip;
            IProfessionAppearanceAnimation IProfessionAppearanceTrack.Animation => Clip;
            public bool Loop { get; }
            public float MixTime { get; set; }
            public float MixDuration { get; set; }
        }
        private readonly PartnerNativeUiSkeletonAsset asset;
        private readonly ProfessionNativeUiGraphic graphic;
        private readonly Func<string, int, Material> material;
        private readonly Func<string, Texture> texture;
        private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>(StringComparer.Ordinal);
        private readonly ProfessionNativeScenePhysicsMovement movement = new ProfessionNativeScenePhysicsMovement();
        private PartnerNativeUiPhysicsPose physics;
        private ProfessionNativeAppearanceSkeleton owned;
        private Track current;
        private bool disposed;
        public PartnerNativeUiPose(PartnerNativeUiSkeletonAsset asset, ProfessionNativeUiGraphic graphic,
            Func<string, int, Material> material, Func<string, Texture> texture)
        {
            this.asset = asset ? asset : throw new ArgumentNullException(nameof(asset));
            this.graphic = graphic ? graphic : throw new ArgumentNullException(nameof(graphic));
            this.material = material ?? throw new ArgumentNullException(nameof(material));
            this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
            graphic.OnAnimationRebuild += Rebuild;
        }
        public ScriptableObject Asset => asset;
        public IPartnerSequenceAnimationState AnimationState => owned != null ? this : null;
        IProfessionAppearanceAnimationState IProfessionNativeUiPoseSource.AnimationState => owned != null ? this : null;
        public IPartnerSequenceSkeleton Skeleton => owned != null ? this : null;
        public event Action<IPartnerSequenceGraphic> OnAnimationRebuild;
        public event Action<IPartnerSequenceTrack> Start;
        public event Action<IPartnerSequenceTrack> Complete;
        public float PhysicsTime => physics?.PhysicsTime ?? 0;
        public ProfessionNativeAppearanceSkeleton CreateSkeleton(string initialSkin)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PartnerNativeUiPose));
            owned = new ProfessionNativeAppearanceSkeleton(asset.Data);
            var skin = asset.Data.SkinCatalog.FindSkin(string.IsNullOrEmpty(initialSkin) ? "default" : initialSkin) as ProfessionNativeSkin;
            if (skin == null) throw new InvalidOperationException("Original partner UI initial skin is unavailable: " + initialSkin);
            owned.SetSkin(skin); owned.SetToSetupPose(); owned.UpdateCache();
            current = null; physics = new PartnerNativeUiPhysicsPose(asset.Data);
            movement.Reset(graphic.transform);
            return owned;
        }
        public void SetToSetupPose() => owned.SetToSetupPose();
        public IPartnerSequenceAnimation GetOrLoadAnimation(string name)
        {
            if (!clips.TryGetValue(name, out var clip))
            {
                if (!asset.Data.AnimationNames.Contains(name)) return null;
                clips.Add(name, clip = new Clip(new ProfessionNativeAnimationChannels(asset.Data, name, originalBezierSegments: true)));
            }
            return clip;
        }
        public IPartnerSequenceTrack GetCurrent(int track) { RequireTrack(track); return current; }
        IProfessionAppearanceTrack IProfessionAppearanceAnimationState.GetCurrent(int track) { RequireTrack(track); return current; }
        public IPartnerSequenceTrack SetAnimation(int track, IPartnerSequenceAnimation animation, bool loop)
        {
            RequireTrack(track);
            if (owned == null) throw new InvalidOperationException("Initialize the original UI skeleton before its animation.");
            if (animation is not Clip clip || !clips.TryGetValue(clip.Name, out var original) || original != clip)
                throw new ArgumentException("Original animation belongs to another UI skeleton.");
            var previous = current;
            current = new Track(clip, loop, asset.DefaultMix) { Previous = previous };
            Start?.Invoke(current);
            return current;
        }
        public void ClearTrack(int track) { RequireTrack(track); current = null; }
        public void Play(ProfessionNativeAppearanceSkeleton skeleton, string name, bool loop)
        {
            RequireOwned(skeleton);
            if (GetOrLoadAnimation(name) is IPartnerSequenceAnimation clip) SetAnimation(0, clip, loop);
        }
        public void Play(string name, bool loop, float mixDuration, bool endResumeLast)
        {
            if (string.IsNullOrEmpty(name)) return;
            var clip = GetOrLoadAnimation(name);
            if (clip == null) return;
            Clip previous = current?.Clip;
            var next = (Track)SetAnimation(0, clip, loop);
            next.MixDuration = mixDuration; next.MixTime = mixDuration > 0 ? .001f : 0;
            if (!loop && endResumeLast && previous != null) next.Resume = previous;
            UpdateAnimation();
        }
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, float deltaTime)
        {
            RequireOwned(skeleton);
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            var track = current;
            if (track == null) return;
            float next = track.Time + deltaTime;
            if (!float.IsFinite(next)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            track.Time = next;
            var chain = new List<Track>();
            for (var item = track; item != null; item = item.Previous)
            {
                if (item != track) item.Time += deltaTime;
                item.MixTime += deltaTime;
                chain.Add(item);
                if (item.MixDuration <= 0 || item.MixTime >= item.MixDuration)
                { item.Previous = null; break; }
            }
            chain.Reverse();
            physics.Apply(skeleton, chain.Select((item, index) => new ProfessionNativeOrderedConstraintPose.AnimationLayer(
                item.Clip.Channels, item.Time, item.Loop, index == 0 ? 1 : Mathf.Clamp01(item.MixTime / item.MixDuration))).ToArray(), deltaTime);
            float duration = track.Clip.Duration;
            long completed = duration > 0 ? (long)(next / duration) : deltaTime > 0 ? 1 : 0;
            if (!track.Loop) completed = Math.Min(1, completed);
            while (track.Completed < completed && ReferenceEquals(current, track))
            {
                track.Completed++;
                Complete?.Invoke(track);
            }
            float resumeAt = Math.Max(0, duration - asset.DefaultMix);
            if (ReferenceEquals(current, track) && !track.Loop && track.Resume != null && next >= resumeAt)
            {
                var resumed = (Track)SetAnimation(0, track.Resume, true);
                resumed.Time = Math.Max(0, next - resumeAt);
            }
        }
        public void UpdateAnimation() => graphic.ApplyAnimation();
        public void UpdatePhysicsMovement()
        {
            if (physics == null) return;
            if (graphic.physicsMovementRelativeTo)
                throw new NotSupportedException("Bind the original relative UI physics frame before use.");
            movement.Apply(graphic.transform, asset.Data.DataScale * graphic.MeshScale,
                graphic.physicsPositionInheritanceFactor, graphic.physicsRotationInheritanceFactor, physics.Translate, physics.Rotate);
        }
        public Material MaterialForPage(string page, int blend) => material(page, blend);
        public Texture TextureForPage(string page) => texture(page);
        private void Rebuild(IProfessionFollowerAnimation value) => OnAnimationRebuild?.Invoke(this);
        private static void RequireTrack(int track) { if (track != 0) throw new ArgumentOutOfRangeException(nameof(track)); }
        private void RequireOwned(ProfessionNativeAppearanceSkeleton skeleton)
        { if (disposed || skeleton == null || skeleton != owned) throw new ArgumentException("Bind this exact original UI skeleton."); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (graphic) graphic.OnAnimationRebuild -= Rebuild;
            current = null; owned = null;
        }
    }
}
