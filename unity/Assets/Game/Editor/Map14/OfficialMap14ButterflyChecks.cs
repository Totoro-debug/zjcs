using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14;

namespace Zjcs.Editor.Map14
{
    // Cross-check the native C# projection against independent double-precision
    // evaluation of the original decoded source. This is not a runtime comparison.
    public static class OfficialMap14ButterflyChecks
    {
        [Serializable] private sealed class Check { public string animation, skin; public float time; public Vector2[] vertices; }
        [Serializable] private sealed class Model { public string name; public Check[] checks; }
        [Serializable] private sealed class Source { public int mapClassId; public string manifest; public Model[] models; }

        public static void Validate()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Native butterfly checks require Play Mode.");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(OfficialMap14ButterflyTools.Folder + "reference-checks.json"));
            if (source.mapClassId != 14 || source.manifest != "871_156598") throw new InvalidDataException("Butterfly reference identity differs.");
            int samples = 0, points = 0;
            foreach (var model in source.models)
            {
                var asset = AssetDatabase.LoadAssetAtPath<NativeMap14ButterflyData>(OfficialMap14ButterflyTools.Folder + model.name + "-data.asset");
                foreach (var group in model.checks.GroupBy(c => c.skin + ":" + c.animation))
                {
                    var first = group.First();
                    var host = new GameObject("Original butterfly curve fixture"); host.SetActive(false);
                    host.AddComponent<MeshFilter>(); host.AddComponent<MeshRenderer>().sharedMaterial = asset.Material;
                    var player = host.AddComponent<NativeMap14ButterflyMesh>();
                    player.skeletonDataAsset = asset; player.initialSkinName = first.skin;
                    player.loop = true; player.updateTiming = 1; player.updateWhenInvisible = 3;
                    var serialized = new SerializedObject(player);
                    serialized.FindProperty("_animationName").stringValue = first.animation;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    host.SetActive(true);
                    foreach (var check in group)
                    {
                        player.Sample(check.time);
                        Vector3[] actual = player.DeformedMesh.vertices;
                        if (actual.Length != check.vertices.Length) throw new InvalidDataException("Original butterfly vertex count differs.");
                        for (int i = 0; i < actual.Length; i++)
                        {
                            if (Vector2.Distance(actual[i], check.vertices[i]) > .0002f)
                                throw new InvalidDataException("Original butterfly affine/curve projection differs: " + model.name + ":" +
                                    check.skin + ":" + check.animation + ":" + check.time + ":" + i + " actual=" + actual[i] + " expected=" + check.vertices[i]);
                            points++;
                        }
                        samples++;
                    }
                    UnityEngine.Object.Destroy(host);
                }
            }
            Debug.Log("Map14 native butterfly source projection passed: " + samples + " poses, " + points + " vertices, every original animation/skin, key boundaries and curve interiors. No Spine runtime comparison was performed.");
        }
    }
}
