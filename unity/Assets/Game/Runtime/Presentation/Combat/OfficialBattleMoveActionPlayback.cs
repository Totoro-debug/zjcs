using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    // Owns native action lifetimes; the shared graph clock remains with the caller.
    internal sealed class OfficialBattleMoveActionPlayback : IMyActionEntityLookup, IServiceProvider, IECViewInjection, IDisposable
    {
        private sealed class Active
        {
            public EcEntityIdentifier Id;
            public MyEntity Entity;
            public IFightActionSkillMoveView View;
            public float Remaining;
            public bool Blocked;
            public Action Contact;
        }

        private readonly OfficialBattleNativeEntityLoader loader;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Func<EcEntityIdentifier, MyEntity> entityLookup;
        private readonly List<Active> active = new List<Active>();
        private bool disposed;

        public OfficialBattleMoveActionPlayback(Transform parent, MyContainer container,
            OfflineBattleNativePlayableScope scope, Func<EcEntityIdentifier, MyEntity> entityLookup)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.entityLookup = entityLookup ?? throw new ArgumentNullException(nameof(entityLookup));
            loader = new OfficialBattleNativeEntityLoader(parent, container, scope,
                OfficialBattleNativeEntityKind.MoveAction, instance => {
                    var actions = instance.GetComponents<MyAction>();
                    var moves = instance.GetComponents<MyFightActionSkillMove>();
                    if (actions.Length != 1 || moves.Length != 1)
                        throw new InvalidOperationException("Original movement prefab requires one action and one movement view.");
                    actions[0].EntityLookup = this;
                    actions[0].mMyPlayableManager = scope;
                    // Original audited movement actions have no MyAction loop/effect graph.
                    if (actions[0].myEffect || actions[0].myEndEffect || actions[0].LoopEffectPlayable
                        || actions[0].Duration != -1f)
                        throw new InvalidOperationException("Review a changed original movement action performance binding.");
                    moves[0].mMyPlayableManager = scope;
                    moves[0].EntitySlot = parent;
                    moves[0].ManualTiming = true;
                    OfficialBattleCurrentViewBinding.Prepare(instance, this);
                    var driver = instance.GetComponentInChildren<ECViewDriver>(true);
                    moves[0].CurrentView = driver ? driver.GetView<ECFightActionSkillMoveView>() : null;
                });
            scope.NativePlayableStarted += OnNativePlayableStarted;
        }

        MyEntity IMyActionEntityLookup.GetEntity(EcEntityIdentifier id) => entityLookup(id);
        public MyEntity GetEntity(EcEntityIdentifier id) => loader.GetEntity(id);
        public bool ContainsClass(int classId) => loader.ContainsClass(classId);
        public object GetService(Type type) => type == typeof(IMyActionEntityLookup) ? this
            : type == typeof(IMyPlayableManager) ? scope : null;
        public void Inject(object value)
        {
            if (value is ECFightActionSkillMoveView move)
            { move.MyPlayableManager = scope; move.EntityLookup = this; move.EntitySlot = loader.EntitySlot; }
            else throw new InvalidOperationException("Unbound original movement EC view: " + value?.GetType().FullName);
        }

        public float NextDelay
        {
            get {
                float next = float.PositiveInfinity;
                foreach (var value in active)
                    next = Mathf.Min(next, Mathf.Max(0f, value.Remaining), value.View.RemainingMoveTime);
                return next;
            }
        }

        public MyEntity Create(OfficialBattleMoveActionData data, Action contact = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleMoveActionPlayback));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Movement.BlockedPos.HasValue && data.Movement.Info.DelayDestroyTime != 0f)
                throw new NotSupportedException("Blocked action destruction delay requires its original server schedule.");
            if (contact != null && !data.Movement.BlockedPos.HasValue)
                throw new ArgumentException("Collision contact requires a blocked position.");
            if (contact != null)
            {
                var prefab = loader.GetPrefab(data.ClassId);
                var nativeMove = prefab ? MovementView(prefab) : null;
                if (nativeMove == null || !BattleBlockedTrackContact.Supports(nativeMove.BlockPlayablePrefab))
                    throw new NotSupportedException("The original blocked movement has no verified contact track.");
            }
            var entity = loader.Create(data);
            entity.GetComponent<MyFightActionSkillMove>().EntitySlot = loader.EntitySlot;
            var view = MovementView(entity.gameObject);
            var value = new Active { Id = data.Id, Entity = entity, View = view,
                Blocked = data.Movement.BlockedPos.HasValue, Contact = contact,
                Remaining = data.Movement.BlockedPos.HasValue ? float.PositiveInfinity
                    : data.Movement.TotalMoveDuration + data.Movement.Info.DelayDestroyTime };
            active.Add(value);
            scope.Dispatch(entity.MyStart);
            if (value.Remaining <= 0f) Remove(value);
            return entity;
        }

        // The owner splits scope.Tick at NextDelay, then advances these original timers.
        public void Advance(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleMoveActionPlayback));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            scope.Dispatch(() => {
                foreach (var value in new List<Active>(active))
                {
                    value.View.Advance(deltaTime);
                    value.Remaining -= deltaTime;
                    if (value.Remaining <= 0f || value.Blocked && !value.View.BlockedPlaybackPending) Remove(value);
                }
            });
        }

        private void OnNativePlayableStarted(GameObject prefab, MyPlayable playable)
        {
            if (disposed || !prefab || prefab.name != "playable_move_blocked"
                || !playable || playable.name != "move"
                || !(playable.TryGetParam("BlockedAction") is GameObject actionObject) || !actionObject) return;
            var value = active.Find(entry => entry.Contact != null && entry.Entity.gameObject == actionObject);
            if (value == null) return;
            BattleBlockedTrackContact.Attach(playable, () => {
                if (disposed || !active.Contains(value) || value.Contact == null) return;
                var callback = value.Contact; value.Contact = null; callback();
            });
        }

        private void Remove(Active value)
        {
            active.Remove(value);
            loader.Remove(value.Id);
        }

        private static IFightActionSkillMoveView MovementView(GameObject root)
        {
            var driver = root.GetComponentInChildren<ECViewDriver>(true);
            var current = driver ? driver.GetView<ECFightActionSkillMoveView>() : null;
            return current != null ? (IFightActionSkillMoveView)current : root.GetComponent<MyFightActionSkillMove>();
        }

        public void Dispose()
        {
            if (disposed) return;
            scope.NativePlayableStarted -= OnNativePlayableStarted;
            foreach (var value in new List<Active>(active)) Remove(value);
            loader.Dispose();
            disposed = true;
        }
    }
}
