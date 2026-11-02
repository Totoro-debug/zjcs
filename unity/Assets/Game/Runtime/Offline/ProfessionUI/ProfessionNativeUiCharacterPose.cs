using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Track zero for the actual UI application calls (mixDuration=0,
    // endResumeLast=false). The default producer includes the native ordered
    // constraints and physics; specialized producers remain explicit.
    public sealed class ProfessionNativeUiCharacterPose : IProfessionNativeUiPoseSource
    {
        private readonly ProfessionNativeSceneCharacterAsset asset;
        private readonly Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool, float> solve;
        private readonly Func<string, int, Material> material;
        private readonly Func<string, Texture> texture;
        private ProfessionNativeAppearanceSkeleton owned;
        private Track current;
        private float time;
        private bool loop;
        private readonly State state;

        public ProfessionNativeUiCharacterPose(ProfessionNativeSceneCharacterAsset asset,
            Func<string, int, Material> material, Func<string, Texture> texture)
            : this(asset, new ProfessionNativePlayerPhysicsPose(asset ? asset.Data :
                throw new ArgumentNullException(nameof(asset))).Apply, material, texture) { }

        public ProfessionNativeUiCharacterPose(ProfessionNativeSceneCharacterAsset asset,
            Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool, float> solve,
            Func<string, int, Material> material, Func<string, Texture> texture)
        {
            this.asset = asset ? asset : throw new ArgumentNullException(nameof(asset));
            this.solve = solve ?? throw new ArgumentNullException(nameof(solve));
            this.material = material ?? throw new ArgumentNullException(nameof(material));
            this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
            state = new State(this);
        }
        public ScriptableObject Asset => asset;
        public IProfessionAppearanceAnimationState AnimationState => owned != null ? state : null;
        public ProfessionNativeAppearanceSkeleton CreateSkeleton(string initialSkin)
        {
            var next = new ProfessionNativeAppearanceSkeleton(asset.Data);
            var skin = next.Data.FindSkin(initialSkin) ?? throw new InvalidOperationException("Original UI initial skin is unavailable: " + initialSkin);
            next.SetSkin(skin); next.SetToSetupPose(); next.UpdateCache();
            owned = next; current = null; time = 0; loop = false;
            return next;
        }
        public void Play(ProfessionNativeAppearanceSkeleton skeleton, string name, bool repeat)
        {
            RequireOwned(skeleton);
            var channels = new ProfessionNativeAnimationChannels(asset.Data, name, originalBezierSegments: true);
            current = new Track(channels); loop = repeat; time = 0;
        }
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, float deltaTime)
        {
            RequireOwned(skeleton);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Bind reversed or non-finite UI animation time explicitly.");
            float next = time + deltaTime;
            if (float.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            solve(skeleton, current?.Channels, next, loop, deltaTime);
            time = next;
        }
        public Material MaterialForPage(string page, int blend) => material(page, blend);
        public Texture TextureForPage(string page) => texture(page);
        private void RequireOwned(ProfessionNativeAppearanceSkeleton value)
        {
            if (value == null || !ReferenceEquals(value, owned))
                throw new ArgumentException("UI pose source belongs to another original skeleton.", nameof(value));
        }
        private sealed class Track : IProfessionAppearanceTrack, IProfessionAppearanceAnimation
        {
            internal readonly ProfessionNativeAnimationChannels Channels;
            internal Track(ProfessionNativeAnimationChannels channels) { Channels = channels; }
            public IProfessionAppearanceAnimation Animation => this;
            public string Name => Channels.Name;
        }
        private sealed class State : IProfessionAppearanceAnimationState
        {
            private readonly ProfessionNativeUiCharacterPose owner;
            internal State(ProfessionNativeUiCharacterPose owner) { this.owner = owner; }
            public IProfessionAppearanceTrack GetCurrent(int track)
            {
                if (track < 0) throw new ArgumentOutOfRangeException(nameof(track));
                return track == 0 ? owner.current : null;
            }
        }
    }
}
