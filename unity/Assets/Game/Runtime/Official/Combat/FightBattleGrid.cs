using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public struct FightGridPoint : IEquatable<FightGridPoint>
    {
        public int X;
        public int Y;

        public FightGridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static FightGridPoint operator +(FightGridPoint left, FightGridPoint right)
        {
            return new FightGridPoint(left.X + right.X, left.Y + right.Y);
        }

        public static FightGridPoint operator -(FightGridPoint left, FightGridPoint right)
        {
            return new FightGridPoint(left.X - right.X, left.Y - right.Y);
        }

        public bool Equals(FightGridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object value) => value is FightGridPoint other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
    }

    [Serializable]
    public sealed class FightBattleGridCell
    {
        public int X;
        public int Y;
        public int GroundType;
        public int Height;
    }

    [Serializable]
    public sealed class FightBattleGrid
    {
        private static readonly FightGridPoint[] Directions =
        {
            new FightGridPoint(0, -1),
            new FightGridPoint(-1, 0),
            new FightGridPoint(0, 1),
            new FightGridPoint(1, 0)
        };

        [Serializable]
        private sealed class Node
        {
            public FightGridPoint Position;
            public float Cost;
            public float Estimate;
            public int Steps;
            public Node Previous;
        }

        public int Width;
        public int Height;
        public float OriginX;
        public float OriginY;
        public float OriginZ;
        public int DefaultGroundType = 2;
        public List<FightBattleGridCell> Cells = new List<FightBattleGridCell>();
        public List<FightGridPoint> BlockedPositions = new List<FightGridPoint>();
        public List<FightGridTransition> BlockedTransitions = new List<FightGridTransition>();

        public bool Contains(FightGridPoint point)
        {
            return point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;
        }

        public int GroundAt(FightGridPoint point)
        {
            if (!Contains(point)) return -1;
            if (Cells != null)
                for (int i = 0; i < Cells.Count; i++)
                {
                    FightBattleGridCell cell = Cells[i];
                    if (cell != null && cell.X == point.X && cell.Y == point.Y)
                        return cell.GroundType;
                }
            return DefaultGroundType;
        }

        public bool CanStand(FightGridPoint point, int moveMask, ISet<FightGridPoint> occupied)
        {
            int ground = GroundAt(point);
            if (ground < 0 || ground >= 31 || (moveMask & (1 << ground)) == 0)
                return false;
            if (BlockedPositions != null && BlockedPositions.Contains(point)) return false;
            return occupied == null || !occupied.Contains(point);
        }

        public List<FightGridPoint> FindPath(FightGridPoint start, FightGridPoint end,
            int moveMask, int maxStep, int preferDistance, bool preciseTarget,
            IReadOnlyList<FightGridPoint> bodyOffsets, ISet<FightGridPoint> occupied,
            IReadOnlyList<FightGridPoint> firstStepPositions = null)
        {
            var path = new List<FightGridPoint>();
            if (!Contains(start) || !Contains(end)) return null;
            if (start.Equals(end)) return path;
            if (bodyOffsets == null || bodyOffsets.Count == 0)
                bodyOffsets = new[] { new FightGridPoint(0, 0) };

            var startNode = new Node
            {
                Position = start,
                Estimate = Estimate(start, end, preciseTarget, bodyOffsets)
            };
            var open = new LinkedList<Node>();
            var explored = new List<Node> { startNode };
            var visited = new HashSet<FightGridPoint> { start };
            var startBodyPositions = new HashSet<FightGridPoint>();
            foreach (FightGridPoint offset in bodyOffsets) startBodyPositions.Add(start + offset);

            if (firstStepPositions != null)
                foreach (FightGridPoint first in firstStepPositions)
                {
                    Node node = new Node
                    {
                        Position = first,
                        Cost = 1f,
                        Estimate = Estimate(first, end, preciseTarget, bodyOffsets),
                        Steps = 1,
                        Previous = startNode
                    };
                    explored.Add(node);
                    Insert(open, node);
                }

            Node current = startNode;
            bool expandCurrent = true;
            while (expandCurrent || open.Count > 0)
            {
                if (expandCurrent)
                {
                    foreach (FightGridPoint direction in Directions)
                    {
                        FightGridPoint next = current.Position + direction;
                        if (visited.Contains(next) || IsTransitionBlocked(current.Position, next)) continue;
                        visited.Add(next);
                        bool canFit = true;
                        foreach (FightGridPoint offset in bodyOffsets)
                        {
                            FightGridPoint bodyPosition = next + offset;
                            if (!startBodyPositions.Contains(bodyPosition)
                                && !CanStand(bodyPosition, moveMask, occupied))
                            {
                                canFit = false;
                                break;
                            }
                        }
                        if (!canFit) continue;

                        var node = new Node
                        {
                            Position = next,
                            Cost = current.Cost + 1f,
                            Estimate = Estimate(next, end, preciseTarget, bodyOffsets),
                            Steps = current.Steps + 1,
                            Previous = current
                        };
                        explored.Add(node);
                        if (maxStep <= 0 || node.Steps + 1 < maxStep)
                            Insert(open, node);
                    }
                }

                expandCurrent = false;
                if (open.Count == 0) break;
                current = open.First.Value;
                open.RemoveFirst();
                if (current.Estimate <= preferDistance)
                    return BuildPath(current);
                expandCurrent = true;
            }

            Node best = null;
            float bestEstimate = float.MaxValue;
            foreach (Node node in explored)
                if (node.Estimate < bestEstimate)
                {
                    best = node;
                    bestEstimate = node.Estimate;
                }
            return best == null ? null : BuildPath(best);
        }

        private bool IsTransitionBlocked(FightGridPoint from, FightGridPoint to)
        {
            if (BlockedTransitions == null) return false;
            for (int i = 0; i < BlockedTransitions.Count; i++)
            {
                FightGridTransition edge = BlockedTransitions[i];
                if (edge != null && ((edge.From.Equals(from) && edge.To.Equals(to))
                    || (edge.From.Equals(to) && edge.To.Equals(from)))) return true;
            }
            return false;
        }

        private static float Estimate(FightGridPoint position, FightGridPoint target,
            bool preciseTarget, IReadOnlyList<FightGridPoint> bodyOffsets)
        {
            if (preciseTarget) return Distance(position, target);
            float result = float.MaxValue;
            foreach (FightGridPoint offset in bodyOffsets)
                result = Math.Min(result, Distance(position + offset, target));
            return result;
        }

        private static int Distance(FightGridPoint left, FightGridPoint right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static void Insert(LinkedList<Node> open, Node node)
        {
            for (LinkedListNode<Node> entry = open.First; entry != null; entry = entry.Next)
                if (node.Cost + node.Estimate < entry.Value.Cost + entry.Value.Estimate)
                {
                    open.AddBefore(entry, node);
                    return;
                }
            open.AddLast(node);
        }

        private static List<FightGridPoint> BuildPath(Node node)
        {
            var path = new List<FightGridPoint>();
            for (Node current = node; current != null; current = current.Previous)
                path.Add(current.Position);
            if (path.Count == 1) return new List<FightGridPoint>();
            path.Reverse();
            return path;
        }
    }

    [Serializable]
    public sealed class FightGridTransition
    {
        public FightGridPoint From;
        public FightGridPoint To;
    }
}
