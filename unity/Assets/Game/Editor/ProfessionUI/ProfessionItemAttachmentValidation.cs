using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zjcs.Offline.ProfessionUI;
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
        private static FontAssetFontService fonts;
        private static ItemIconAttachmentManager manager;
        private static ItemIconAttachment attachment;
        private static GAction lowMemory;
        private static Func<Transform, GameObject, Transform, bool, GameObject> previousInstantiate;
        private static int phase;
        public sealed class PoolObserver : MonoBehaviour, IGameObjectPoolItem
        {
            public int Resets, Clears;
            public Transform ResetParent, ClearParent;
            public bool ResetActive, ClearActive;
            public void OnGameObjectPoolReset() { Resets++; ResetParent = transform.parent; ResetActive = gameObject.activeSelf; }
            public void OnGameObjectPoolClear() { Clears++; ClearParent = transform.parent; ClearActive = gameObject.activeSelf; }
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
            try
            {
            Require(Application.dataPath.Replace('\\', '/').Contains("/.tools/profession-ui-validation/unity/"), "isolated project");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "original prefab native import");
            Require(prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "all original components bound");
            Require(prefab.GetComponent<ItemIconAttachmentHint>() != null, "original attachment root script");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
            EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup >= SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            { Debug.LogError("Profession native attachment pool validation timed out."); Finish(1); return; }
            if (!ready || !EditorApplication.isPlaying) return;
            try
            {
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount < startedFrame + 3) return;
                if (phase == 1)
                {
                    Require(hint == null, "low-memory release destroys the queued native instance after a frame");
                    hint = (ItemIconAttachmentHint)attachment.GetAttachment(ItemIconAttachmentType.HintTip, false);
                    Require(hint != null && !hint.gameObject.activeSelf, "borrow after low-memory release creates an original inactive attachment");
                    attachment.SetShow(ItemIconAttachmentType.HintTip, true);
                    hint.SetCenterHintObjActive(true); hint.SetKey("{0}");
                    ((WorldMultiLanguageText)Field(typeof(ItemIconAttachmentHint), "Text").GetValue(hint)).Format(300);
                    phase = 2; startedFrame = Time.frameCount; return;
                }
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
                if (phase == 0)
                {
                    attachment.HideAll(); lowMemory.Invoke(); phase = 1; startedFrame = Time.frameCount; return;
                }
                attachment.HideAll(); manager.Dispose(); manager = null;
                Require(Field(typeof(GAction), "mRoot").GetValue(lowMemory) == null, "manager disposal removes original low-memory subscription");
                Debug.Log("Profession original attachment manager/pool FIFO reuse, clear/reset order, low-memory release, destruction, font binding and Play Mode GPU rendering passed. Full profession GUI input is not covered.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true; startedFrame = Time.frameCount;
            var fontCatalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>("Assets/Official/ProfessionUI/Fonts/ProfessionFontResources.asset");
            var fontConfig = AssetDatabase.LoadAssetAtPath<OfflineProfessionFontConfig>("Assets/Official/ProfessionUI/Fonts/ProfessionFontConfig.asset");
            fonts = new FontAssetFontService(new OfflineProfessionImageResourceLoader(fontCatalog), fontConfig.Deserialize);
            fonts.Initialize();
            previousInstantiate = ProfessionGameObjectPoolBridge.Provider;
            ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, worldPositionStays) => Object.Instantiate(prefab, parent, worldPositionStays);
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
            VerifyGenericPool(ownerGo.transform);
            attachment = ownerGo.AddComponent<ItemIconAttachment>();
            lowMemory = new GAction();
            var itemCatalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>("Assets/Official/ProfessionUI/Catalogs/ProfessionItemImages.asset");
            var attachmentConfig = AssetDatabase.LoadAssetAtPath<ItemIconAttachmentConfig>("Assets/Official/ProfessionUI/Native/ScriptableConfig/Modules/ItemIcon/ItemIconAttachmentConfig.asset");
            Require(attachmentConfig != null && attachmentConfig.Templates.Count == 42, "complete original attachment configuration");
            manager = new ItemIconAttachmentManager(new OfflineProfessionImageResourceLoader(itemCatalog), () => attachmentConfig, lowMemory);
            attachment.Bind(manager);
            var first = attachment.GetAttachment(ItemIconAttachmentType.HintTip, false);
            Require(first != null && !first.gameObject.activeSelf, "original inactive source prefab and lazy load");
            Require(attachment.GetAttachment(ItemIconAttachmentType.HintTip) == first && !first.gameObject.activeSelf, "cached lookup does not reactivate an existing attachment");
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
            attachment.HideAll(); Require(!first.gameObject.activeSelf && first.transform.parent != ownerGo.transform, "HideAll deactivates and reparents into original pool");
            var next = attachment.GetAttachment(ItemIconAttachmentType.HintTip, false);
            Require(next == first && !next.gameObject.activeSelf, "original manager reuses the same pooled native instance");
            attachment.SetShow(ItemIconAttachmentType.HintTip, true);
            hint = (ItemIconAttachmentHint)next;
            hint.SetCenterHintObjActive(true); hint.SetKey("{0}");
            ((WorldMultiLanguageText)Field(typeof(ItemIconAttachmentHint), "Text").GetValue(hint)).Format(300);
            var nativeText = hint.GetComponentInChildren<TMP_Text>(true); nativeText.ForceMeshUpdate();
            Require(nativeText.textInfo.characterCount == 3 && nativeText.textInfo.characterInfo.Take(3).All(c => c.fontAsset == nativeText.font), "original source font renders every hint glyph");
        }
        private static void VerifyGenericPool(Transform slot)
        {
            var prefab = new GameObject("LifecycleProbe", typeof(PoolObserver)); prefab.SetActive(false);
            var pool = new GameObjectPool<PoolObserver>(slot, prefab);
            pool.MakeSureCount(2);
            var first = pool.Borrow(slot, false); var second = pool.Borrow(slot, true);
            Require(first != second && first.Resets == 1 && second.Resets == 1 && first.ResetParent == slot && !first.ResetActive && second.ResetActive, "borrow resets after parent and activation");
            pool.Return(first); pool.Return(second);
            Require(second.Clears == 1 && second.ClearParent == slot && second.ClearActive && !second.gameObject.activeSelf, "return clears before deactivation and reparenting");
            var reused = pool.Borrow(slot, false); Require(reused == first && reused.Resets == 2, "FIFO reuse and reset");
            var list = new System.Collections.Generic.List<PoolObserver> { reused };
            pool.RecycleMany(list, 2); Require(list[0] == first && list[1] == second && first.Resets == 3 && second.Resets == 2, "grow resets existing entries before borrowing");
            pool.RecycleMany(list, 1); Require(list.Count == 1 && list[0] == first && first.Resets == 4 && second.Clears == 2, "shrink returns tail before resetting retained entries");
            pool.ReturnMany(list); Require(list.Count == 0 && first.Clears == 2, "ReturnMany clears the caller list");
            pool.Release(1); pool.Destory(); Object.Destroy(prefab);
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
            fonts?.Dispose();
            manager?.Dispose(); ProfessionGameObjectPoolBridge.Provider = previousInstantiate;
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
    }
}
