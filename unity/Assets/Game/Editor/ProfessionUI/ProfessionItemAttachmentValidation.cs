using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionItemAttachmentValidation
    {
        private const string Active = "Zjcs.ProfessionItemAttachment.Active";
        private const string PrefabPath = "Assets/Official/ProfessionUI/Native/Prefabs/UI/Item/Attachments/CenterHintContent.prefab";
        private static bool ready, initialized;
        private static int startedFrame;
        private static Camera camera;
        private static RenderTexture target;
        private static Func<string, string> previousLanguage;
        private static ItemIconAttachmentHint hint;
        private sealed class AttachmentManager : IItemIconAttachmentManager
        {
            public GameObject Prefab;
            public int Loads, Returns;
            public GameObject GetAttachment(ItemIconAttachmentType type, Transform slot, bool autoActive)
            {
                Require(type == ItemIconAttachmentType.HintTip, "original HintTip enum route");
                Loads++;
                var result = Object.Instantiate(Prefab, slot, false);
                if (autoActive) result.SetActive(true);
                return result;
            }
            public void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell cell)
            {
                Require(type == ItemIconAttachmentType.HintTip, "recycle original attachment type");
                Returns++;
                Object.Destroy(cell.gameObject);
            }
        }
        static ProfessionItemAttachmentValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
            };
        }
        public static void Run()
        {
            Require(Application.dataPath.Replace('\\', '/').Contains("/.tools/profession-ui-validation/unity/"), "isolated project");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "original prefab native import");
            Require(prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "all original components bound");
            Require(prefab.GetComponent<ItemIconAttachmentHint>() != null, "original attachment root script");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false) || !ready || !EditorApplication.isPlaying) return;
            try
            {
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount < startedFrame + 3) return;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                RenderTexture.active = previous;
                Require(pixels.GetPixels32().Count(pixel => pixel.a > 0) > 100, "original attachment actually renders on GPU");
                string repository = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "../../.."));
                File.WriteAllBytes(Path.Combine(repository, "output/profession-ui-native-20261028/item-hint-native.png"), pixels.EncodeToPNG());
                Object.Destroy(pixels);
                Debug.Log("Profession original item attachment import, retained references, activation, reuse/recycle and Play Mode GPU rendering passed. Full ItemIcon and profession GUI input are not covered.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true; startedFrame = Time.frameCount;
            previousLanguage = WorldMultiLanguageText.Localize;
            WorldMultiLanguageText.Localize = key => key;
            var cameraGo = new GameObject("OriginalAttachmentProbeCamera");
            camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 128;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.transform.position = new Vector3(0, 0, -10);
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var canvasGo = new GameObject("OriginalAttachmentProbeCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(256, 256);
            var ownerGo = new GameObject("OriginalAttachmentProbeOwner", typeof(RectTransform));
            ownerGo.transform.SetParent(canvas.transform, false);
            var attachment = ownerGo.AddComponent<ItemIconAttachment>();
            var manager = new AttachmentManager { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) };
            attachment.Bind(manager);
            var first = attachment.GetAttachment(ItemIconAttachmentType.HintTip, false);
            Require(first != null && !first.gameObject.activeSelf && manager.Loads == 1, "original inactive source prefab and lazy load");
            Require(attachment.GetAttachment(ItemIconAttachmentType.HintTip) == first && manager.Loads == 1 && !first.gameObject.activeSelf, "cached lookup does not reactivate an existing attachment");
            attachment.SetShow(ItemIconAttachmentType.HintTip, true);
            Require(first.gameObject.activeSelf, "SetShow explicitly activates cached source instance");
            hint = (ItemIconAttachmentHint)first;
            var hintObject = (GameObject)Field(typeof(ItemIconAttachmentHint), "HintObj").GetValue(hint);
            var text = (WorldMultiLanguageText)Field(typeof(ItemIconAttachmentHint), "Text").GetValue(hint);
            Require(hintObject != null && text != null && text.transform.IsChildOf(hint.transform), "native HintObj/Text serialized references remain internal");
            hint.SetCenterHintObjActive(false); Require(!hintObject.activeSelf, "hide original child");
            hint.SetCenterHintObjActive(true); Require(hintObject.activeSelf, "show original child");
            hint.SetKey("{0}"); text.Format(300);
            Require(text.Text == "300", "original key and formatter update native text");
            attachment.HideAll(); Require(manager.Returns == 1, "HideAll recycles cached original instance");
            var next = attachment.GetAttachment(ItemIconAttachmentType.HintTip, false);
            Require(next != null && next != first && manager.Loads == 2, "HideAll clears cache before next borrow");
            attachment.SetShow(ItemIconAttachmentType.HintTip, true);
            hint = (ItemIconAttachmentHint)next;
            hint.SetCenterHintObjActive(true); hint.SetKey("{0}");
            ((WorldMultiLanguageText)Field(typeof(ItemIconAttachmentHint), "Text").GetValue(hint)).Format(300);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Profession item attachment: " + message);
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            WorldMultiLanguageText.Localize = previousLanguage;
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
    }
}
