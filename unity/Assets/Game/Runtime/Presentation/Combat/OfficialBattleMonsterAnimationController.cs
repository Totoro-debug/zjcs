using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Presentation.Combat
{
    // Keep the original application controller on the entity node that native
    // ApplyBySource leaves query; the original Animator remains on its model.
    internal sealed class OfficialBattleMonsterAnimationController : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest; public Binding[] models; }
        [Serializable] private sealed class Binding
        {
            public int entityId;
            public string animatorPath;
            public bool initOnAwake, createOverride, notCreateGraph, resetOnDisable, enabled;
        }
        [Serializable] private sealed class PrivateSettings { public bool mResetAnimatorInitedOnDisable; }
        private static Dictionary<int, Binding> bindings;
        private readonly Animator animator;
        private readonly RuntimeAnimatorController original;
        private bool disposed;
        internal AnimationController Controller { get; private set; }

        internal OfficialBattleMonsterAnimationController(Transform entity, Animator animator, int entityId)
        {
            this.animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            if (!entity) throw new ArgumentNullException(nameof(entity));
            if (bindings == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/monster-animation-controllers");
                var data = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
                if (data?.manifest != "884_156692" || data.models == null)
                    throw new InvalidOperationException("Original monster application controller bindings are missing.");
                bindings = data.models.ToDictionary(value => value.entityId);
            }
            if (!bindings.TryGetValue(entityId, out var binding) || entity.Find(binding.animatorPath) != animator.transform)
                throw new InvalidOperationException("Original monster application Animator hierarchy differs: " + entityId);
            if (entity.GetComponent<AnimationController>())
                throw new InvalidOperationException("Original monster application controller is already bound.");
            original = animator.runtimeAnimatorController;
            bool active = entity.gameObject.activeSelf;
            entity.gameObject.SetActive(false);
            try
            {
                Controller = entity.gameObject.AddComponent<AnimationController>();
                Controller.myAnimator = animator;
                Controller.InitOnAwake = binding.initOnAwake;
                Controller.CreateOverrideController = binding.createOverride;
                Controller.NotCreateGraph = binding.notCreateGraph;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new PrivateSettings {
                    mResetAnimatorInitedOnDisable = binding.resetOnDisable }), Controller);
                Controller.enabled = binding.enabled;
            }
            catch { Dispose(); throw; }
            finally { entity.gameObject.SetActive(active); }
            if (entity.gameObject.activeInHierarchy && binding.initOnAwake && !Controller.myInited)
            { Dispose(); throw new InvalidOperationException("Original monster application controller did not initialize."); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!Controller) return;
            RuntimeAnimatorController owned = Controller.RuntimeAnimatorController;
            if (animator && animator.runtimeAnimatorController == owned) animator.runtimeAnimatorController = original;
            if (owned && owned != original) Destroy(owned);
            Controller.InitOnAwake = false;
            Destroy(Controller); Controller = null;
        }
        private static void Destroy(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
