using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusMoveScopeState
    {
        public bool DefinitionResolved;
        public float Rate;
        public int GridItemId;
        public List<int> StatusIds = new List<int>();
        public List<FightBattleGridSummonCell> Cells = new List<FightBattleGridSummonCell>();
        public bool TryAtStart;
        public bool StartChecked;
        public int MaxCount;
        public int TriggeredCount;

        public FightBattleStatusMoveScopeState Copy()
        {
            var result = (FightBattleStatusMoveScopeState)MemberwiseClone();
            result.StatusIds = new List<int>(StatusIds ?? new List<int>());
            result.Cells = new List<FightBattleGridSummonCell>();
            foreach (var cell in Cells ?? new List<FightBattleGridSummonCell>())
                result.Cells.Add(cell == null ? null : new FightBattleGridSummonCell {
                    Position = cell.Position, ScopePosition = cell.ScopePosition,
                    Rate = cell.Rate, Delay = cell.Delay
                });
            return result;
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusMoveScopeState ParseMoveScope(Dictionary<string, object> component)
        {
            if (component == null) return null;
            var result = new FightBattleStatusMoveScopeState {
                DefinitionResolved = true, Rate = Float(component, "Rate", 1f),
                MaxCount = Int(component, "MaxCount", -1), GridItemId = Int(component, "ScopeGridItemId"),
                TryAtStart = Bool(component, "TryAtStart", true),
                StatusIds = new List<int>(IntArray(component, "GridItemStatus"))
            };
            if (component.TryGetValue("Scopes", out object raw) && raw != null)
                foreach (var value in Array(raw, "move scopes"))
                {
                    var cell = Object(value, "move scope");
                    var point = Component(cell, "Scope");
                    result.Cells.Add(new FightBattleGridSummonCell {
                        Position = new FightGridPoint(Int(point, "X"), Int(point, "Y")),
                        Rate = Float(cell, "Rate", 0f)
                    });
                }
            return result;
        }

        internal void RestoreMoveScopeDefinition(FightBattleStatusState status, bool initialize)
        {
            var previous = status.Components?.MoveScope;
            if (previous == null || !previous.DefinitionResolved)
            {
                if (!entities.TryGetValue(status.ClassId, out var components)) return;
                var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
                var definition = ParseMoveScope(Component(resolved, "FightStatusMoveScopeComponent"));
                if (definition != null && previous != null) {
                    definition.StartChecked = previous.StartChecked;
                    definition.TriggeredCount = previous.TriggeredCount;
                }
                if (status.Components == null) status.Components = new FightBattleStatusComponentState();
                status.Components.MoveScope = definition;
            }
            // Restoring an already existing entity never synthesizes its activation event.
            if (!initialize && status.Components?.MoveScope != null)
                status.Components.MoveScope.StartChecked = true;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private static bool IsPendingMoveScopeStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var component = status?.Components?.MoveScope;
            return owner != null && owner.Hp > 0 && !owner.Ended && component != null
                && component.DefinitionResolved && !component.StartChecked
                && status.ForbiddenType == 0 && status.StackCount > 0;
        }

        private int StartMoveScope(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (movementStartContext == null || !IsPendingMoveScopeStart(owner, status)) return 0;
            status.Components.MoveScope.StartChecked = true;
            return status.Components.MoveScope.TryAtStart
                ? ApplyMoveScope(owner, status, new FightGridPoint(owner.X, owner.Y),
                    movementStartContext.StatusStartTime) : 0;
        }

        private int ApplyMoveScope(FightBattleUnitState owner, FightBattleStatusState status,
            FightGridPoint center, float delay)
        {
            var component = status?.Components?.MoveScope;
            if (statusCatalog == null || component == null || !component.DefinitionResolved
                || owner == null || owner.Hp <= 0 || owner.Ended || !owner.Statuses.Contains(status)
                || status.ForbiddenType != 0 || status.StackCount <= 0 || component.GridItemId <= 0
                || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount
                || !RollComponent(component.Rate)) return 0;
            component.TriggeredCount++;
            // This component creates terrain directly; it does not cast the separate
            // round-start skill 11630 or borrow that skill's guaranteed center cell.
            var source = new FightBattleSkillDefinition {
                EntityId = status.ClassId, ItemId = status.ApplyingSkillItemId,
                RootEntityId = status.RootSkillId,
                Rank = status.SourceSkillRank, Level = status.SourceSkillLevel, SubRank = status.SourceSubRank,
                StatusOrigin = FightBattleStatusOrigin.Resolve(status).Copy(),
                BlessPropertyScales = FightStatusPropertyModifiers.Copy(status.BlessPropertyScales)
            };
            var hit = new FightBattleHitDefinition {
                Delay = Math.Max(0f, delay), SummonGridItemClassId = component.GridItemId,
                SummonReferenceType = 1, SummonStatusIds = new List<int>(component.StatusIds),
                Anchor = (int)FightHitAnchor.OnSourceCastPosAIAsTarget
            };
            foreach (var cell in component.Cells)
                if (cell != null)
                    hit.GridSummonCells.Add(new FightBattleGridSummonCell {
                        // WalkScope offsets are world grid offsets, without facing transforms.
                        Position = new FightGridPoint(owner.Direction == 1 ? -cell.Position.X : cell.Position.X,
                            cell.Position.Y), Rate = cell.Rate
                    });
            int before = checkpoint.GridItems.Count;
            SpawnGridItems(owner, source, hit, new List<FightBattleUnitState>(), null, center);
            return checkpoint.GridItems.Count - before;
        }

        private void ApplyMoveScopesForMovement(FightBattleUnitState owner, FightBattleMovementEvent movement)
        {
            if (movementStartContext == null || movement?.Path == null || movement.Path.Count < 2) return;
            float time = Math.Max(0f, movement.Delay);
            for (int index = 1; index < movement.Path.Count; index++)
            {
                var previous = movement.Path[index - 1];
                var point = movement.Path[index];
                if (movement.MoveDuration >= 0f) time += movement.MoveDuration;
                else if (movement.MoveSpeed > 0f) {
                    double x = point.X - previous.X, y = point.Y - previous.Y;
                    time += (float)(Math.Sqrt(x * x + y * y) / movement.MoveSpeed);
                }
                else time += MovementSecondsPerCell;
                if (point.Equals(previous)) continue;
                // A small jump/teleport has one settled landing. Traversed walking
                // cells are separate settled path entries; no airborne cell is sampled.
                foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                    ApplyMoveScope(owner, status, point, time);
            }
        }
    }
}
