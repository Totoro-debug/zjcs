using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Official.StorySourceFade;

namespace Zjcs.Offline.World.NativeMap13EntityViews
{
    // Resources are injected while the original model subtree is inactive.
    // Its original components then receive normal Awake/OnEnable callbacks;
    // independent geometry is initialized immediately after activation.
    public sealed class NativeMap13EntityModelBinding : MonoBehaviour
    {
        [Serializable] public sealed class OriginalAnimator
        { public Animator Animator; public string ControllerKey; }
        public string SourceKey;
        [Serializable] public sealed class OriginalExternalFadeTarget
        { public SpriteRendererList Owner; public int Index; public string GameObjectKey; }
        public OriginalExternalFadeTarget[] ExternalFadeTargets;
        public OriginalAnimator[] Animators;
        [Serializable] public sealed class OriginalFadeTarget
        { public SpriteRendererList Owner; public NativeMap13PropFadeRenderer Renderer; public NativeMap13PropMecanim Skeleton; }
        public OriginalFadeTarget[] FadeTargets;
        public NativeMap13PropMecanim[] Skeletons;
        public NativeMap13EntitySpriteBinding[] Sprites;
        private NativeMap13AnimatorLibrary animationLibrary;
        private bool prepared, initialized;
        private void Awake()
        {
            if (!prepared) throw new InvalidOperationException("Original Model must receive source resource/external bindings before Awake.");
        }
        public void Prepare(NativeMap13AnimatorLibrary animations, NativeMap13EntitySpriteLibrary sprites,
            IReadOnlyDictionary<string, GameObject> externalObjects,
            IReadOnlyDictionary<string, RuntimeAnimatorController> additionalControllers)
        {
            if (prepared || gameObject.activeInHierarchy) throw new InvalidOperationException("Prepare original model once, before activation.");
            animationLibrary = animations ?? throw new ArgumentNullException(nameof(animations));
            if (sprites == null) throw new ArgumentNullException(nameof(sprites));
            foreach (var target in ExternalFadeTargets)
            {
                if (externalObjects == null || !externalObjects.TryGetValue(target.GameObjectKey, out var gameObject) || !gameObject)
                    throw new InvalidOperationException("Original external UI fade target is absent: " + target.GameObjectKey);
                target.Owner.FadeInCompleteActiveGO[target.Index] = gameObject;
            }
            foreach (var binding in Sprites) binding.Bind(sprites);
            foreach (var target in FadeTargets)
            {
                if (!target.Owner || !target.Renderer || !target.Skeleton || target.Renderer.gameObject != target.Skeleton.gameObject)
                    throw new InvalidOperationException("Original application fade binding is incomplete.");
                target.Renderer.Renderer = target.Skeleton;
                target.Owner.SkeletonRenderer = target.Renderer;
            }
            foreach (var binding in Animators)
                binding.Animator.runtimeAnimatorController = additionalControllers != null && additionalControllers.TryGetValue(binding.ControllerKey, out var controller)
                    ? controller : animations.Controller(binding.ControllerKey);
            prepared = true;
        }
        public void InitializeActive()
        {
            if (!prepared || initialized || !gameObject.activeInHierarchy) throw new InvalidOperationException("Initialize prepared original model after activation.");
            foreach (var skeleton in Skeletons) skeleton.Initialize(animationLibrary);
            initialized = true;
        }
    }
}
