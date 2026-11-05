using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Presentation.Combat
{
    // The original follower owns movement and cast pause/return behavior. This
    // adapter supplies settled grid notifications and the current visual facing.
    public sealed class OfficialBattlePetFollowerView : IDisposable
    {
        private sealed class Grid : ECGridTransformData
        {
            public Pos2d Position;
            public Transform FacingTarget;
            public Func<Pos2d, Vector3> ToPosition;
            private readonly HashSet<IGridTransformPosUpdatedEventReceiver> receivers = new HashSet<IGridTransformPosUpdatedEventReceiver>();
            public override Pos2d Pos => Position;
            public override Direction Direction => default;
            public override bool FlipX => FacingTarget.localScale.x < 0;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d value) => ToPosition(value);
            public override Pos2d PositionToGrid(Vector3 value) => throw new NotSupportedException("Pet follower only consumes owner grid positions.");
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) => receivers.Add(value);
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) => receivers.Remove(value);
            public void Refresh(Pos2d position)
            {
                if (Position.Equals(position)) return;
                var previous = Position; Position = position;
                foreach (var receiver in receivers) receiver.GridTransform_OnPosUpdated(this, previous);
            }
        }

        private readonly OfficialBattleLiveMonsterModelView model;
        private readonly GridTransformFollower follower;
        private readonly MyFightRolePet role;
        private readonly Grid grid;
        private readonly MyFightSkillAgent ownerSkills;
        private readonly bool ownsOwnerSkills;
        private bool disposed;
        public Transform Transform => model.Transform;
        public int EntityId { get; }

        public OfficialBattlePetFollowerView(OfficialBattlePetFollowerCatalog.Binding binding,
            FightBattleUnitState owner, Transform ownerRoot, Transform ownerFacing, Transform worldRoot,
            Func<FightGridPoint, Vector3> gridToWorld, OfflineBattleNativePlayableScope scope)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            EntityId = binding.entityId;
            try
            {
                model = OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState {
                    Id = -owner.Id, CharacterType = 5, MonsterId = EntityId, MonsterTemplateId = EntityId
                }, worldRoot, nativeSkillGraphs: true)
                    ?? throw new InvalidOperationException("Original childhood pet model is unavailable: " + EntityId);
                model.Transform.gameObject.SetActive(false);
                model.Transform.gameObject.AddComponent<MyTransform>();
                var petSkills = model.Transform.gameObject.AddComponent<MyFightSkillAgent>();
                ownerSkills = ownerRoot.GetComponent<MyFightSkillAgent>();
                if (!ownerSkills) { ownerSkills = ownerRoot.gameObject.AddComponent<MyFightSkillAgent>(); ownsOwnerSkills = true; }
                follower = model.Transform.gameObject.AddComponent<GridTransformFollower>();
                var modelTransform = model.Transform.Find(binding.modelPath);
                if (!modelTransform) throw new InvalidOperationException("Original follower model transform is unavailable: " + binding.modelPath);
                follower.Configure(binding.moveGridTime, modelTransform, binding.MovePlayable, binding.startDelay,
                    binding.teleportDistance, binding.followPosZ, binding.useLastMovingData);
                follower.MyPlayableManager = scope;
                role = model.Transform.gameObject.AddComponent<MyFightRolePet>();
                grid = new Grid { Position = new Pos2d(owner.X, owner.Y), FacingTarget = ownerFacing,
                    ToPosition = value => worldRoot.InverseTransformPoint(gridToWorld(new FightGridPoint(value.X, value.Y))) };
                model.Transform.gameObject.SetActive(true);
                role.Bind(follower, petSkills, ownerSkills, ownerFacing, grid);
            }
            catch { Dispose(); throw; }
        }

        public void Refresh(FightBattleUnitState owner)
        {
            if (disposed) return;
            grid.Refresh(new Pos2d(owner.X, owner.Y));
            follower.NotifyTargetScaleUpdated();
        }
        public void Tick(float deltaTime)
        {
            // Original MonoBehaviour/SkeletonMecanim updates stop while the
            // original skill graph hides this childhood entity.
            if (disposed || deltaTime <= 0 || !model.Transform.gameObject.activeInHierarchy) return;
            follower.Tick(deltaTime);
            model.Tick(deltaTime);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (role) role.Release();
            model?.Dispose();
            if (ownsOwnerSkills && ownerSkills)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(ownerSkills);
                else UnityEngine.Object.DestroyImmediate(ownerSkills);
            }
        }
    }
}
