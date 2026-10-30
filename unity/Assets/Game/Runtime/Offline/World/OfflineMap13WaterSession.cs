using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    /// <summary>
    /// Original EC stage/result ordering and ground overlays, with the confirmed
    /// offline implementation of WorldConveyanceComponent.FireTrigger. No reward,
    /// inventory, terminal payment or persistence callbacks are bypassed here.
    /// </summary>
    public sealed class OfflineMap13WaterSession
    {
        private readonly Dictionary<int, Map13WaterRule> rules;
        private readonly Dictionary<int, Map13WaterTerminal> terminals;
        private Dictionary<int, Map13WaterState> states;
        private readonly List<Map13WaterMovement> movements = new List<Map13WaterMovement>();
        public IReadOnlyList<Map13WaterMovement> Movements => movements;
        public IEnumerable<int> PlacementIds => rules.Keys;

        public OfflineMap13WaterSession(Map13WaterDefinition definition, Map13WaterState[] saved = null)
        {
            if (definition == null || definition.MapClassId != 13 || definition.Manifest != "871_156598")
                throw new InvalidDataException("Water rules must belong to the selected Map13 source.");
            rules = definition.Rules.ToDictionary(rule => rule.PlacementId);
            terminals = definition.Terminals.ToDictionary(terminal => terminal.PlacementId);
            states = (saved ?? Array.Empty<Map13WaterState>()).ToDictionary(state => state.PlacementId, Clone);
            if (states.Keys.Any(id => !rules.ContainsKey(id)))
                throw new InvalidDataException("Saved Map13 water state refers to an unknown entity.");
            foreach (Map13WaterRule rule in rules.Values)
            {
                if (rule.Stage == null || rule.InitialState < 0 || rule.InitialState >= rule.Stage.StateInfos.Count)
                    throw new InvalidDataException("Invalid original water stage: " + rule.PlacementId);
                if (!states.ContainsKey(rule.PlacementId))
                {
                    states.Add(rule.PlacementId, new Map13WaterState
                    {
                        PlacementId = rule.PlacementId, State = rule.InitialState, Grid = rule.OriginalGrid,
                        Spawned = rule.InitiallySpawned
                    });
                    EnterState(rule, states[rule.PlacementId]);
                }
                Map13WaterState value = states[rule.PlacementId];
                if (value.State < 0 || value.State >= rule.Stage.StateInfos.Count
                    || value.GroundOperation < -1 || value.GroundOperation >= rule.GroundOperations.Length
                    || (rule.Routes.Length == 0 && (value.NextSegment != 0 || value.Returning))
                    || (rule.Routes.Length > 0 && (value.NextSegment < 0 || value.NextSegment >= rule.Routes.Length)))
                    throw new InvalidDataException("Saved Map13 water state is invalid: " + rule.PlacementId);
                if (rule.Routes.Length > 0)
                {
                    MapGridPoint[] path = rule.Routes[value.NextSegment].OffsetPath;
                    MapGridPoint boundary = path[value.Returning ? path.Length - 1 : 0];
                    if (!value.Grid.Equals(rule.OriginalGrid + boundary))
                        throw new InvalidDataException("Saved platform is outside its route boundary: " + rule.PlacementId);
                }
            }
        }

        public Map13WaterState State(int id) => Clone(states[id]);
        public Map13WaterState[] Save() => states.OrderBy(pair => pair.Key).Select(pair => Clone(pair.Value)).ToArray();

        public bool CanTouch(int id, MapGridPoint actorGrid)
        {
            Map13WaterRule rule = rules[id];
            return states[id].Spawned && rule.InteractiveRange.Any(offset => (states[id].Grid + offset).Equals(actorGrid));
        }

        public bool Touch(int id, MapGridPoint actorGrid, Map13WaterOccupant[] occupants,
            Func<int, MapGridPoint, bool> canMove)
        {
            if (canMove == null) throw new ArgumentNullException(nameof(canMove));
            movements.Clear();
            if (!CanTouch(id, actorGrid)) return false;
            Map13WaterRule rule = rules[id];
            Map13WaterState before = states[id];
            OfficialStageStateInfo stage = rule.Stage.StateInfos[before.State];
            OfficialSubStageStateInfo sub = stage.SubStateList[0];
            if (sub.Behaviour.CompType != 2) return false;
            Map13WaterOccupant[] allOccupants = rules.Values.Where(value => states[value.PlacementId].Spawned)
                .Select(value => new Map13WaterOccupant { PlacementId = value.PlacementId,
                    EntityClassId = value.EntityClassId, Grid = states[value.PlacementId].Grid,
                    HasGridMover = value.HasGridMover }).Concat(occupants ?? Array.Empty<Map13WaterOccupant>()).ToArray();
            foreach (OfficialStageCompCfg condition in sub.Conditions)
            {
                if (condition.CompType != 108)
                    throw new InvalidDataException("Unsupported original water condition: " + condition.CompType);
                // Common.StageConUncoveredEntityComponent.IsConveredByEntity:
                // inspect every target body cell and only the configured classes.
                if (rule.UncoveredEntities.Any(target => CoveredBy(target, rule.UncoveredEntityClasses, allOccupants)))
                    return false;
            }
            Dictionary<int, Map13WaterState> snapshot = states.ToDictionary(pair => pair.Key, pair => Clone(pair.Value));
            try
            {
                foreach (OfficialStageCompCfg result in sub.Results)
                {
                    switch (result.CompType)
                    {
                        case 209:
                            SetState(id, rule.SelfStates[result.CompIndex].State);
                            break;
                        case 217:
                            SetGroundOperation(id, result.CompIndex);
                            break;
                        case 242:
                            foreach (int target in rule.TriggerEntities)
                                if (!MovePlatform(target, allOccupants, canMove))
                                {
                                    states = snapshot;
                                    movements.Clear();
                                    return false;
                                }
                            break;
                        case 243:
                            // Keep duplicates and original order (5328 includes
                            // 5324 twice). Default-state entries use the Lo state.
                            foreach (Map13WaterStateUpdate update in rule.StateUpdates[result.CompIndex].StageDatas)
                                SetState(update.LoEntId, update.UseLoEntDefaultState
                                    ? rules[update.LoEntId].InitialState : update.State);
                            break;
                        case 500: break;
                        default: throw new InvalidDataException("Unsupported original water result: " + result.CompType);
                    }
                }
                if (rule.Stage.AutoNextState) SetState(id, states[id].State + 1);
                return true;
            }
            catch
            {
                states = snapshot;
                movements.Clear();
                throw;
            }
        }

        // Apply only after the caller's original fee/receipt transaction succeeds.
        // Neither terminal operation claims or clears any reward record.
        public void OpenPaidTerminalRoads(int terminalId)
        {
            if (!terminals.TryGetValue(terminalId, out Map13WaterTerminal terminal))
                throw new ArgumentOutOfRangeException(nameof(terminalId));
            foreach (int id in terminal.ExtendRoadLoEntityIds) states[id].Spawned = true;
        }

        public void ResetTerminal(int terminalId)
        {
            if (!terminals.TryGetValue(terminalId, out Map13WaterTerminal terminal))
                throw new ArgumentOutOfRangeException(nameof(terminalId));
            movements.Clear();
            foreach (int id in terminal.NeedResetLoEntityIds)
            {
                Map13WaterRule rule = rules[id];
                Map13WaterState value = states[id];
                value.Grid = rule.OriginalGrid;
                value.Spawned = rule.InitiallySpawned;
                value.NextSegment = 0;
                value.Returning = false;
                value.State = rule.InitialState;
                value.GroundOperation = -1;
                EnterState(rule, value);
            }
        }

        public Map13WaterBody[] Body(int id)
        {
            Map13WaterRule rule = rules[id];
            int index = states[id].GroundOperation;
            if (index < 0 || rule.GroundOperations[index].IsRevertGroundType) return rule.Body;
            Map13WaterBody[] replacements = rule.GroundData[rule.GroundOperations[index].DataIndex].BodyRangeOverrideInfos;
            return rule.Body.Select(body => replacements.FirstOrDefault(value => value.Pos.Equals(body.Pos)) ?? body).ToArray();
        }

        private bool CoveredBy(int id, int[] classes, IEnumerable<Map13WaterOccupant> occupants)
        {
            return Body(id).Any(body => occupants.Any(occupant => occupant.PlacementId != id
                && classes.Contains(occupant.EntityClassId) && (states[id].Grid + body.Pos).Equals(occupant.Grid)));
        }

        private void SetState(int id, int target)
        {
            Map13WaterRule rule = rules[id];
            Map13WaterState value = states[id];
            if (target < 0 || target >= rule.Stage.StateInfos.Count)
                throw new InvalidDataException("Water result references invalid state: " + id + "/" + target);
            if (value.State == target) return;
            int previous = value.State;
            value.State = target;
            // Original StageResultOverrideBodyGroundTypeComponent.OnStateUpdate
            // executes the new EnterResults before the first old substage result.
            EnterState(rule, value);
            OfficialStageCompCfg oldResult = rule.Stage.StateInfos[previous].SubStateList[0].Results
                .FirstOrDefault(result => result.CompType == 217);
            if (oldResult != null) SetGroundOperation(id, oldResult.CompIndex);
        }

        private void EnterState(Map13WaterRule rule, Map13WaterState value)
        {
            OfficialStageCompCfg result = rule.Stage.StateInfos[value.State].EnterResults
                .FirstOrDefault(entry => entry.CompType == 217);
            if (result != null) SetGroundOperation(rule.PlacementId, result.CompIndex);
        }

        private void SetGroundOperation(int id, int index)
        {
            Map13WaterRule rule = rules[id];
            if (index < 0 || index >= rule.GroundOperations.Length
                || rule.GroundOperations[index].DataIndex < 0
                || rule.GroundOperations[index].DataIndex >= rule.GroundData.Length)
                throw new InvalidDataException("Water ground operation is out of range: " + id + "/" + index);
            states[id].GroundOperation = index;
        }

        private bool MovePlatform(int id, Map13WaterOccupant[] occupants, Func<int, MapGridPoint, bool> canMove)
        {
            Map13WaterRule rule = rules[id];
            Map13WaterState value = states[id];
            if (rule.Routes.Length == 0) throw new InvalidDataException("Water trigger has no original route: " + id);
            MapGridPoint[] offsets = rule.Routes[value.NextSegment].OffsetPath;
            MapGridPoint[] path = (value.Returning ? offsets.Reverse() : offsets).Select(offset => rule.OriginalGrid + offset).ToArray();
            if (!path[0].Equals(value.Grid)) throw new InvalidDataException("Saved platform is outside its route boundary: " + id);
            // Common.WorldConveyanceComponent.DoAddInRangeEntity and Goods.offset.
            Map13WaterOccupant[] passengers = occupants.Where(occupant => occupant.PlacementId != id
                && occupant.HasGridMover && Body(id).Any(body => (value.Grid + body.Pos).Equals(occupant.Grid))).ToArray();
            foreach (MapGridPoint point in path.Skip(1))
                if (Body(id).Any(body => !canMove(id, point + body.Pos))) return false;
            movements.Add(new Map13WaterMovement { PlacementId = id, Path = path });
            foreach (Map13WaterOccupant passenger in passengers)
            {
                MapGridPoint offset = new MapGridPoint(passenger.Grid.X - value.Grid.X, passenger.Grid.Y - value.Grid.Y);
                movements.Add(new Map13WaterMovement { PlacementId = passenger.PlacementId,
                    Path = path.Select(point => point + offset).ToArray() });
                if (states.TryGetValue(passenger.PlacementId, out Map13WaterState passengerState))
                    passengerState.Grid = path[path.Length - 1] + offset;
            }
            value.Grid = path[path.Length - 1];
            if (!value.Returning && value.NextSegment == rule.Routes.Length - 1) value.Returning = true;
            else if (value.Returning && value.NextSegment == 0) value.Returning = false;
            else value.NextSegment += value.Returning ? -1 : 1;
            return true;
        }

        private static Map13WaterState Clone(Map13WaterState source) => new Map13WaterState
        {
            PlacementId = source.PlacementId, State = source.State, Grid = source.Grid, Spawned = source.Spawned,
            NextSegment = source.NextSegment, Returning = source.Returning, GroundOperation = source.GroundOperation
        };
    }
}
