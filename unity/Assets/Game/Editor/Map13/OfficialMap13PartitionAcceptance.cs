using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Exercises native chunk lifecycle in Play Mode. This is not a map/UI acceptance test.
    public static class OfficialMap13PartitionAcceptance
    {
        private const string Key = "Zjcs.Map13.PartitionExecution";
        private static bool started;

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 partition execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 partition test runner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 partition Play Mode passed: 200 original chunks, asynchronous local handles, inactive/active/clear/reload/remove and removal during pending load. Native map rendering, world-context bindings, UI and saves remain unverified.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            var cameraObject = new GameObject("Partition test camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-terrain-catalog");
            Require(catalog != null, "Missing native catalog");
            var data = catalog.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            var chunks = new List<PartitionData.Chunk>(data.TileChunks);
            foreach (var region in data.RegionDatas)
                if (region.LayerDatas != null)
                    foreach (var layer in region.LayerDatas)
                        if (layer?.Tiles != null) chunks.AddRange(layer.Tiles);
            Require(chunks.Count == 200 && chunks.Select(chunk => chunk.Res).Distinct().Count() == 200, "Original partition tile references differ");
            int full = 0, partial = 0, offset = 0;
            foreach (var record in chunks)
            {
                var chunk = catalog.Load<TileChunkData>(record.Res);
                var host = new GameObject("Partition lifecycle fixture");
                host.SetActive(false);
                var maps = new Dictionary<string, Tilemap>();
                foreach (var mapData in chunk.TilemapDatas)
                {
                    var gridHost = new GameObject(mapData.Name + " grid");
                    gridHost.transform.SetParent(host.transform, false);
                    gridHost.AddComponent<Grid>().cellSize = new Vector3(mapData.CellSize.x, mapData.CellSize.y, 1);
                    var mapHost = new GameObject(mapData.Name);
                    mapHost.transform.SetParent(gridHost.transform, false);
                    maps.Add(mapData.Name, mapHost.AddComponent<Tilemap>());
                }
                var resources = host.AddComponent<NativeMap13ResourceLoader>();
                resources.Catalog = catalog;
                var loader = host.AddComponent<PartitionLoader>();
                loader.ResourceLoader = resources;
                loader.ServiceProvider = resources;
                loader.IsCameraReady = () => true;
                loader.IsOtherGameRunning = () => false;
                loader.SetIgnoreCamera(true);
                host.SetActive(true);
                loader.Initialize(data.MinX, data.MaxX, data.MinY, data.MaxY, data.CellSizes, data.ChunkSize);
                loader.AddAsset(record.Pos, record.Id, record.Res, ChunkAssetType.Tile, ChunkRuntimeState.Inactive);
                loader.LoadVicinity(record.Pos.x, record.Pos.y);
                CheckTiles(chunk, maps, false);
                yield return null;
                yield return null;
                Require(loader.IsPosReady(new Vector3(record.Pos.x * data.ChunkSize.x, record.Pos.y * data.ChunkSize.y)), "Original local handle did not complete: " + record.Res);
                CheckTiles(chunk, maps, false);
                loader.ChangeAssetRuntimeState(record.Id, ChunkRuntimeState.Active);
                CheckTiles(chunk, maps, true);
                loader.ChangeAssetRuntimeState(record.Id, ChunkRuntimeState.Inactive);
                CheckTiles(chunk, maps, false);
                loader.ChangeAssetRuntimeState(record.Id, ChunkRuntimeState.Active);
                CheckTiles(chunk, maps, true);
                loader.RemoveAsset(record.Pos, record.Id, record.Res, ChunkAssetType.Tile);
                CheckTiles(chunk, maps, false);
                // The source handles removal after scheduling but before asynchronous completion.
                loader.AddAsset(record.Pos, record.Id, record.Res, ChunkAssetType.Tile, ChunkRuntimeState.Active);
                loader.RemoveAsset(record.Pos, record.Id, record.Res, ChunkAssetType.Tile);
                yield return null;
                yield return null;
                CheckTiles(chunk, maps, false);
                UnityEngine.Object.Destroy(host);
                if (chunk.IsPartial) partial++; else full++;
                offset += chunk.TilemapDatas.Count(map => map.HasOffset);
            }
            Debug.Log($"Map13 original chunk coverage: {full} full, {partial} partial, {offset} tilemap entries with offsets.");
            Require(full == 200 && partial == 0 && offset > 0, "Original Map13 chunk classification differs");
        }

        private static void CheckTiles(TileChunkData chunk, Dictionary<string, Tilemap> maps, bool populated)
        {
            foreach (var mapData in chunk.TilemapDatas)
            {
                var map = maps[mapData.Name];
                for (int index = 0; index < mapData.Tiles.Length; index++)
                {
                    var pair = mapData.Tiles[index];
                    Vector3Int position = chunk.GetPos(pair.Index, mapData.CellSize);
                    Require(map.GetTile(position) == (populated ? chunk.Tiles[pair.TileIndex] : null), "Original tile binding differs: " + chunk.name + "/" + mapData.Name + "/" + position);
                    if (populated && mapData.HasOffset && mapData.MatrixIndexes[index] >= 0)
                    {
                        Matrix4x4 expected = chunk.Matrices[mapData.MatrixIndexes[index]];
                        Matrix4x4 actual = map.GetTransformMatrix(position);
                        for (int field = 0; field < 16; field++)
                            Require(Math.Abs(actual[field] - expected[field]) < 0.00001f, "Original tile offset matrix differs: " + chunk.name);
                    }
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
