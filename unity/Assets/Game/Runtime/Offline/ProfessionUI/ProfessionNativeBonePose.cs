using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent planar affine evaluation using Unity matrices and weighted
    // curves. It reads source transforms; it contains no skeleton SDK code.
    // Constraints and attachment geometry are separate stages.
    public sealed class ProfessionNativeBonePose
    {
        [Serializable]
        public sealed class Bone
        {
            public string name;
            public int parent = -1;
            public bool skinRequired;
            public int inherit; // normal, translation, no rotation/reflection, no scale, no scale/reflection.
            public float x, y, rotation, scaleX = 1, scaleY = 1, shearX, shearY;
        }

        public sealed class Track
        {
            public readonly int Bone, Channel;
            public readonly ProfessionNativeCurve Curve;
            // Channels: rotation, translation X/Y, scale X/Y.
            public Track(int bone, int channel, ProfessionNativeCurve curve)
            { Bone = bone; Channel = channel; Curve = curve ?? throw new ArgumentNullException(nameof(curve)); }
        }

        private readonly Bone[] source;
        private readonly bool[] active;
        private readonly Matrix4x4[] world;
        private readonly Vector2[] position, scale;
        private readonly float[] rotation;
        public int Count => source.Length;
        public bool IsActive(int bone) => active[bone];
        public Matrix4x4 World(int bone) => world[bone];
        public Vector2 LocalPosition(int bone) => position[bone];
        public Vector2 LocalScale(int bone) => scale[bone];
        public float LocalRotation(int bone) => rotation[bone];

        public ProfessionNativeBonePose(Bone[] bones)
        {
            if (bones == null || bones.Length == 0) throw new ArgumentException("Original bone hierarchy is empty.");
            source = new Bone[bones.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i] ?? throw new ArgumentException("Original bone is null.");
                if (string.IsNullOrEmpty(b.name) || !names.Add(b.name) || b.parent < -1 || b.parent >= i || b.inherit < 0 || b.inherit > 4)
                    throw new ArgumentException("Original bone name, parent order or inheritance mode is invalid.");
                Finite(b.x); Finite(b.y); Finite(b.rotation); Finite(b.scaleX); Finite(b.scaleY); Finite(b.shearX); Finite(b.shearY);
                source[i] = new Bone { name = b.name, parent = b.parent, skinRequired = b.skinRequired, inherit = b.inherit,
                    x = b.x, y = b.y, rotation = b.rotation, scaleX = b.scaleX, scaleY = b.scaleY, shearX = b.shearX, shearY = b.shearY };
            }
            active = new bool[bones.Length]; world = new Matrix4x4[bones.Length];
            position = new Vector2[bones.Length]; scale = new Vector2[bones.Length]; rotation = new float[bones.Length];
            SetSkinBones(Array.Empty<int>());
            Reset();
        }

        public void SetSkinBones(IReadOnlyList<int> skinBones)
        {
            if (skinBones == null) throw new ArgumentNullException(nameof(skinBones));
            for (int i = 0; i < source.Length; i++) active[i] = !source[i].skinRequired;
            for (int i = 0; i < skinBones.Count; i++)
            {
                int bone = skinBones[i];
                if (bone < 0 || bone >= source.Length) throw new ArgumentOutOfRangeException(nameof(skinBones));
                while (bone >= 0) { active[bone] = true; bone = source[bone].parent; }
            }
        }

        public void Reset()
        {
            for (int i = 0; i < source.Length; i++)
            {
                Bone b = source[i]; position[i] = new Vector2(b.x, b.y);
                scale[i] = new Vector2(b.scaleX, b.scaleY); rotation[i] = b.rotation;
            }
        }

        public void Apply(IReadOnlyList<Track> tracks, float time)
        {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            Finite(time);
            Reset();
            foreach (Track track in tracks)
            {
                if (track == null || track.Bone < 0 || track.Bone >= source.Length || track.Channel < 0 || track.Channel > 4)
                    throw new ArgumentException("Original bone timeline binding is invalid.");
                int i = track.Bone;
                if (!active[i] || !track.Curve.TryEvaluate(time, out float value)) continue;
                Bone b = source[i];
                switch (track.Channel)
                {
                    case 0: rotation[i] = b.rotation + value; break;
                    case 1: position[i].x = b.x + value; break;
                    case 2: position[i].y = b.y + value; break;
                    case 3: scale[i].x = b.scaleX * value; break;
                    case 4: scale[i].y = b.scaleY * value; break;
                }
            }
        }

        public void UpdateWorld()
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (!active[i]) { world[i] = Matrix4x4.zero; continue; }
                Bone b = source[i];
                Matrix4x4 parent = b.parent < 0 ? Matrix4x4.identity : world[b.parent];
                Vector3 origin = parent.MultiplyPoint3x4(position[i]);
                Matrix4x4 basis;
                if (b.parent < 0 || b.inherit == 1)
                    basis = Axes(rotation[i], b.shearX, b.shearY, scale[i]);
                else if (b.inherit == 0)
                    basis = parent * Axes(rotation[i], b.shearX, b.shearY, scale[i]);
                else
                    throw new NotSupportedException("The original selective rotation/scale/reflection inheritance needs its own verified native binding.");
                basis.m03 = origin.x; basis.m13 = origin.y; world[i] = basis;
            }
        }

        private static Matrix4x4 Axes(float rotation, float shearX, float shearY, Vector2 scale)
        {
            float x = (rotation + shearX) * Mathf.Deg2Rad, y = (rotation + 90 + shearY) * Mathf.Deg2Rad;
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.m00 = Mathf.Cos(x) * scale.x; matrix.m10 = Mathf.Sin(x) * scale.x;
            matrix.m01 = Mathf.Cos(y) * scale.y; matrix.m11 = Mathf.Sin(y) * scale.y;
            return matrix;
        }

        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original bone inputs must be finite."); }
    }
}
