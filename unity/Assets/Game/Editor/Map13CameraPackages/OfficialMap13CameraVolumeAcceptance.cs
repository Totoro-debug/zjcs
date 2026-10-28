using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13CameraPackages
{
    public static class OfficialMap13CameraVolumeAcceptance
    {
        private const string SourcePath = "Assets/Official/World/Map/13/Source/native-camera-volume.json";
        private const string Key = "Zjcs.Map13.CameraVolume";
        private static bool started, runtimeError;
        [Serializable] private sealed class Source { public string manifest, assetPath, catalogResource; public Item[] objects; }
        [Serializable] private sealed class Item { public string identity, type, name, scriptGuid; public long fileId; public Check[] checks; }
        [Serializable] private sealed class Check { public string path, kind, value; public long fileId; }
        private sealed class Runner : MonoBehaviour { }
        public static void BuildAndValidate()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            Require(source.manifest == "871_156598" && source.objects.Length == 2, "Original camera Volume selection differs.");
            AssetDatabase.ImportAsset(source.assetPath, ImportAssetOptions.ForceSynchronousImport);
            var assets = AssetDatabase.LoadAllAssetsAtPath(source.assetPath);
            var catalog = Resources.Load<NativeMap13AssetCatalog>(source.catalogResource);
            Require(catalog && catalog.Bindings.Length == 2, "Camera Volume resource catalog is not explicitly bound.");
            int checkedFields = 0;
            foreach (var item in source.objects)
            {
                var asset = assets.Single(v => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(v, out string guid, out long id); return id == item.fileId; });
                Require(asset.name == item.name && asset.GetType().Name == item.type, "Original camera Volume object identity changed.");
                Require(catalog.Load<UnityEngine.Object>(item.identity) == asset, "Camera Volume CAB binding changed.");
                var serialized = new SerializedObject(asset);
                Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(serialized.FindProperty("m_Script").objectReferenceValue)) == item.scriptGuid, "Camera Volume native script binding changed.");
                foreach (var check in item.checks)
                {
                    var property = serialized.FindProperty(check.path);
                    Require(property != null, "Native camera Volume field is missing: " + item.type + "." + check.path);
                    if (check.kind == "Array") Require(property.arraySize == int.Parse(check.value, CultureInfo.InvariantCulture), "Camera Volume array length changed.");
                    else if (check.kind == "ObjectReference")
                    {
                        if (check.fileId == 0) Require(property.objectReferenceValue == null, "Original null camera Volume reference changed.");
                        else
                        {
                            Require(property.objectReferenceValue != null, "Camera Volume native reference was lost.");
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(property.objectReferenceValue, out string guid, out long id);
                            Require(id == check.fileId && guid == AssetDatabase.AssetPathToGUID(source.assetPath), "Camera Volume native reference changed.");
                        }
                    }
                    else if (property.propertyType == SerializedPropertyType.Boolean) Require(property.boolValue == (check.value != "0" && check.value != "False"), "Camera Volume Boolean differs: " + check.path);
                    else if (property.propertyType == SerializedPropertyType.Float)
                    {
                        float value = float.Parse(check.value, CultureInfo.InvariantCulture);
                        Require(BitConverter.ToInt32(BitConverter.GetBytes(property.floatValue), 0) == BitConverter.ToInt32(BitConverter.GetBytes(value), 0), "Camera Volume Float32 differs: " + check.path);
                    }
                    else if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum)
                        Require(property.intValue == int.Parse(check.value, CultureInfo.InvariantCulture), "Camera Volume integer differs: " + check.path);
                    else throw new InvalidOperationException("Unverified camera Volume field type: " + check.path + " " + property.propertyType);
                    checkedFields++;
                }
            }
            Require(checkedFields == 29, "Camera Volume field coverage changed.");
            var profile = assets.OfType<VolumeProfile>().Single();
            Require(profile.components.Count == 1 && profile.TryGet<Bloom>(out var bloom) && bloom.intensity.value == 1, "Original Bloom dependency is not usable.");
            Debug.Log("Map13 camera Volume native bindings passed: two original objects, 11 Bloom parameters, all 29 serialized fields and native/CAB references, including legacy skipIterations and null dirt texture.");
        }
        public static void Run()
        {
            BuildAndValidate();
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1"); Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1); SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = runtimeError = false;
            Application.logMessageReceived -= Observe; Application.logMessageReceived += Observe;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void Observe(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = true; }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0); SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Observe; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0)) { Debug.LogError("Camera Volume GPU probe timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Map13 original camera Volume probe").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(Key + ".result", 0); Debug.Log("Map13 original camera Volume GPU passed: actual original URP, native profile, HDR sprite and Bloom spread; all runtime errors monitored. Component diagnostic only; complete camera prefab and Map13 gameplay remain pending."); EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            foreach (var startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(v => v.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow")) { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (var old in UnityEngine.Object.FindObjectsOfType<Camera>()) old.enabled = false;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; ShaderUtil.allowAsyncCompilation = false;
            Require(GraphicsSettings.defaultRenderPipeline != null && QualitySettings.activeColorSpace == ColorSpace.Linear, "Original URP and Linear color space are unavailable.");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            var profile = Resources.Load<NativeMap13AssetCatalog>(source.catalogResource).Load<VolumeProfile>(source.objects.Single(v => v.type == "VolumeProfile").identity);
            var root = new GameObject("Camera Volume diagnostic");
            var cameraGo = new GameObject("Original pipeline diagnostic camera"); cameraGo.transform.SetParent(root.transform, false); cameraGo.transform.position = new Vector3(0, 0, -250);
            var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 2; camera.nearClipPlane = .3f; camera.farClipPlane = 500; camera.allowHDR = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>(); cameraData.SetRenderer(0); cameraData.renderPostProcessing = false; cameraData.volumeLayerMask = 1;
            var volume = new GameObject("Original Volume profile diagnostic").AddComponent<Volume>(); volume.transform.SetParent(root.transform, false); volume.isGlobal = true; volume.weight = 1; volume.sharedProfile = profile; volume.enabled = false;
            var frame = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); frame.Create(); camera.targetTexture = frame;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/BoundEntity/SpritesDefault_b64456be.shader"); Require(shader && shader.isSupported, "Verified original Sprite shader is unavailable.");
            var material = new Material(shader); material.SetColor("_Color", new Color(8, 8, 8, 1));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            var marker = new GameObject("HDR diagnostic marker; never a game asset").AddComponent<SpriteRenderer>(); marker.transform.SetParent(root.transform, false); marker.sprite = sprite; marker.sharedMaterial = material;
            yield return null; yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
            Color[] hdr = Read(frame, "hdr-source"); Require(hdr.Max(v => v.r) > 2, "Diagnostic HDR source did not reach the original pipeline: max=" + hdr.Max(v => v.r));
            Debug.Log("Camera Volume HDR source: target=" + frame.graphicsFormat + " max=" + hdr.Max(v => v.r));
            cameraData.renderPostProcessing = true;
            yield return null; yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
            Color[] without = Read(frame, "without-bloom");
            volume.enabled = true;
            yield return null; yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
            var active = VolumeManager.instance.stack.GetComponent<Bloom>(); Require(active && active.intensity.value == 1 && Mathf.Abs(active.threshold.value - 1.3f) < 1e-6f, "The actual camera did not consume original Bloom settings.");
            Color[] with = Read(frame, "with-bloom"); int spread = 0; float added = 0;
            for (int y = 0; y < frame.height; y++) for (int x = 0; x < frame.width; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - 63.5f), Mathf.Abs(y - 63.5f)) <= 20) continue;
                int index = y * frame.width + x; float delta = with[index].r - without[index].r;
                if (delta > .001f) { spread++; added += delta; }
            }
            Require(spread > 64 && added > 1, "Original camera Bloom did not spread outside the diagnostic sprite: pixels=" + spread + " added=" + added);
            Debug.Log("Original camera Bloom GPU spread: " + spread + " pixels, HDR added=" + added);
            camera.targetTexture = null; frame.Release(); UnityEngine.Object.Destroy(frame); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(material);
            yield return null; Require(!runtimeError, "Original camera Volume runtime reported errors.");
        }
        private static Color[] Read(RenderTexture frame, string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = frame;
            var texture = new Texture2D(frame.width, frame.height, TextureFormat.RGBAFloat, false, true); texture.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); texture.Apply(); var colors = texture.GetPixels(); UnityEngine.Object.Destroy(texture);
            var preview = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, true); preview.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); preview.Apply();
            string output = Path.GetFullPath(".tools/map13-camera-volume/" + name + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, preview.EncodeToPNG()); UnityEngine.Object.Destroy(preview); RenderTexture.active = previous; return colors;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
