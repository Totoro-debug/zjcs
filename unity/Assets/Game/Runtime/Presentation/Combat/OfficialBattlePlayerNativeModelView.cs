using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    // The shared battle supplies grid movement, HP and skill graphs. This view
    // owns the original player actor, ambient/death Animator and shadow binding.
    public sealed class OfficialBattlePlayerNativeModelView : IOfficialBattleNativeModel
    {
        private readonly OfflineProfessionBattlePlayerResources resources;
        private readonly OfflineProfessionBattlePlayerResources.Player player;
        private readonly OfficialBattleAnimatorPlayback movement;
        private readonly OfficialBattleCharacterDeathPlayback death;
        private readonly OfficialBattleUnitSorting sorting;
        private readonly OfficialBattleShadowView shadow;
        private bool dead, disposed;

        public ProfessionNativeBattlePlayerActor Actor => player.Actor;
        public Transform Transform => Actor.Root.transform;
        public Transform ModelTransform => Actor.Animator.transform;
        public Animator Animator => Actor.Animator;
        // The original indefinite death graph remains until revival or unload.
        public bool IsAnimationFinished => false;

        public OfficialBattlePlayerNativeModelView(FightBattleUnitState state, Transform parent,
            OfflineBattleNativePlayableScope scope, OfflineProfessionBattlePlayerResources resources,
            long playerId, Func<OfflinePlayerProgress> readPlayer, Func<int, int?> itemType)
        {
            if (state == null || state.CharacterType != 1 || scope == null)
                throw new ArgumentException("Original player role state and native battle graph scope are required.");
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            try
            {
                player = resources.Create(parent, playerId, readPlayer, itemType);
                var renderer = Actor.Renderer.GetComponent<MeshRenderer>();
                int layer = Array.FindIndex(SortingLayer.layers, value => value.id == renderer.sortingLayerID);
                if (layer < 0) throw new InvalidOperationException("Original player sorting layer is unavailable.");
                // Include the original sibling WeaponEffect branch while keeping
                // shadow/UI outside this group's local body/effect draw order.
                sorting = new OfficialBattleUnitSorting(Actor.Root.transform.Find("3100_view/body"),
                    checked(layer * 1000 + renderer.sortingOrder));
                shadow = OfficialBattleShadowView.Create(3100, Transform, Actor.Renderer)
                    ?? throw new InvalidOperationException("Original player shadow is unavailable.");
                Actor.Attachments.RebindOriginalShadow(shadow.Root);
                movement = new OfficialBattleAnimatorPlayback(Animator, Transform, Actor.Controller);
                dead = state.Hp <= 0;
                death = new OfficialBattleCharacterDeathPlayback(Actor.Root, () => dead, scope,
                    OfficialBattleCharacterDeathCatalog.LoadDefault().Find(3100));
                Actor.Appearance.OnAnimatorChanged += death.OnAnimatorInited;
                death.OnAnimatorInited();
            }
            catch { Dispose(); throw; }
        }

        public bool Supports(OfficialBattleSkillPlayableCue cue) => OfficialBattleAnimatorPlayback.Supports(Animator, cue);
        public void PlayMovement(OfficialBattleSkillPlayableCue cue, float duration) => movement.Play(cue, null, duration);
        public void RefreshAppearance() => player.RefreshAppearance();
        public bool HasAnimation(string name) => name == "idle_01" || name == "die_01";
        public bool Play(string name, bool loop, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattlePlayerNativeModelView));
            if (!HasAnimation(name)) return false;
            if (name == "die_01")
            {
                movement.Stop();
                if (dead) { if (restart) death.ForcePlayDiePlayable(); }
                else { dead = true; death.OnDie(true); }
            }
            else if (dead) { dead = false; death.OnDie(false); }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (disposed) return;
            movement.Tick(deltaTime); resources.Update();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (player != null && death != null) Actor.Appearance.OnAnimatorChanged -= death.OnAnimatorInited;
            death?.Dispose(); movement?.Dispose(); shadow?.Dispose(); sorting?.Dispose(); player?.Dispose();
        }
    }
}
