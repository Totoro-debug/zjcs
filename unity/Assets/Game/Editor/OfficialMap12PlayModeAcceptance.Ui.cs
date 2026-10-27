using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static IEnumerator VerifyUiSteps()
        {
            Application.runInBackground = true;
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                UnityEngine.Object.Destroy(startup.gameObject);
            }
            // This empty-scene fixture owns its UI; the application's automatic
            // boot canvas is outside the map and would intercept simulated clicks.
            foreach (Canvas boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas
                && c.name.StartsWith("BootSplash", StringComparison.Ordinal))) UnityEngine.Object.Destroy(boot.gameObject);
            int uiMap = UnityEditor.SessionState.GetInt(Key + ".uiMap", 12);
            string uiRoot = "Map" + uiMap + "/UI/";
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>(uiRoot + "catalog").text);
            if (EventSystem.current == null) new GameObject("Map12SimulatedInput", typeof(EventSystem));
            TMP_SpriteAsset common = Resources.Load<TMP_SpriteAsset>(uiRoot + "Common");
            MaterialReferenceManager.AddSpriteAsset(common.hashCode, common);
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetSimpleHashCode(common.name), common);
            var session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), 12);
            UnityEngine.Object.Instantiate(session.Assets.GroundPrefab);
            var camera = new GameObject("Map12UiCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~(1 << 5);
            var uiCamera = new GameObject("Map12UiCaptureCamera").AddComponent<Camera>();
            uiCamera.CopyFrom(camera);
            uiCamera.cullingMask = 1 << 5;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.depth = camera.depth + 1;
            var origin = session.Definition.Navigation.Origin;
            var entry = session.Definition.Entities.Single(e => e.Id == 101);
            camera.transform.position = new Vector3(origin.X + entry.Grid[0], origin.Y + entry.Grid[1], -100);
            var entityRoot = new GameObject("Map12UiEntities");
            entityRoot.AddComponent<OfficialMap10EntityView>().Initialize(entityRoot.transform,
                new Vector2(origin.X, origin.Y), camera, 12, "Map12/Entities/native-catalog");
            string output = Path.GetFullPath("../.tools/map" + uiMap + "-ui-render-session");
            Directory.CreateDirectory(output);
            foreach (int width in new[] { 450, 720 })
            {
                int height = width == 450 ? 800 : 1280;
                SetUiGameViewSize(width, height);
                float resizeDeadline = Time.realtimeSinceStartup + 10;
                while (Screen.width != width || Screen.height != height)
                {
                    if (Time.realtimeSinceStartup > resizeDeadline)
                        throw new InvalidDataException("GameView did not adopt the portrait fixture size: " + Screen.width + "x" + Screen.height);
                    yield return null;
                }
                var frame = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                frame.Create();
                camera.targetTexture = frame;
                camera.aspect = (float)width / height;
                uiCamera.targetTexture = frame;
                uiCamera.aspect = camera.aspect;
                GameObject hud = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(uiRoot + "WorldCanvas"));
                ConfigureCanvas(hud, uiCamera, data);
                hud.GetComponent<WorldPanel>().Initialize(false);
                var overlay = new GameObject("Map12OriginalDialogs", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                ConfigureCanvas(overlay, uiCamera, data);
                overlay.GetComponent<Canvas>().sortingOrder = 100;
                yield return null;
                Canvas.ForceUpdateCanvases();
                CaptureUi(camera, uiCamera, frame, output, width + "-hud");
                foreach (string key in new[] { "GoddessTouchDialog", "LostGoddessTouchDialog" })
                {
                    string iconKey = data.bindings.Single(b => b.key == key).iconKey;
                    int expectedIcons = data.iconBindings.Count(b => b.key == iconKey);
                    int denied = 0, activated = 0, transferred = 0, closed = 0;
                    GoddessTouchDialog dialog = NewDialog(uiRoot, key, overlay.transform);
                    string title = Text(data, key == "GoddessTouchDialog" ? "entity_6010_name" : "entity_6011_name");
                    dialog.DoInit(false, false, title, Text(data, "STAGE_CONDITION_REGION_PROGRESS_NOT_FULL"), "1",
                        Array.Empty<string>(), () => activated++, () => transferred++, () => denied++, () => closed++);
                    yield return new WaitForSecondsRealtime(.6f);
                    VerifyIconState(dialog, false, expectedIcons);
                    if (uiMap == 12) VerifyNativeMasks(dialog.gameObject, data, key);
                    CaptureUi(camera, uiCamera, frame, output, width + "-" + key + "-locked");
                    SimulateUiClick(dialog.CannotActiveBtn, uiCamera);
                    if (denied != 1 || activated != 0 || closed != 0)
                        throw new InvalidDataException("Native locked goddess click state: " + key);
                    UnityEngine.Object.Destroy(dialog.gameObject);
                    yield return null;
                    dialog = NewDialog(uiRoot, key, overlay.transform);
                    dialog.DoInit(false, true, title, "100%", "1", Array.Empty<string>(),
                        () => activated++, () => transferred++, () => denied++, () => closed++);
                    yield return new WaitForSecondsRealtime(.6f);
                    CaptureUi(camera, uiCamera, frame, output, width + "-" + key + "-ready");
                    SimulateUiClick(dialog.myTouchBtn, uiCamera);
                    yield return new WaitForSecondsRealtime(.6f);
                    if (activated != 1 || closed != 1 || dialog != null)
                        throw new InvalidDataException("Native goddess activate/close click state: " + key);
                    dialog = NewDialog(uiRoot, key, overlay.transform);
                    dialog.DoInit(true, true, title, "100%", "1", Array.Empty<string>(),
                        () => activated++, () => transferred++, () => denied++, () => closed++);
                    yield return new WaitForSecondsRealtime(.6f);
                    VerifyIconState(dialog, true, expectedIcons);
                    CaptureUi(camera, uiCamera, frame, output, width + "-" + key + "-active");
                    SimulateUiClick(dialog.mTransferBtn, uiCamera);
                    yield return new WaitForSecondsRealtime(.6f);
                    if (transferred != 1 || closed != 2 || dialog != null)
                        throw new InvalidDataException("Native goddess transfer/close click state: " + key);
                }
                UnityEngine.Object.Destroy(hud);
                UnityEngine.Object.Destroy(overlay);
                camera.targetTexture = null;
                uiCamera.targetTexture = null;
                frame.Release();
                UnityEngine.Object.Destroy(frame);
                yield return null;
            }
        }

        private static string Text(OfficialMap11UiData data, string key) => data.texts.Single(t => t.key == key).value;
        private static void SetUiGameViewSize(int width, int height)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Map12 UI fixture" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            // The editor is placed on the internal display by the launcher; never focus or activate it.
            EditorWindow view = EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
            view.Repaint();
        }
        private static void VerifyNativeMasks(GameObject root, OfficialMap11UiData data, string key)
        {
            foreach (OfficialMap11UiComponent source in data.components.Where(c => c.panel == key && c.type == "Mask"))
            {
                string relative = source.path.Substring(key.Length + 2);
                Mask mask = root.transform.Find(relative).GetComponent<Mask>();
                OfficialMap10UiNodeData node = data.panels.Single(p => p.key == key).nodes.Single(n => n.path == source.path);
                if (mask.enabled != (source.fields.m_Enabled != 0) || mask.showMaskGraphic != node.mask_show_graphic)
                    throw new InvalidDataException("Original UI mask flags differ: " + source.path);
            }
        }
        private static void VerifyIconState(GoddessTouchDialog dialog, bool active, int expectedCount)
        {
            GoddessDialogIcon[] icons = dialog.DialogIconSlot.GetComponentsInChildren<GoddessDialogIcon>(true);
            if (icons.Length != expectedCount) throw new InvalidDataException("Original goddess icon state bindings are missing.");
            foreach (GoddessDialogIcon icon in icons)
            {
                GameObject[] selected = icon.mActivedStateSwitcher.Single(s => s.state == (active ? 1 : 0)).objects;
                foreach (GameObject item in icon.mActivedStateSwitcher.SelectMany(s => s.objects).Distinct())
                    if (item.activeSelf != selected.Contains(item))
                        throw new InvalidDataException("Original goddess icon state did not switch: " + item.name);
            }
        }
        private static GoddessTouchDialog NewDialog(string uiRoot, string key, Transform parent)
        {
            GameObject root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(uiRoot + key), parent, false);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            // WindowManager supplies the UI camera when mounting the original WorldSpace canvas.
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.worldCamera = parent.GetComponentInParent<Canvas>().worldCamera;
            return root.GetComponent<GoddessTouchDialog>();
        }

        private static void ConfigureCanvas(GameObject root, Camera camera, OfficialMap11UiData data)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = root.GetComponent<CanvasScaler>() ?? root.AddComponent<CanvasScaler>();
            var original = data.worldBindings.canvasScaler;
            scaler.uiScaleMode = (CanvasScaler.ScaleMode)original.scaleMode;
            scaler.referenceResolution = new Vector2(original.referenceResolution[0], original.referenceResolution[1]);
            scaler.screenMatchMode = (CanvasScaler.ScreenMatchMode)original.screenMatchMode;
            scaler.matchWidthOrHeight = original.matchWidthOrHeight;
        }

        private static void SimulateUiClick(Button expected, Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            var rect = expected.targetGraphic == null
                ? (RectTransform)expected.transform
                : (RectTransform)expected.targetGraphic.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (maximum.x - minimum.x < 20 || maximum.y - minimum.y < 12)
                throw new InvalidDataException("Native button rendered below a usable portrait size: " + expected.name);
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault().gameObject;
            if (hit == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != expected.gameObject)
                throw new InvalidDataException("Original UI button is occluded or outside its hit rectangle: " + expected.name
                    + " @ " + position + "; hits=" + string.Join(",", hits.Take(6).Select(h => h.gameObject.name))
                    + "; graphics=" + string.Join(",", expected.GetComponents<Graphic>().Select(g => g.GetType().Name
                        + ":depth=" + g.depth + ":cull=" + g.canvasRenderer.cull + ":raycast=" + g.raycastTarget
                        + ":canvas=" + g.canvas?.name + ":canvasMode=" + g.canvas?.renderMode
                        + ":graphicCount=" + GraphicRegistry.GetGraphicsForCanvas(g.canvas).Count))
                    + "; target=" + (expected.targetGraphic == null ? "none" : expected.targetGraphic.name
                        + ":center=" + expected.targetGraphic.rectTransform.TransformPoint(expected.targetGraphic.rectTransform.rect.center)
                        + ":corners=" + string.Join("/", Corners(expected.targetGraphic.rectTransform))
                        + ":canvas=" + expected.targetGraphic.canvas?.name + ":mode=" + expected.targetGraphic.canvas?.renderMode
                        + ":root=" + expected.targetGraphic.canvas?.rootCanvas?.name + ":rootRect="
                        + (expected.targetGraphic.canvas?.rootCanvas == null ? "none" : expected.targetGraphic.canvas.rootCanvas.GetComponent<RectTransform>().rect.ToString())
                        + ":scale=" + expected.targetGraphic.canvas?.scaleFactor)
                    + "; groups=" + string.Join(",", expected.GetComponentsInParent<CanvasGroup>().Select(g =>
                        g.name + ":blocks=" + g.blocksRaycasts + ":alpha=" + g.alpha)));
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pressPosition = position;
            pointer.pointerPressRaycast = hits[0];
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        private static void CaptureUi(Camera camera, Camera uiCamera, RenderTexture frame, string output, string name)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            uiCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = frame;
            var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            RenderTexture.active = previous;
        }
    }
}
