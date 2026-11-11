using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // The common scene owns movement/time; the full source skeleton owns geometry
    // and follower bones. No sampled-pose or historical transform binding is used.
    public sealed class OfficialBattleLiveMonsterModelView : IOfficialBattleNativeModel, IOfficialBattleNativeModelSpeed
    {
        private readonly BattleNativeMonsterRenderer renderer;
        private readonly OfficialBattleUnitSorting sorting;
        private readonly OfficialBattleShadowView shadow;
        private readonly OfficialBattleShadowBoneFollower follower;
        private readonly BattleNativeMonsterAssets assets;
        private readonly OfficialBattleMonsterAnimatorBindings.State[] states;
        private readonly OfficialBattleMonsterAnimationController application;
        private readonly OfficialBattleMonsterAppearance appearance;
        private OfficialBattleAnimatorPlayback skillPlaybacks;
        private OfficialBattleCharacterGenerationPlayback generation;
        private readonly OfficialBattleCharacterDeathPlayback death;
        private readonly bool destroyOnDeath;
        private readonly float destroyDelayTime;
        private bool dead;
        private double deathElapsed;
        private bool initialIdlePending;
        private float playbackSpeed = 1f;
        private int requestedState;
        private bool loop = true;
        private bool skillControlled;
        private bool disposed;

        public Transform Transform => renderer.EntityRoot;
        public Transform ModelTransform => renderer.transform;
        internal Transform ResolveOriginalTransform(string path)
        {
            // Rebinding schedules the old shadow for Destroy at frame end. A
            // same-name Transform.Find can still return that retired subtree.
            Transform liveShadow = shadow?.Root;
            if (liveShadow)
            {
                string originalPath = liveShadow.name;
                for (Transform parent = liveShadow.parent; parent && parent != Transform; parent = parent.parent)
                    originalPath = parent.name + "/" + originalPath;
                if (originalPath == path) return liveShadow;
            }
            return Transform.Find(path);
        }
        internal bool UsesNativeSkillGraphs { get; }
        public bool IsAnimationFinished => !disposed && (death != null && dead
            ? destroyOnDeath && deathElapsed >= destroyDelayTime
            : !loop && !skillControlled && !renderer.Animator.IsInTransition(0)
            && (renderer.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash != requestedState
                || renderer.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f));

        internal static bool HasBinding(FightBattleUnitState state)
        {
            if (state == null || state.CharacterType == 1) return false;
            int entityId = state.MonsterTemplateId > 0 ? state.MonsterTemplateId : state.MonsterId;
            var assets = Resources.Load<BattleNativeMonsterAssets>(BattleNativeMonsterAssets.ResourcePath);
            return assets && assets.Find(entityId) != null;
        }

        public static OfficialBattleLiveMonsterModelView TryCreate(FightBattleUnitState state, Transform parent,
            bool nativeSkillGraphs = false, OfflineBattleNativePlayableScope scope = null, bool restoring = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Role-player arena guards may carry a monster stat row.
            if (state.CharacterType == 1) return null;
            int entityId = state.MonsterTemplateId > 0 ? state.MonsterTemplateId : state.MonsterId;
            var assets = BattleNativeMonsterAssets.Load();
            return assets.Find(entityId) == null ? null
                : new OfficialBattleLiveMonsterModelView(assets, entityId, parent, "BattleUnit_" + state.Id,
                    nativeSkillGraphs, scope, restoring, state.Hp <= 0);
        }

        private OfficialBattleLiveMonsterModelView(BattleNativeMonsterAssets assets, int entityId,
            Transform parent, string name, bool nativeSkillGraphs, OfflineBattleNativePlayableScope scope, bool restoring, bool initialDead)
        {
            try
            {
                this.assets = assets;
                states = OfficialBattleMonsterAnimatorBindings.Find(entityId);
                var appearanceBinding = BattleNativeMonsterAppearanceCatalog.Find(entityId);
                renderer = BattleNativeMonsterRenderer.Create(assets, entityId, parent, name, appearanceBinding?.Materials);
                renderer.BindOriginalAnimator(assets, entityId);
                renderer.BindOriginalAttachments(entityId);
                renderer.BindOriginalPhaseAnimator(entityId);
                if (appearanceBinding != null) appearance = new OfficialBattleMonsterAppearance(renderer, appearanceBinding);
                application = new OfficialBattleMonsterAnimationController(renderer.EntityRoot, renderer.Animator, entityId);
                UsesNativeSkillGraphs = nativeSkillGraphs;
                Transform body = renderer.transform.parent;
                if (!body || body.name != "body" || !body.IsChildOf(renderer.EntityRoot))
                    throw new InvalidOperationException("Original monster body transform is unbound: " + entityId);
                requestedState = renderer.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                var source = (Dictionary<string, object>)assets.Find(entityId)["meshRenderer"];
                sorting = new OfficialBattleUnitSorting(renderer.transform,
                    checked(Convert.ToInt32(source["m_SortingLayer"]) * 1000 + Convert.ToInt32(source["m_SortingOrder"])));
                shadow = OfficialBattleShadowView.Create(entityId, renderer.EntityRoot, renderer)
                    ?? OfficialBattleShadowView.Create(entityId, renderer.EntityRoot, renderer, "Combat/NativeMonsters/shadow-bindings")
                    ?? throw new InvalidOperationException("Original monster shadow is unbound: " + entityId);
                renderer.Attachments.RebindOriginalShadow(shadow.Root);
                follower = shadow.Root.GetComponent<OfficialBattleShadowBoneFollower>();
                if (!follower) throw new InvalidOperationException("Original monster bone follower is unbound: " + entityId);
                if (scope != null)
                {
                    var role = OfficialBattleSummonRoleViewCatalog.FindRole(entityId);
                    if (role != null)
                    {
                        if (!float.IsFinite(role.DestroyDelayTime) || role.DestroyDelayTime < 0)
                            throw new InvalidOperationException("Original summon death delay is invalid: " + entityId);
                        dead = initialDead;
                        destroyOnDeath = role.DestroyOnDie;
                        destroyDelayTime = role.DestroyDelayTime;
                        death = new OfficialBattleCharacterDeathPlayback(renderer.EntityRoot.gameObject, () => dead,
                            scope, OfficialBattleSummonRoleViewCatalog.FindDeath(entityId));
                        death.OnAnimatorInited();
                    }
                    var binding = OfficialBattleSummonRoleViewCatalog.FindGeneration(entityId);
                    if (binding != null)
                    {
                        generation = new OfficialBattleCharacterGenerationPlayback(renderer.EntityRoot.gameObject,
                            scope, binding, !restoring);
                        // GetOrCreate records an initial idle request. Do not
                        // replace the original generation trigger with Play(idle).
                        initialIdlePending = true;
                        renderer.Animator.Update(0);
                        renderer.ApplyAnimatorPose(0);
                        follower.ForceUpdate();
                    }
                }
            }
            catch { Dispose(); throw; }
        }

        public bool HasAnimation(string name) => !disposed && renderer.AnimationNames.Contains(name)
            && OfficialBattleMonsterAnimatorBindings.UniqueState(states, name) != null;
        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveMonsterModelView));
            if (!HasAnimation(name)) return false;
            if (death != null && name == "die_01")
            {
                initialIdlePending = false;
                skillPlaybacks?.Stop();
                if (dead) { if (restart) death.ForcePlayDiePlayable(); }
                else { dead = true; deathElapsed = 0; death.OnDie(true); }
                loop = false; skillControlled = false; playbackSpeed = 1f;
                renderer.Animator.Update(0);
                renderer.ApplyAnimatorPose(0);
                follower.ForceUpdate();
                return true;
            }
            if (death != null && dead && name == "idle_01")
            { dead = false; deathElapsed = 0; death.OnDie(false); }
            if (initialIdlePending)
            {
                initialIdlePending = false;
                if (name == "idle_01") return true;
            }
            var state = OfficialBattleMonsterAnimatorBindings.UniqueState(states, name);
            int hash = Animator.StringToHash(state.state);
            if (!renderer.Animator.HasState(0, hash)) throw new InvalidOperationException("Original monster state is missing: " + state.state);
            bool changed = restart || renderer.Animation != name || loop != loopAnimation || IsAnimationFinished;
            if (!changed) return true;
            skillPlaybacks?.Stop();
            // The original death playable holds die=true until its owner stops.
            // Idle is the common scene's explicit live/revival request.
            if (name == "die_01" || name == "idle_01") renderer.Animator.SetBool("die", name == "die_01");
            renderer.Animator.speed = 1f;
            renderer.Animator.Play(hash, 0, 0);
            renderer.Animator.Update(0);
            renderer.ApplyAnimatorPose(0);
            requestedState = hash;
            skillControlled = false;
            loop = loopAnimation;
            if (changed) playbackSpeed = 1f;
            follower.ForceUpdate();
            return true;
        }

        public bool Supports(OfficialBattleSkillPlayableCue cue) => !disposed
            && OfficialBattleAnimatorPlayback.Supports(renderer.Animator, cue);

        public void TriggerBodyAnimator(string parameter)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveMonsterModelView));
            renderer.TriggerBodyAnimator(parameter);
            follower.ForceUpdate();
        }

        public bool PlaySkill(OfficialBattleSkillPlayableCue cue, AnimationClip marker, float ownerDuration = -1f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveMonsterModelView));
            if (ownerDuration == 0 || !OfficialBattleAnimatorPlayback.Supports(renderer.Animator, cue)) return false;
            AnimationClip motion = cue.kind == 1
                ? OfficialBattleMonsterAnimatorBindings.ResolveMotion(assets, states, marker) : null;
            if (cue.kind == 1 && (!motion || !renderer.AnimationNames.Contains(motion.name))) return false;
            skillPlaybacks ??= new OfficialBattleAnimatorPlayback(renderer.Animator, Transform,
                UsesNativeSkillGraphs ? application.Controller : null);
            skillPlaybacks.Play(cue, motion, ownerDuration);
            loop = false; skillControlled = true; playbackSpeed = 1f;
            renderer.Animator.Update(0);
            renderer.ApplyAnimatorPose(0);
            requestedState = renderer.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            follower.ForceUpdate();
            return true;
        }
        public void SetPlaybackSpeed(float speed)
        { playbackSpeed = float.IsFinite(speed) && speed > 0 ? speed : 1f; }
        public void Tick(float deltaTime)
        {
            if (disposed) return;
            appearance?.Tick();
            if (death != null && dead) deathElapsed += Math.Max(0, deltaTime);
            generation?.Tick(deltaTime);
            skillPlaybacks?.Tick(deltaTime);
            if (!renderer.gameObject.activeInHierarchy) return;
            renderer.Advance(deltaTime * playbackSpeed);
            follower.ForceUpdate();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            death?.Dispose();
            generation?.Dispose();
            skillPlaybacks?.Dispose();
            application?.Dispose();
            shadow?.Dispose();
            sorting?.Dispose();
            appearance?.Dispose();
            if (renderer) renderer.Release();
        }
    }
}
