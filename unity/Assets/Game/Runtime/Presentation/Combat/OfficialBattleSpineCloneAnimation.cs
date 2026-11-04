using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Native endpoint for the original clone's independent SkeletonAnimation.
    // Appearance and renderer settings must be bound from that clone prefab.
    public sealed class OfficialBattleSpineCloneAnimation : MonoBehaviour,
        IBattleSpineCloneAnimationBackend, IProfessionAppearanceAnimationState
    {
        private sealed class Track : IProfessionAppearanceTrack, IProfessionAppearanceAnimation
        {
            internal ProfessionNativeAnimationChannels Channels;
            internal float Time;
            internal bool Loop;
            public IProfessionAppearanceAnimation Animation => this;
            public string Name => Channels.Name;
        }

        private ProfessionNativeSceneRenderer rendererGraph;
        private ProfessionNativeAppearanceSkeleton skeleton;
        private ProfessionNativePlayerPhysicsPose physics;
        private Track current;
        private string pendingName;
        private bool pendingLoop;

        public IProfessionAppearanceAnimationState AnimState => skeleton == null ? null : this;
        public float AnimationTime => current?.Time ?? 0;
        public float PhysicsTime => physics?.PhysicsTime ?? 0;

        public void Configure(ProfessionNativeSceneRenderer renderer)
        {
            if (!renderer || renderer.gameObject != gameObject)
                throw new ArgumentException("Bind the original clone's own native renderer.", nameof(renderer));
            if (renderer.GetComponent<Animator>())
                throw new InvalidOperationException("An independent clone must not share the source Animator clock.");
            if (rendererGraph) rendererGraph.OnAnimationRebuild -= OnRebuild;
            rendererGraph = renderer;
            rendererGraph.OnAnimationRebuild += OnRebuild;
            OnRebuild(rendererGraph);
        }

        private void OnRebuild(IProfessionFollowerAnimation ignored)
        {
            skeleton = rendererGraph.State;
            physics = skeleton == null ? null : new ProfessionNativePlayerPhysicsPose(skeleton.Source);
            current = null;
            if (skeleton != null && pendingName != null)
            {
                string name = pendingName;
                pendingName = null;
                PlayAnimation(name, pendingLoop);
            }
        }

        public IProfessionAppearanceTrack GetCurrent(int track) => track == 0 ? current : null;

        public void PlayAnimation(string name, bool loop)
        {
            if (!rendererGraph)
                throw new InvalidOperationException("Bind the native clone renderer before playing its animation.");
            // Preserve AppearanceAnimUtils' null-data and pending-state branches.
            if (!rendererGraph.skeletonDataAsset) return;
            if (rendererGraph.State == null)
            {
                pendingName = name;
                pendingLoop = loop;
                return;
            }
            if (!ReferenceEquals(skeleton, rendererGraph.State)) OnRebuild(rendererGraph);
            bool found = false;
            foreach (string candidate in skeleton.Source.AnimationNames)
                if (candidate == name) { found = true; break; }
            if (!found)
            {
                Debug.LogWarning("不存在指定的动画:" + name, this);
                return;
            }
            current = new Track { Channels = new ProfessionNativeAnimationChannels(skeleton.Source, name, true), Loop = loop };
            // Set track zero, zero mix, then apply and draw now. Replaying a
            // track does not reset the skeleton's independent physics clock.
            Apply(0);
            rendererGraph.LateUpdateMesh();
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!rendererGraph || rendererGraph.State == null) return;
            if (!ReferenceEquals(skeleton, rendererGraph.State)) OnRebuild(rendererGraph);
            if (rendererGraph.UpdateMode == 0) return;
            if (current != null) current.Time += deltaTime;
            Apply(deltaTime);
        }

        private void Apply(float deltaTime)
            => physics.Apply(skeleton, current?.Channels, current?.Time ?? 0, current?.Loop ?? false, deltaTime);

        private void Update() => Tick(UnityEngine.Time.deltaTime);
        private void OnDestroy()
        {
            if (rendererGraph) rendererGraph.OnAnimationRebuild -= OnRebuild;
        }
    }
}
