using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent linear blend of the original bone-local vertex data.
    // Source weights are retained in order and are never normalized.
    public sealed class ProfessionNativeVertexAttachment
    {
        private struct Influence { public int Bone; public float X, Y, Weight; }
        private readonly Influence[] influences;
        private readonly int[] starts;
        private readonly int boneCount;
        public int VertexCount => starts.Length - 1;
        public int InfluenceCount => influences.Length;
        public bool Weighted { get; }

        public ProfessionNativeVertexAttachment(float[] vertices, int vertexCount, int slotBone, int boneCount)
        {
            if (vertices == null || vertexCount <= 0 || boneCount <= 0 || slotBone < 0 || slotBone >= boneCount)
                throw new ArgumentException("Original vertex layout or slot bone is invalid.");
            this.boneCount = boneCount;
            Weighted = vertices.Length != checked(vertexCount * 2);
            starts = new int[checked(vertexCount + 1)];
            var decoded = new List<Influence>(); int cursor = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                starts[i] = decoded.Count;
                int count = Weighted ? Integer(Read(vertices, ref cursor), "influence count") : 1;
                if (count <= 0 || count > (vertices.Length - cursor) / (Weighted ? 4 : 2))
                    throw new ArgumentException("Original vertex influence count exceeds its data.");
                for (int j = 0; j < count; j++)
                {
                    int bone = Weighted ? Integer(Read(vertices, ref cursor), "bone index") : slotBone;
                    float x = Read(vertices, ref cursor), y = Read(vertices, ref cursor), weight = Weighted ? Read(vertices, ref cursor) : 1;
                    if (bone < 0 || bone >= boneCount || weight < 0 || weight > 1)
                        throw new ArgumentException("Original vertex bone or weight is invalid.");
                    decoded.Add(new Influence { Bone = bone, X = x, Y = y, Weight = weight });
                }
            }
            starts[vertexCount] = decoded.Count;
            if (cursor != vertices.Length) throw new ArgumentException("Original vertex data has trailing values.");
            influences = decoded.ToArray();
        }
        public void Evaluate(ProfessionNativeBonePose pose, Vector2[] output)
        {
            if (pose == null || pose.Count != boneCount || output == null || output.Length != VertexCount)
                throw new ArgumentException("Original vertex output or bone layout differs.");
            for (int i = 0; i < VertexCount; i++)
            {
                Vector2 sum = Vector2.zero;
                for (int j = starts[i]; j < starts[i + 1]; j++)
                {
                    Influence influence = influences[j];
                    if (influence.Weight == 0) continue;
                    if (!pose.IsActive(influence.Bone)) throw new InvalidOperationException("A selected vertex requires an inactive original skin bone.");
                    Matrix4x4 m = pose.World(influence.Bone);
                    sum.x += (m.m00 * influence.X + m.m01 * influence.Y + m.m03) * influence.Weight;
                    sum.y += (m.m10 * influence.X + m.m11 * influence.Y + m.m13) * influence.Weight;
                }
                output[i] = sum;
            }
        }
        private static float Read(float[] source, ref int cursor)
        {
            if (cursor >= source.Length) throw new ArgumentException("Original vertex data is truncated.");
            float value = source[cursor++];
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original vertex values must be finite.");
            return value;
        }
        private static int Integer(float value, string label)
        {
            if (value < 0 || value >= int.MaxValue || value != Math.Floor(value)) throw new ArgumentException("Original vertex " + label + " is not an integer.");
            return (int)value;
        }
    }
}
