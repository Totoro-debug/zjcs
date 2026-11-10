using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Owns native action lifetimes; the shared graph clock remains with the caller.
    internal sealed class OfficialBattleMoveActionPlayback : IMyActionEntityLookup, IDisposable
    {
        private sealed class Active
        {
            public EcEntityIdentifier Id;
            public MyFightActionSkillMove View;
            public float Remaining;
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
                });
        }

        MyEntity IMyActionEntityLookup.GetEntity(EcEntityIdentifier id) => entityLookup(id);
        public MyEntity GetEntity(EcEntityIdentifier id) => loader.GetEntity(id);
        public bool ContainsClass(int classId) => loader.ContainsClass(classId);

        public float NextDelay
        {
            get {
                float next = float.PositiveInfinity;
                foreach (var value in active)
                    next = Mathf.Min(next, Mathf.Max(0f, value.Remaining), value.View.RemainingMoveTime);
                return next;
            }
        }

        public MyEntity Create(OfficialBattleMoveActionData data)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleMoveActionPlayback));
            if (data == null) throw new ArgumentNullException(nameof(data));
            var entity = loader.Create(data);
            var view = entity.GetComponent<MyFightActionSkillMove>();
            view.EntitySlot = loader.EntitySlot;
            var value = new Active { Id = data.Id, View = view,
                Remaining = data.Movement.TotalMoveDuration + data.Movement.Info.DelayDestroyTime };
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
                    if (value.Remaining <= 0f) Remove(value);
                }
            });
        }

        private void Remove(Active value)
        {
            active.Remove(value);
            loader.Remove(value.Id);
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var value in new List<Active>(active)) Remove(value);
            loader.Dispose();
            disposed = true;
        }
    }
}
