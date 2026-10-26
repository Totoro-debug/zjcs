using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13
{
    // Independent native carrier for the three fully decoded official map flags.
    // This is not a Spine runtime or a general skeleton importer.
    public sealed class NativeMap13FlagData : ScriptableObject
    {
        [Serializable] public struct Bone { public string Name; public int Parent; public Vector2 Position; public float Rotation; }
        [Serializable] public struct Vertex { public int First, Count; }
        [Serializable] public struct Influence { public int Bone; public Vector2 Position; public float Weight; }
        [Serializable] public struct RotationTrack { public int Bone; public AnimationCurve Curve; }

        public float Scale, Duration;
        public Bone[] Bones;
        public Vertex[] Vertices;
        public Influence[] Influences;
        public Vector2[] UV;
        public int[] Triangles;
        public Material Material;
        public RuntimeAnimatorController Controller;
        public AnimationClip Clip;
        public RotationTrack[] RotationTracks;
    }
}
