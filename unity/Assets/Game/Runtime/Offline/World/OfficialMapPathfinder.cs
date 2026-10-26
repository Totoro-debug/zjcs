using System;
using System.Collections.Generic;

namespace Zjcs.Offline.World
{
    public struct MapGridPoint : IEquatable<MapGridPoint>
    {
        public int X;
        public int Y;

        public MapGridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static MapGridPoint operator +(MapGridPoint left, MapGridPoint right)
        {
            return new MapGridPoint(left.X + right.X, left.Y + right.Y);
        }

        public bool Equals(MapGridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object value) => value is MapGridPoint other && Equals(other);
        public override int GetHashCode() => 31 * X.GetHashCode() + Y.GetHashCode();
    }

    public interface IOfficialMapMovementRules
    {
        bool IsFit(MapGridPoint position);
        bool CanMoveToNext(MapGridPoint from, MapGridPoint to);
    }

    public sealed class OfficialMapPathRequest
    {
        public MapGridPoint Start;
        public MapGridPoint End;
        public int PreferDistance;
        public bool PreciseTarget;
        public int MaxSteps;
        public IReadOnlyList<MapGridPoint> BodyOffsets;
        public IReadOnlyList<MapGridPoint> FirstSteps;
        public Func<MapGridPoint, bool> CanEnd;
    }

    public static class OfficialMapPathfinder
    {
        private static readonly MapGridPoint[] Directions =
        {
            new MapGridPoint(0, -1),
            new MapGridPoint(-1, 0),
            new MapGridPoint(0, 1),
            new MapGridPoint(1, 0)
        };

        private sealed class Node
        {
            public readonly MapGridPoint Position;
            public readonly float Cost;
            public readonly float Estimate;
            public readonly int Steps;
            public readonly Node Previous;

            public Node(MapGridPoint position, float cost, float estimate, int steps, Node previous)
            {
                Position = position;
                Cost = cost;
                Estimate = estimate;
                Steps = steps;
                Previous = previous;
            }
        }

        public static List<MapGridPoint> FindPath(IOfficialMapMovementRules movement, OfficialMapPathRequest request)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.BodyOffsets == null) throw new ArgumentException("Body offsets are required.", nameof(request));

            var path = new List<MapGridPoint>();
            if (request.Start.Equals(request.End)) return path;

            var start = new Node(request.Start, 0f, Estimate(request.Start, request), 0, null);
            Node result = Search(movement, request, start);
            if (result == null) return null;

            for (Node node = result; node != null; node = node.Previous)
                path.Add(node.Position);
            if (path.Count == 1) return new List<MapGridPoint>();
            path.Reverse();
            return path;
        }

        private static Node Search(IOfficialMapMovementRules movement, OfficialMapPathRequest request, Node start)
        {
            var open = new LinkedList<Node>();
            var visited = new HashSet<MapGridPoint> { request.Start };
            var explored = new List<Node> { start };

            if (request.FirstSteps != null)
            {
                foreach (MapGridPoint first in request.FirstSteps)
                {
                    var node = new Node(first, 1f, Estimate(first, request), 1, start);
                    explored.Add(node);
                    Insert(open, node);
                }
            }

            var startingBody = new HashSet<MapGridPoint>();
            foreach (MapGridPoint offset in request.BodyOffsets)
                startingBody.Add(request.Start + offset);

            Node current = start;
            while (true)
            {
                foreach (MapGridPoint direction in Directions)
                {
                    MapGridPoint nextPosition = current.Position + direction;
                    if (visited.Contains(nextPosition) || !movement.CanMoveToNext(current.Position, nextPosition))
                        continue;
                    visited.Add(nextPosition);

                    bool fits = true;
                    foreach (MapGridPoint offset in request.BodyOffsets)
                    {
                        MapGridPoint bodyPosition = nextPosition + offset;
                        if (!startingBody.Contains(bodyPosition) && !movement.IsFit(bodyPosition))
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (!fits) continue;

                    var next = new Node(nextPosition, current.Cost + 1f, Estimate(nextPosition, request), current.Steps + 1, current);
                    explored.Add(next);
                    if (request.MaxSteps <= 0 || next.Steps + 1 < request.MaxSteps)
                        Insert(open, next);
                }

                if (open.Count == 0) break;
                current = open.First.Value;
                open.RemoveFirst();
                if (CanEnd(request, current)) return current;
            }

            Node closest = null;
            float bestEstimate = float.MaxValue;
            foreach (Node node in explored)
            {
                if (node.Estimate < bestEstimate)
                {
                    closest = node;
                    bestEstimate = node.Estimate;
                }
            }
            return closest;
        }

        private static bool CanEnd(OfficialMapPathRequest request, Node node)
        {
            return node.Estimate <= request.PreferDistance || request.CanEnd != null && request.CanEnd(node.Position);
        }

        private static float Estimate(MapGridPoint position, OfficialMapPathRequest request)
        {
            if (request.PreciseTarget)
                return Distance(position, request.End);

            float estimate = float.MaxValue;
            foreach (MapGridPoint offset in request.BodyOffsets)
                estimate = Math.Min(estimate, Distance(position + offset, request.End));
            return estimate;
        }

        private static int Distance(MapGridPoint left, MapGridPoint right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static void Insert(LinkedList<Node> open, Node node)
        {
            for (LinkedListNode<Node> current = open.First; current != null; current = current.Next)
            {
                if (node.Cost + node.Estimate < current.Value.Cost + current.Value.Estimate)
                {
                    open.AddBefore(current, node);
                    return;
                }
            }
            open.AddLast(node);
        }
    }
}
