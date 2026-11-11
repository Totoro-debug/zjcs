using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    // The executor owns settlement. This module schedules only original damage
    // views at each settled occurrence's exact cast identity, pose and time.
    internal sealed class OfficialBattleDamageReceiptPlayback : IDisposable
    {
        private sealed class Grid : ECGridTransformData
        {
            public Pos2d Position;
            public Direction Facing;
            public bool Mirrored;
            public OfficialBattleSkillEntityPlayback Views;
            public override Pos2d Pos => Position;
            public override Direction Direction => Facing;
            public override bool FlipX => Mirrored;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d position) => Views.GridToPos(position);
            public override Pos2d PositionToGrid(Vector3 position) => Views.PositionToGrid(position);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
        }
        private sealed class Pending
        {
            public float Remaining, Lifetime;
            public OfficialBattleDamageEntityData Data;
            public IFightPlayableOverrideStatusServices Status;
        }
        private sealed class Active
        {
            public EcEntityIdentifier Id;
            public float Remaining;
        }

        private readonly Transform root;
        private readonly MyContainer container;
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly List<Pending> pending = new List<Pending>();
        private readonly List<Active> active = new List<Active>();
        private OfficialBattleDamageEntityPlayback playback;
        private int serial;

        public OfficialBattleDamageReceiptPlayback(Transform root, MyContainer container,
            OfficialBattleSkillEntityPlayback views, OfflineBattleNativePlayableScope scope,
            FightOfficialCombatCatalog catalog)
        { this.root = root; this.container = container; this.views = views; this.scope = scope; this.catalog = catalog; }

        public MyEntity GetEntity(EcEntityIdentifier id) => playback?.GetEntity(id);
        public float NextDelay
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (var value in pending) next = Mathf.Min(next, value.Remaining);
                foreach (var value in active) next = Mathf.Min(next, value.Remaining);
                return next;
            }
        }

        public void Enqueue(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId,
            IReadOnlyDictionary<int, FightBattleDamageEvent> receipts, Direction direction, bool flipX)
        {
            if (receipts == null || cast.DamageEventIds == null || cast.DamageEventIds.Count == 0) return;
            var source = views.GetEntity(skillId).GetComponent<MyFightSkill>().Component.SrcEntityId;
            var status = new OfficialBattleSkillSourceStatusSnapshot(cast.Presentation, source, catalog);
            var seen = new HashSet<int>();
            foreach (int id in cast.DamageEventIds)
            {
                if (!seen.Add(id) || !receipts.TryGetValue(id, out var receipt) || receipt.IsSkillCost
                    || receipt.SourceId != cast.ActorId || receipt.SkillEntityId != cast.SkillEntityId)
                    throw new InvalidOperationException("Damage receipt does not belong to this actual cast: " + id);
                // Legacy receipts keep missing results/positions unknown. Do not
                // infer either from the scene, current HP or surviving statuses.
                if (!receipt.HasDamageTriggerStatus || !receipt.HasDamagePosition) continue;
                if (!catalog.TryGetComponent(receipt.DamageEntityId, "FightDamageComponent", out var config))
                    throw new InvalidOperationException("Original damage configuration is missing: " + receipt.DamageEntityId);
                float lifetime = config.TryGetValue("Duration", out var duration)
                    ? Convert.ToSingle(duration, CultureInfo.InvariantCulture) : .5f;
                if (lifetime < 0f || float.IsNaN(lifetime) || float.IsInfinity(lifetime))
                    throw new InvalidOperationException("Review original damage lifetime: " + receipt.DamageEntityId);
                var position = new Pos2d(receipt.DamageX, receipt.DamageY);
                var grid = catalog.HasComponent(receipt.DamageEntityId, "GridTransformComponent")
                    ? new Grid { Position = position, Facing = direction, Mirrored = flipX, Views = views } : null;
                var result = new DamageResultParameter {
                    DamageValue = receipt.DamageValue, ShieldValue = receipt.ShieldDamage,
                    AbsoluteDamage = receipt.AbsoluteDamage, Pos = position,
                    IsElementAffect = receipt.IsElementAffect, ElementConfrontation = receipt.ElementConfrontation,
                    SkillElementType = (ElementType)receipt.ElementType,
                    ResultType = receipt.IsCure ? DamageResultType.Cure
                        : receipt.HasAbsoluteDamage ? DamageResultType.Damage : DamageResultType.None,
                    DamageTypeData = new DamageTypeData { IsBlock = receipt.Block, IsCrit = receipt.Critical,
                        IsDodge = receipt.Dodge, IsBlinding = receipt.Blinding, IsImmunity = receipt.Immunity,
                        IsBreakResilience = receipt.BreakResilience, IsTrueDamage = receipt.TrueDamage } };
                pending.Add(new Pending { Remaining = Mathf.Max(0f, receipt.Delay - cast.Delay), Lifetime = lifetime,
                    Status = status, Data = new OfficialBattleDamageEntityData(new EcEntityIdentifier(6, ++serial),
                        receipt.DamageEntityId, skillId, new EcEntityIdentifier(1, receipt.TargetId), result,
                        receipt.IsDamageTriggerStatus, views.GridToPos(position), direction.DirToRotation(),
                        new Vector3(flipX ? -1f : 1f, 1f, 1f), grid) });
            }
            DispatchDue();
        }

        public void Advance(float elapsed)
        {
            foreach (var value in pending) value.Remaining -= elapsed;
            foreach (var value in active) value.Remaining -= elapsed;
        }

        public void DispatchDue()
        {
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Remaining <= 0f)
                { playback.Remove(active[index].Id); active.RemoveAt(index); }
            for (int index = 0; index < pending.Count;)
            {
                var value = pending[index];
                if (value.Remaining > 0f) { index++; continue; }
                pending.RemoveAt(index);
                playback ??= new OfficialBattleDamageEntityPlayback(root, container, views, scope);
                playback.Create(value.Data, value.Status);
                active.Add(new Active { Id = value.Data.Id, Remaining = value.Lifetime });
            }
        }

        public void Dispose()
        {
            pending.Clear();
            playback?.Dispose(); playback = null;
            active.Clear();
        }
    }
}
