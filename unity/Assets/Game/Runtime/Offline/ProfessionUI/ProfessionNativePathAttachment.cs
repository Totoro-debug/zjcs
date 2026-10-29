using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Preserve the original weighted control points and source curve lengths.
    // Loading/evaluating these points is separate from a path constraint solver.
    public sealed class ProfessionNativePathAttachment : ProfessionNativeAttachment
    {
        private readonly ProfessionNativeVertexAttachment vertices;
        private readonly float scale;
        public int VertexCount => vertices.VertexCount;
        public bool Closed { get; }
        public bool ConstantSpeed { get; }
        public IReadOnlyList<float> Lengths { get; }

        public ProfessionNativePathAttachment(string name, int slotBone, int boneCount,
            float[] sourceVertices, int vertexCount, float[] lengths, bool closed, bool constantSpeed, float dataScale)
            : base(name, slotBone)
        {
            if (!(dataScale > 0) || float.IsInfinity(dataScale) || lengths == null)
                throw new ArgumentException("Original path scale or curve lengths are invalid.");
            var retained = (float[])lengths.Clone();
            foreach (float length in retained)
                if (float.IsNaN(length) || float.IsInfinity(length) || length < 0)
                    throw new ArgumentException("Original path curve length is invalid.");
            vertices = new ProfessionNativeVertexAttachment(sourceVertices, vertexCount, slotBone, boneCount);
            Lengths = Array.AsReadOnly(retained); Closed = closed; ConstantSpeed = constantSpeed; scale = dataScale;
        }

        public void ComputeVertices(ProfessionNativeBonePose pose, Vector2[] output)
        {
            vertices.Evaluate(pose, output);
            for (int i = 0; i < output.Length; i++) output[i] *= scale;
        }
    }
}
