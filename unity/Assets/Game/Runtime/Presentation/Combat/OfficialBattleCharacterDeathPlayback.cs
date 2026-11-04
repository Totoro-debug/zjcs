using System;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Presentation.Combat
{
    // Death-only methods from ECFightRoleCharacterView. The owner supplies the
    // exact entity root, current Die value and original serialized view binding.
    // Appearance/HUD/generation methods remain with the owning character view.
    public sealed class OfficialBattleCharacterDeathPlayback : IBattleSourceDeathView, IDisposable
    {
        private readonly GameObject source, mDiePlayable, mDieOnStartPlayable;
        private readonly bool mTryDiePlayableOnStart;
        private readonly Func<bool> isDead;
        private readonly IMyPlayableManager MyPlayableManager;
        private MyPlayable mDiePlayableHandle;
        private bool mInitAnimator;

        public OfficialBattleCharacterDeathPlayback(GameObject entityRoot, Func<bool> isDead,
            IMyPlayableManager manager, OfficialBattleCharacterDeathCatalog.Binding binding)
        {
            source = entityRoot ? entityRoot : throw new ArgumentNullException(nameof(entityRoot));
            this.isDead = isDead ?? throw new ArgumentNullException(nameof(isDead));
            MyPlayableManager = manager ?? throw new ArgumentNullException(nameof(manager));
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            mDiePlayable = binding.Normal; mDieOnStartPlayable = binding.OnStart;
            mTryDiePlayableOnStart = binding.TryOnStart;
        }

        public void OnDie(bool die) => UpdateDiePlayable(die, false);

        public void OnAnimatorInited()
        {
            mInitAnimator = true;
            if (mTryDiePlayableOnStart) UpdateDiePlayable(isDead(), true);
        }

        private void UpdateDiePlayable(bool isDie, bool isOnStart)
        {
            if (mInitAnimator)
            {
                if (isDie) PlayDiePlayable(isOnStart);
                else StopDiePlayable();
            }
        }

        public void ForcePlayDiePlayable()
        {
            if (isDead())
            {
                StopDiePlayable();
                UpdateDiePlayable(isDead(), false);
            }
        }

        void IBattleSourceDeathView.ForcePlayDiePlayable(GameObject value)
        {
            if (value == source) ForcePlayDiePlayable();
        }

        private void PlayDiePlayable(bool isOnStart)
        {
            if (mDiePlayableHandle != null) return;
            GameObject prefab = !isOnStart ? mDiePlayable : mDieOnStartPlayable;
            if (prefab == null) return;
            // The original resolves this exact entity through GetComponentInParent<MyEntity>.
            // An explicitly bound entity root replaces the global view/container lookup.
            if (source != null)
            {
                if (source.TryGetComponent<AnimationController>(out var controller)) controller.ForceResumeAllPause();
                mDiePlayableHandle = MyPlayableManager.Play(prefab, source, source);
            }
        }

        private void StopDiePlayable()
        {
            if (mDiePlayableHandle != null)
            {
                mDiePlayableHandle.Stop();
                mDiePlayableHandle = null;
            }
        }

        public void Dispose() => StopDiePlayable();
    }
}
