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
            public FightBattleDamageEvent Receipt;
            public OfficialBattleDamageEntityData Data;
            public IFightPlayableOverrideStatusServices Status;
            public bool AwaitStatus;
        }
        private sealed class Active
        {
            public EcEntityIdentifier Id;
            public float Remaining;
            public FightBattleDamageEvent Receipt;
            public OfficialBattleDamageEntityData Data;
            public bool AwaitStatus;
            public GameObject Target, Source;
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
        internal static bool CanCreate(FightBattleDamageEvent receipt) => receipt != null
            && !receipt.IsSkillCost && receipt.HasDamageTriggerStatus
            && receipt.HasDamagePosition && receipt.DamageEntityId != 0;
        public float NextDelay
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (var value in pending) next = Mathf.Min(next, value.Remaining);
                foreach (var value in active) if (!value.AwaitStatus) next = Mathf.Min(next, value.Remaining);
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
                QueueReceipt(skillId, receipt, direction, flipX, status,
                    Mathf.Max(0f, receipt.Delay - cast.Delay));
            }
            DispatchDue();
        }

        // Called while the executor is paused immediately after the actual hit.
        public bool EnqueueImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId,
            FightBattleDamageEvent receipt, Direction direction, bool flipX)
        {
            if (!CanCreate(receipt)) return false;
            var source = views.GetEntity(skillId).GetComponent<MyFightSkill>().Component.SrcEntityId;
            var status = new OfficialBattleSkillSourceStatusSnapshot(cast.Presentation, source, catalog);
            bool queued = QueueReceipt(skillId, receipt, direction, flipX, status, 0f);
            DispatchDue();
            return queued;
        }

        // Status resolution can itself start a child skill. Show the known hit now,
        // retaining its native damage entity until the remaining status result arrives.
        public bool EnqueueUnresolvedImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId,
            FightBattleDamageEvent receipt, Direction direction, bool flipX)
        {
            if (receipt == null || receipt.HasDamageTriggerStatus || receipt.IsSkillCost
                || !receipt.HasDamagePosition || receipt.DamageEntityId == 0) return false;
            var source = views.GetEntity(skillId).GetComponent<MyFightSkill>().Component.SrcEntityId;
            var status = new OfficialBattleSkillSourceStatusSnapshot(cast.Presentation, source, catalog);
            bool queued = QueueReceipt(skillId, receipt, direction, flipX, status, 0f, true);
            DispatchDue();
            return queued;
        }

        public bool ResolveImpactStatus(FightBattleDamageEvent receipt)
        {
            if (receipt == null || !receipt.HasDamageTriggerStatus) return false;
            var value = active.Find(item => item.AwaitStatus && ReferenceEquals(item.Receipt, receipt));
            if (value == null) return false;
            var entity = playback?.GetEntity(value.Id);
            var view = entity ? entity.GetComponent<MyFightDamage>() : null;
            if (!view) return false;
            value.Data.ResolveTriggeredStatus(receipt.IsDamageTriggerStatus);
            if (receipt.IsDamageTriggerStatus && view.myTriggerStatusPlayable && value.Target && value.Source)
            {
                var parameters = new Dictionary<string, object> { { "Damage", view.gameObject } };
                scope.Dispatch(() => view.mMyPlayableManager.Play(view.myTriggerStatusPlayable,
                    value.Target, value.Source, 1f, null, parameters));
            }
            value.AwaitStatus = false;
            value.Remaining = Mathf.Max(0f, value.Remaining);
            return true;
        }

        private bool QueueReceipt(EcEntityIdentifier skillId, FightBattleDamageEvent receipt,
            Direction direction, bool flipX, IFightPlayableOverrideStatusServices status, float remaining,
            bool awaitStatus = false)
        {
            // Legacy receipts keep missing results/positions unknown. Do not
            // infer either from the scene, current HP or surviving statuses.
            // Original trigger-only hits (for example 13522) have no
            // damage entity. Their hit graph and child skill own playback.
            if (!awaitStatus && !CanCreate(receipt)) return false;
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
            pending.Add(new Pending { Receipt = receipt, Remaining = remaining, Lifetime = lifetime, AwaitStatus = awaitStatus,
                Status = status, Data = new OfficialBattleDamageEntityData(new EcEntityIdentifier(6, ++serial),
                    receipt.DamageEntityId, skillId, new EcEntityIdentifier(1, receipt.TargetId), result,
                    receipt.IsDamageTriggerStatus, views.GridToPos(position), direction.DirToRotation(),
                    new Vector3(flipX ? -1f : 1f, 1f, 1f), grid) });
            return true;
        }

        public void Advance(float elapsed)
        {
            foreach (var value in pending) value.Remaining -= elapsed;
            foreach (var value in active) value.Remaining -= elapsed;
        }

        public void DispatchDue()
        {
            for (int index = active.Count - 1; index >= 0; index--)
                if (!active[index].AwaitStatus && active[index].Remaining <= 0f)
                { playback.Remove(active[index].Id); active.RemoveAt(index); }
            for (int index = 0; index < pending.Count;)
            {
                var value = pending[index];
                if (value.Remaining > 0f) { index++; continue; }
                pending.RemoveAt(index);
                views.PrepareDamageUITarget((int)value.Data.GetComponent<FightDamageComponent>().TargetId.EntityId);
                playback ??= new OfficialBattleDamageEntityPlayback(root, container, views, scope);
                var damageState = value.Data.GetComponent<FightDamageComponent>();
                var target = views.GetEntity(damageState.TargetId);
                var skill = views.GetEntity(damageState.SkillId);
                var source = skill ? skill.GetComponent<MyFightSkill>()?.Source : null;
                playback.Create(value.Data, value.Status, value.Receipt.SkillItemId);
                active.Add(new Active { Id = value.Data.Id, Remaining = value.Lifetime, Receipt = value.Receipt,
                    Data = value.Data, AwaitStatus = value.AwaitStatus,
                    Target = target ? target.gameObject : null, Source = source ? source.gameObject : null });
                views.DamageStarted(value.Receipt);
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
