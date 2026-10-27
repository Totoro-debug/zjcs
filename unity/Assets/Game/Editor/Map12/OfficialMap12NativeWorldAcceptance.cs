using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Reflection;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using Zjcs.Official.World.NativeMap12Labels;
using Zjcs.Presentation.World.NativeMap12;

namespace Zjcs.Editor.Map12
{
    public static class OfficialMap12NativeWorldAcceptance
    {
        public static void VerifyOpenedRegion(MonoBehaviour runtime, int region)
        {
            var view = runtime.GetComponentInChildren<OfficialMap12NativeWorldView>(true);
            var controller = view.GroundRoot.PartitionController;
            if (controller.Context.GetCurrentMapStatus()[region] != MapRegionStatus.Public)
                throw new InvalidDataException("Saved goddess activation did not update the original native world context.");
            var registered = (IDictionary)typeof(PartitionLoader).GetField("mId2Assets", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller.Loader);
            var global = view.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true)
                .Single(l => l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global);
            if (!global.gameObject.activeInHierarchy || global.intensity != 1f
                || global.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                throw new InvalidDataException("Original active-scene global lighting is not enabled after goddess activation.");
            var day = controller.PartitionData.RegionDatas[region].GetLayerData(PartitionLayer.Day);
            var dayIds = (day?.Tiles?.Select(c => c.Id) ?? Enumerable.Empty<ushort>())
                .Concat(day?.Scenes?.Select(c => c.Id) ?? Enumerable.Empty<ushort>())
                .Concat(day?.Extensions?.Select(c => c.Id) ?? Enumerable.Empty<ushort>());
            foreach (ushort id in dayIds)
                if (!registered.Contains(id) || (ChunkRuntimeState)typeof(PartitionAsset)
                    .GetField("mRuntimeState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registered[id]) != ChunkRuntimeState.Active)
                    throw new InvalidDataException("Original Day layer was not registered and activated: " + id);
            var night = controller.PartitionData.RegionDatas[region].GetLayerData(PartitionLayer.Night);
            var ids = (night?.Tiles?.Select(c => c.Id) ?? Enumerable.Empty<ushort>())
                .Concat(night?.Scenes?.Select(c => c.Id) ?? Enumerable.Empty<ushort>())
                .Concat(night?.Extensions?.Select(c => c.Id) ?? Enumerable.Empty<ushort>()).ToArray();
            if (ids.Any(id => registered.Contains(id)))
                throw new InvalidDataException("Original five-second Night layer removal was not preserved.");
        }

        public static IEnumerator VerifyRuntime(MonoBehaviour runtime, Func<int, int, bool> canStand,
            Action<int, int> setPosition, string output)
        {
            var view = runtime.GetComponentInChildren<OfficialMap12NativeWorldView>(true);
            var data = JsonUtility.FromJson<Map12LabelData>(Resources.Load<TextAsset>("Map12/Native/region-label-data").text);
            var root = view.GroundRoot;
            var water = view.GetComponent<OfficialMap12WaterView>();
            Shader waterShader = Resources.Load<Shader>("World/Map12Water");
            if (water == null || water.RendererCount != 57 || waterShader == null)
                throw new InvalidDataException("Official Map12 Water2 binding did not load all 57 source renderers.");
            var waterSource = JsonUtility.FromJson<Map12WaterAcceptancePayload>(
                Resources.Load<TextAsset>("Map12/Water/map12-water-runtime").text);
            if (waterSource?.renderers?.Length != 57) throw new InvalidDataException("Map12 Water2 runtime payload is incomplete.");
            foreach (var item in waterSource.renderers)
            {
                var renderer = root.GetComponentsInChildren<MeshRenderer>(true).SingleOrDefault(value => value.name == item.name);
                if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader != waterShader)
                    throw new InvalidDataException("Original Map12 Water2 renderer did not receive its local shader: " + item.name);
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                long indexCount = 0;
                if (mesh != null)
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                        indexCount += mesh.GetIndexCount(subMesh);
                if (mesh == null || mesh.vertexCount != item.vertices.Length / 2 || indexCount != item.indices.Length)
                    throw new InvalidDataException("Original Map12 Water2 geometry differs from its source: " + item.name);
            }
            var loader = root.PartitionController.Loader;
            var observedLights = new HashSet<int>();
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) => {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                // Deliberate position fixture: it validates streaming against
                // the production avatar/camera, not a normal progression route.
                foreach (var region in data.regions)
                {
                    var points = Enumerable.Range(0, data.regionGrid.Length).Where(i => data.regionGrid[i] == region.Region
                        && canStand(i % data.width, i / data.width)).ToArray();
                    if (points.Length == 0) throw new InvalidDataException("No standable native region fixture point: " + region.Region);
                    double centerX = points.Average(i => i % data.width), centerY = points.Average(i => i / data.width);
                    int point = points.OrderBy(i => Math.Pow(i % data.width - centerX, 2) + Math.Pow(i / data.width - centerY, 2)).First();
                    setPosition(point % data.width, point / data.width);
                    Vector3 target = new Vector3(data.origin[0] + point % data.width, data.origin[1] + point / data.width, 0);
                    float deadline = Time.realtimeSinceStartup + 4f;
                    do
                    {
                        yield return null;
                        foreach (var light in root.EnvEffect.RegionLights.Where(h => h.RegionLight)) observedLights.Add(light.RegionLight.Region);
                    } while ((Vector2.Distance(Camera.main.transform.position, target) > .05f || !loader.IsPosReady(target))
                        && Time.realtimeSinceStartup < deadline);
                    if (!loader.IsPosReady(target) || data.QueryRegion(target) != region.Region)
                        throw new InvalidDataException("Production camera did not load native region " + region.Region);
                    if (!root.GetComponentsInChildren<Tilemap>(true).Any(t => t.GetUsedTilesCount() > 0))
                        throw new InvalidDataException("Production camera has no native terrain in region " + region.Region);
                    if (new[] { 0, 34, 98 }.Contains(region.Region))
                    {
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, "native-region-" + region.Region + ".png"));
                        yield return null;
                    }
                }
                if (errors.Count != 0) throw new InvalidDataException("Native production world logged an error: " + errors[0]);
                Debug.Log("Map12 production native streaming passed: 99 original region position fixtures, actual camera loading, "
                    + observedLights.Count + " observed region lights, native terrain and label lifecycle. Normal progression remains separate.");
            }
            finally { Application.logMessageReceived -= capture; }
        }

        [Serializable]
        private sealed class Map12WaterAcceptancePayload { public Map12WaterAcceptanceRenderer[] renderers; }
        [Serializable]
        private sealed class Map12WaterAcceptanceRenderer
        {
            public string name;
            public float[] vertices, uv;
            public int[] indices;
        }
    }
}
