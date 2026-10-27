using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map14
{
    // Exercises native chunk lifecycle in Play Mode. This is not a map/UI acceptance test.
    public static class OfficialMap14PartitionAcceptance
    {
        private const string Key = "Zjcs.Map14.PartitionExecution";
        private static bool started;

        public static void Run()
        {
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
            PlayerSettings.colorSpace = ColorSpace.Linear;
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
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map14 partition execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map14 partition test runner").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map14 partition Play Mode passed: 240 original chunks, asynchronous local handles, inactive/active/clear/reload/remove and removal during pending load; scene texture GPU bytes and tile rendering fixtures. Full scene, world-context bindings, UI, input and saves remain unverified.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12, "Native validation requires DX12.");
            string repository = Path.GetFullPath("..");
            var source = JsonUtility.FromJson<SceneSource>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/official-map/map14-native-scene-source-manifest.json")));
            int mipCount = 0;
            foreach (var entry in source.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.path.Substring(6));
                foreach (var mip in entry.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    while (!request.done) yield return null;
                    Require(!request.hasError && Hash(request.GetData<byte>().ToArray()) == mip.rgba_sha256,
                        "Original scene GPU mip bytes differ: " + entry.path + ":" + mip.level);
                    mipCount++;
                }
            }
            Debug.Log("Map14 original scene texture GPU payloads verified: " + source.textures.Length + " textures, " + mipCount + " mips.");
            var cameraObject = new GameObject("Partition test camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.farClipPlane = 1000; camera.allowHDR = false; camera.allowMSAA = false;
            camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var target = new RenderTexture(450, 800, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create(); camera.targetTexture = target;
            var light = new GameObject("Tile rendering light fixture").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.color = Color.white; light.intensity = 1;
            var lightFields = new SerializedObject(light);
            var layers = lightFields.FindProperty("m_ApplyToSortingLayers");
            layers.arraySize = SortingLayer.layers.Length;
            for (int i = 0; i < layers.arraySize; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
            lightFields.ApplyModifiedPropertiesWithoutUndo();
            var bare = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-scene-catalog")
                .Load<GameObject>("Assets/Partition/MapGround_14/MapGround_14_bare");
            var originalMaps = bare.GetComponentsInChildren<TilemapRenderer>(true).ToDictionary(value => value.name);
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-terrain-catalog");
            Require(catalog != null, "Missing native catalog");
            var data = catalog.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            var chunks = new List<PartitionData.Chunk>(data.TileChunks);
            foreach (var region in data.RegionDatas)
                if (region.LayerDatas != null)
                    foreach (var layer in region.LayerDatas)
                        if (layer?.Tiles != null) chunks.AddRange(layer.Tiles);
            Require(chunks.Count == 240 && chunks.Select(chunk => chunk.Res).Distinct().Count() == 240, "Original partition tile references differ");
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
                    var renderer = mapHost.AddComponent<TilemapRenderer>();
                    var original = originalMaps[mapData.Name];
                    renderer.sharedMaterials = original.sharedMaterials;
                    renderer.sortingLayerID = original.sortingLayerID;
                    renderer.sortingOrder = original.sortingOrder;
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
                if ((record.Pos.x == 0 && record.Pos.y == 0) || (record.Pos.x == 3 && record.Pos.y == -3))
                {
                    camera.transform.position = new Vector3((record.Pos.x + 0.5f) * data.ChunkSize.x,
                        (record.Pos.y + 0.5f) * data.ChunkSize.y, -100);
                    yield return null; yield return null;
                    var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                    while (!request.done) yield return null;
                    Require(!request.hasError, "Original tile fixture camera failed to render.");
                    var pixels = request.GetData<Color32>().ToArray();
                    Require(pixels.Any(value => value.r > 10 || value.g > 10 || value.b > 10), "Original tile fixture rendered blank.");
                    var image = new Texture2D(450, 800, TextureFormat.RGBA32, false, true);
                    image.SetPixels32(pixels); image.Apply();
                    File.WriteAllBytes(Path.Combine(repository, "output/map14-tiles-" + record.Pos.x + "-" + record.Pos.y + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image);
                }
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
            Debug.Log($"Map14 original chunk coverage: {full} full, {partial} partial, {offset} tilemap entries with offsets.");
            Require(full == 240 && partial == 0 && offset > 0, "Original Map14 chunk classification differs");
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(light.gameObject);
        }

        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class TextureSource { public string path; public Mip[] mips; }
        [Serializable] private sealed class SceneSource { public TextureSource[] textures; }
        private static string Hash(byte[] value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
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
