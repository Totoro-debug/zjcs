using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Home
{
    // Headless source-project check before a migrated mesh reaches CanvasRenderer.
    public static class OfficialUiMeshValidation
    {
        [Serializable] private sealed class Row
        {
            public string guid, vertexHash, indexHash;
            public int vertices, indices;
        }
        [Serializable] private sealed class Fixture { public Row[] meshes; public string result; }
        [Serializable] private sealed class Result { public bool success; public string message; }
        public static void Validate()
        {
            string fixturePath = Environment.GetEnvironmentVariable("ZJCS_UI_MESH_FIXTURE");
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(fixturePath));
            var result = new Result();
            try
            {
                foreach (var row in fixture.meshes)
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(row.guid));
                    if (!mesh || mesh.vertexCount != row.vertices || mesh.triangles.Length != row.indices)
                        throw new InvalidOperationException("Original UI mesh vertex/index counts differ.");
                    using var vertices = new MemoryStream();
                    using var indices = new MemoryStream();
                    using var vertexWriter = new BinaryWriter(vertices);
                    using var indexWriter = new BinaryWriter(indices);
                    var positions = mesh.vertices; var uv = mesh.uv;
                    for (int index = 0; index < positions.Length; index++)
                    { vertexWriter.Write(positions[index].x); vertexWriter.Write(positions[index].y); vertexWriter.Write(positions[index].z); vertexWriter.Write(uv[index].x); vertexWriter.Write(uv[index].y); }
                    foreach (int index in mesh.triangles) indexWriter.Write((ushort)index);
                    if (Hash(vertices.ToArray()) != row.vertexHash || Hash(indices.ToArray()) != row.indexHash)
                        throw new InvalidOperationException("Unity UI mesh streams differ from original packed samples.");
                }
                result.success = true;
                result.message = "Native UI mesh streams match original decoded samples.";
            }
            catch (Exception error) { result.message = error.ToString(); Debug.LogException(error); }
            File.WriteAllText(fixture.result, JsonUtility.ToJson(result, true));
            if (!result.success) throw new InvalidOperationException(result.message);
        }
        private static string Hash(byte[] bytes)
        { using var hash = SHA256.Create(); return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
    }
}
