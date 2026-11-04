using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // The original controller owns ambient/skill transitions. Movement and role
    // death parameters come from the original application playables, not aliases.
    public sealed class OfficialBattlePartnerNativeModelView : IOfficialBattleNativeModel
    {
        [Serializable] private sealed class Views { public string manifest; public Role[] partners; }
        [Serializable] private sealed class Role
        {
            public int npcId, entityId;
            public bool tryOnStart, generateNeedsDelay;
            public float generateDelayTime;
            public string mDiePlayable, mDieOnStartPlayable, mGeneratePlayable;
        }
        private readonly PartnerNativeBattleActor actor;
        private readonly PartnerNativeBattleAnimationBinding animation;
        private readonly OfficialBattleAnimatorPlayback movement;
        private readonly OfficialBattleCharacterDeathPlayback death;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly List<OfficialBattleUnitSorting> sorting = new List<OfficialBattleUnitSorting>();
        private readonly OfficialBattleShadowView shadow;
        private readonly OfficialBattleShadowBoneFollower boneFollower;
        private MyPlayable generation;
        private bool dead, disposed;

        public Transform Transform => animation.Source.transform;
        public Transform ModelTransform => actor.Animator.transform;
        public Animator Animator => actor.Animator;
        public PartnerNativeBattleActor Actor => actor;
        // ECFightRoleCharacterView retains its indefinite death playable until
        // revival/unload; reaching the last animation frame does not remove it.
        public bool IsAnimationFinished => false;

        public static bool HasBinding(int npcId)
        {
            if (npcId <= 0 || npcId >= 11015 && npcId <= 11022) return false;
            var catalog = Resources.Load<PartnerNativeBattleAssetCatalog>(PartnerNativeBattleAssetCatalog.ResourcePath);
            return catalog && catalog.Find(npcId) != null;
        }

        public static OfficialBattlePartnerNativeModelView TryCreate(FightBattleUnitState state,
            Transform parent, OfflineBattleNativePlayableScope scope, bool restoring)
        {
            if (scope == null || state == null || !HasBinding(state.NpcClassId)) return null;
            return new OfficialBattlePartnerNativeModelView(state, parent, scope, restoring);
        }

        private OfficialBattlePartnerNativeModelView(FightBattleUnitState state, Transform parent,
            OfflineBattleNativePlayableScope scope, bool restoring)
        {
            this.scope = scope;
            try
            {
                var text = Resources.Load<TextAsset>("Combat/NativePartnerAssets/role-views");
                if (!text) throw new InvalidOperationException("Original partner role views are unbound.");
                var views = JsonUtility.FromJson<Views>(text.text);
                if (views?.manifest != "884_156692") throw new InvalidOperationException("Original partner role view version differs.");
                Role role = views.partners.Single(row => row.npcId == state.NpcClassId);
                actor = new PartnerNativeBattleActor(PartnerNativeBattleAssetCatalog.Load(), role.npcId,
                    parent, "BattleUnit_" + state.Id);
                if (Convert.ToInt32(actor.OriginalRow["entityId"]) != role.entityId)
                    throw new InvalidOperationException("Original partner battle identity differs.");
                animation = new PartnerNativeBattleAnimationBinding(actor);
                var rows = new[] { actor.OriginalRow }.Concat(((List<object>)actor.OriginalRow["parts"])
                    .Cast<Dictionary<string, object>>()).ToArray();
                for (int index = 0; index < actor.Renderers.Count; index++)
                {
                    var fields = (Dictionary<string, object>)rows[index]["meshRenderer"];
                    sorting.Add(new OfficialBattleUnitSorting(actor.Renderers[index].transform,
                        checked(Convert.ToInt32(fields["m_SortingLayer"]) * 1000 + Convert.ToInt32(fields["m_SortingOrder"]))));
                }
                shadow = OfficialBattleShadowView.Create(role.entityId, animation.Source.transform,
                    actor.Attachments.OriginalRenderers.Values.First(), "Combat/NativePartnerAssets/partner-shadow-bindings")
                    ?? throw new InvalidOperationException("Original partner shadow is unbound: " + role.npcId);
                actor.Attachments.RebindOriginalShadow(shadow.Root);
                boneFollower = shadow.Root.GetComponent<OfficialBattleShadowBoneFollower>();
                movement = new OfficialBattleAnimatorPlayback(actor.Animator, actor.Root, animation.Controller);
                dead = state.Hp <= 0;
                death = new OfficialBattleCharacterDeathPlayback(animation.Source, () => dead, scope,
                    new OfficialBattleCharacterDeathCatalog.Binding {
                        Normal = Load(role.mDiePlayable), OnStart = Load(role.mDieOnStartPlayable), TryOnStart = role.tryOnStart });
                death.OnAnimatorInited();
                if (!restoring && !dead && !string.IsNullOrEmpty(role.mGeneratePlayable))
                {
                    if (role.generateNeedsDelay || role.generateDelayTime != 0)
                        throw new NotSupportedException("Bind the original delayed generation event before opening this partner.");
                    generation = scope.Play(Load(role.mGeneratePlayable), animation.Source, animation.Source);
                }
            }
            catch { Dispose(); throw; }
        }

        private static GameObject Load(string path) => string.IsNullOrEmpty(path) ? null
            : Resources.Load<GameObject>(path) ?? throw new InvalidOperationException("Original partner lifecycle prefab is missing: " + path);

        public bool Supports(OfficialBattleSkillPlayableCue cue) => OfficialBattleAnimatorPlayback.Supports(Animator, cue);
        public void PlayMovement(OfficialBattleSkillPlayableCue cue, float duration) => movement.Play(cue, null, duration);
        public bool HasAnimation(string name) => name == "idle_01" || name == "die_01";
        public bool Play(string name, bool loop, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattlePartnerNativeModelView));
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
            movement.Tick(deltaTime);
            actor.Advance(deltaTime);
            if (boneFollower) boneFollower.ForceUpdate();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (generation && generation.myStatus != MyPlayable.PlayableStatus.Stop) scope.Stop(generation);
            death?.Dispose(); movement?.Dispose(); shadow?.Dispose();
            foreach (var group in sorting) group.Dispose();
            animation?.Dispose(); actor?.Dispose();
        }
    }
}
