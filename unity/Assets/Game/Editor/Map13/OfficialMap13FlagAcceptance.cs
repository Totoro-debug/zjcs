using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Tests original native renderer assets and texture upload; uses isolated fixtures, never user saves.
    public static class OfficialMap13FlagAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeFlagExecution";
        private static bool started;

        public static void Run()
        {
            OfficialMap13FlagTools.RestoreAndValidate();
            OfficialMap13FlagSceneTools.RestoreAndValidate();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
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
                Debug.LogError("Map13 pipeline execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 pipeline test runner").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map13 native flag Play Mode passed: original Animator clocks, native weighted curves, independent reference poses, dynamic weighted meshes, original texture GPU upload, seven original shrine prefabs and DX12 rendering. Complete map interaction and saves remain unverified.");
            EditorApplication.isPlaying = false;
        }


        [Serializable] private sealed class Sample { public float time; public float[] rotations; public Vector2[] vertices; }
        [Serializable] private sealed class ReferenceModel { public string name; public Sample[] samples; }
        [Serializable] private sealed class References { public ReferenceModel[] models; }
        [Serializable] private sealed class FlagMip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class FlagTexture { public string path; public FlagMip[] mips; }
        [Serializable] private sealed class FlagTextures { public FlagTexture[] textures; }

        private static IEnumerator VerifySteps()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Native flag GPU check requires original Linear color and Windows DX12.");
            string root = OfficialMap13FlagTools.Repository;
            var references = JsonUtility.FromJson<References>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-flags-reference.json"));
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-flag-catalog");
            var models = OfficialMap13FlagTools.Read();
            var sourceTextures = JsonUtility.FromJson<FlagTextures>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-native-flags-source-manifest.json")));
            int mipCount = 0;
            var sceneTextures = JsonUtility.FromJson<FlagTextures>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-native-flag-scene-source-manifest.json")));
            foreach (var sourceTexture in sourceTextures.textures.Concat(sceneTextures.textures))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceTexture.path.Substring(6));
                foreach (var mip in sourceTexture.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    while (!request.done) yield return null;
                    if (request.hasError || Hash(request.GetData<byte>().ToArray()) != mip.rgba_sha256)
                        throw new InvalidDataException("Original flag native GPU texture/mip/rows differ: " + sourceTexture.path);
                    mipCount++;
                }
            }
            var cameraObject = new GameObject("Original flag native animation camera fixture");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 1);
            camera.allowHDR = false; camera.allowMSAA = false;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additional.SetRenderer(0); additional.renderPostProcessing = false;
            var target = new RenderTexture(384, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create(); camera.targetTexture = target;
            var players = new List<NativeMap13FlagMesh>();
            int poses = 0, vertexCount = 0;
            for (int index = 0; index < references.models.Length; index++)
            {
                var reference = references.models[index];
                var data = catalog.Load<NativeMap13FlagData>(reference.name);
                var host = new GameObject(reference.name); host.SetActive(false);
                host.transform.position = new Vector3((index - 1) * 0.72f, -1.05f, 0);
                var animator = host.AddComponent<Animator>(); animator.runtimeAnimatorController = data.Controller;
                var renderer = host.AddComponent<MeshRenderer>(); renderer.sharedMaterial = data.Material;
                renderer.sortingLayerName = "Object"; host.AddComponent<MeshFilter>();
                var player = host.AddComponent<NativeMap13FlagMesh>(); player.skeletonDataAsset = data;
                host.SetActive(true); animator.speed = 0; yield return null;
                players.Add(player);
                var model = models.models.Single(m => m.name == reference.name);
                foreach (var sample in reference.samples)
                {
                    animator.Play("loop", 0, sample.time / data.Duration); animator.Update(0);
                    yield return null; yield return null;
                    if (player.Bones.Length != data.Bones.Length) throw new InvalidDataException("Original native flag bone count differs.");
                    foreach (var track in model.tracks)
                    {
                        var binding = EditorCurveBinding.FloatCurve(OfficialMap13FlagTools.BonePath(model, track.bone), typeof(Transform), "localEulerAnglesRaw.z");
                        var curve = AnimationUtility.GetEditorCurve(data.Clip, binding);
                        if (Math.Abs(curve.Evaluate(sample.time) - sample.rotations[track.bone]) > 0.0005f)
                            throw new InvalidDataException("Native weighted Unity curve differs from independent cubic reference: " + reference.name + " bone " + track.bone + " time " + sample.time);
                    }
                    for (int bone = 0; bone < player.Bones.Length; bone++)
                        if (Math.Abs(Mathf.DeltaAngle(player.Bones[bone].localEulerAngles.z, sample.rotations[bone])) > 0.001f)
                            throw new InvalidDataException("Native Animator bone pose differs from original curve: " + reference.name + " bone " + bone + " time " + sample.time + " actual " + player.Bones[bone].localEulerAngles.z + " expected " + sample.rotations[bone]);
                    var vertices = player.DeformedMesh.vertices;
                    if (vertices.Length != sample.vertices.Length) throw new InvalidDataException("Native original mesh vertex count differs.");
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        if (Vector2.Distance(vertices[vertex], sample.vertices[vertex]) > 0.00002f)
                            throw new InvalidDataException("Native weighted flag mesh differs from independent affine reference: " + reference.name + " vertex " + vertex + " time " + sample.time + " actual " + vertices[vertex] + " expected " + sample.vertices[vertex]);
                        vertexCount++;
                    }
                    if (!player.DeformedMesh.triangles.SequenceEqual(data.Triangles) || !player.DeformedMesh.uv.SequenceEqual(data.UV))
                        throw new InvalidDataException("Original native flag topology/UV order differs.");
                    poses++;
                }
                animator.Play("loop", 0, 0); animator.Update(0); yield return null;
            }
            Debug.Log("Map13 native Animator/reference validation passed: " + poses + " poses, " + vertexCount + " weighted vertices, all original topology/UVs and " + mipCount + " GPU texture mips.");
            Vector3[][] before = players.Select(p => p.DeformedMesh.vertices).ToArray();
            for (int frame = 0; frame < 2; frame++)
            {
                if (frame == 1)
                {
                    foreach (var player in players) player.GetComponent<Animator>().speed = 1;
                    yield return new WaitForSeconds(0.45f);
                }
                yield return null; yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Original flag DX12 camera output failed.");
                var image = new Texture2D(384, 512, TextureFormat.RGBA32, false, true);
                image.SetPixels32(request.GetData<Color32>().ToArray()); image.Apply();
                File.WriteAllBytes(Path.Combine(root, "output/map13-native-flag-frame-" + frame + "-20261027.png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
            }
            for (int i = 0; i < players.Count; i++)
                if (!players[i].DeformedMesh.vertices.Where((v, index) => Vector3.Distance(v, before[i][index]) > 0.0001f).Any())
                    throw new InvalidDataException("Native flag did not animate through normal Unity frames: " + players[i].name);
            foreach (var player in players) UnityEngine.Object.Destroy(player.gameObject);
            yield return null; yield return null;
            var sceneCatalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-flag-scene-catalog");
            int originalInstances = 0;
            foreach (var sceneBinding in sceneCatalog.Bindings)
            {
            var scene = UnityEngine.Object.Instantiate((GameObject)sceneBinding.Asset);
            var sceneFlags = scene.GetComponentsInChildren<NativeMap13FlagMesh>(true);
            if (sceneFlags.Length == 0 || sceneFlags.Any(f => !f.DeformedMesh || !f.gameObject.activeInHierarchy))
                throw new InvalidDataException("Original shrine flag instances must initialize in the actual prefab hierarchy.");
            originalInstances += sceneFlags.Length;
            var sceneRenderers = scene.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = sceneRenderers[0].bounds;
            foreach (var renderer in sceneRenderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 20);
            camera.orthographicSize = Math.Max(bounds.extents.y, bounds.extents.x / (384f / 512)) * 1.05f;
            Vector3[][] sceneBefore = sceneFlags.Select(f => f.DeformedMesh.vertices).ToArray();
            for (int frame = 0; frame < 2; frame++)
            {
                yield return new WaitForSeconds(frame == 0 ? 0.1f : 0.4f);
                yield return null; yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Original shrine partition DX12 camera readback failed.");
                var image = new Texture2D(384, 512, TextureFormat.RGBA32, false, true);
                image.SetPixels32(request.GetData<Color32>().ToArray()); image.Apply();
                File.WriteAllBytes(Path.Combine(root, "output/map13-original-shrine-" + scene.name.Replace("(Clone)", "") + "-frame-" + frame + "-20261027.png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
            }
            if (sceneFlags.Any(f => !f.GetComponent<MeshRenderer>().isVisible))
                throw new InvalidDataException("Original shrine camera did not include all 46 flag instances.");
            for (int i = 0; i < sceneFlags.Length; i++)
                if (!sceneFlags[i].DeformedMesh.vertices.Where((v, vertex) => Vector3.Distance(v, sceneBefore[i][vertex]) > 0.0001f).Any())
                    throw new InvalidDataException("Original shrine flag did not animate in the restored prefab: " + sceneFlags[i].name);
            Vector3 sceneCameraPosition = camera.transform.position;
            camera.transform.position += new Vector3(10000, 10000, 0);
            yield return null; yield return null; yield return null;
            var hidden = sceneFlags.Select(f => f.DeformedMesh.vertices).ToArray();
            yield return new WaitForSeconds(0.15f);
            for (int i = 0; i < sceneFlags.Length; i++)
                if (sceneFlags[i].GetComponent<MeshRenderer>().isVisible || !sceneFlags[i].DeformedMesh.vertices.SequenceEqual(hidden[i]))
                    throw new InvalidDataException("Original invisible Nothing mode did not stop native deformation.");
            camera.transform.position = sceneCameraPosition;
            yield return null; yield return null; yield return null;
            scene.SetActive(false); yield return null; scene.SetActive(true);
            yield return null; yield return null; yield return null;
            if (scene.GetComponentsInChildren<NativeMap13FlagMesh>(true).Length != sceneFlags.Length || sceneFlags.Any(f => !f.DeformedMesh))
                throw new InvalidDataException("Original shrine flag disable/re-enable duplicated or lost native state.");
            UnityEngine.Object.Destroy(scene); yield return null; yield return null;
            }
            if (originalInstances != 46) throw new InvalidDataException("Original shrine prefab set must cover 46 source components.");
            Debug.Log("Map13 original shrine prefabs Play Mode passed: 17 partition and 29 entity-prefab flag instances in seven original hierarchies, actual camera rendering/animation, invisible Nothing mode and disable/re-enable.");
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }
        private static string Hash(byte[] value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
