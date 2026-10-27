using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14
{
    // Independent carrier for the two verified environment models. The source
    // generator rejects constraints, weighted/deformed meshes and other formats.
    public sealed class NativeMap14ButterflyData : ScriptableObject
    {
        public TextAsset Source;
        public string SourceSha256, ModelName;
        public Material Material;
        [NonSerialized] private Model model;

        public Model Load()
        {
            if (model != null) return model;
            if (!Source || !Material) throw new InvalidDataException("Original Map14 butterfly source/material is unbound.");
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(Source.bytes)).Replace("-", "").ToLowerInvariant() != SourceSha256)
                    throw new InvalidDataException("Original Map14 butterfly source hash differs.");
            var payload = JsonUtility.FromJson<Payload>(Source.text);
            if (payload == null || payload.mapClassId != 14 || payload.manifest != "871_156598" || payload.models == null || payload.models.Length != 2)
                throw new InvalidDataException("Original Map14 butterfly source identity differs.");
            foreach (Model value in payload.models)
                if (value.name == ModelName) { model = value; break; }
            if (model == null || model.bones == null || model.bones.Length == 0 || model.skins == null || model.animations == null)
                throw new InvalidDataException("Original Map14 butterfly model is missing: " + ModelName);
            return model;
        }

        [Serializable] private sealed class Payload { public int mapClassId; public string manifest; public Model[] models; }
        [Serializable] public sealed class Model
        {
            public string name, materialPath;
            public float scale;
            public Bone[] bones;
            public Skin[] skins;
            public Clip[] animations;
        }
        [Serializable] public struct Bone
        {
            public string name;
            public int parent;
            public float x, y, rotation, scaleX, scaleY, shearX, shearY;
            public bool onlyTranslation;
        }
        [Serializable] public sealed class Skin
        {
            public string name;
            public Vector2[] vertices, uv;
            public int[] triangles, vertexBones;
        }
        [Serializable] public sealed class Clip { public string name; public float duration; public Track[] tracks; }
        [Serializable] public sealed class Track { public int bone, channel; public Key[] keys; }
        [Serializable] public struct Key { public float time, x, y; public bool stepped; public float[] curve; }
    }
}
