using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Inventory;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Inventory
{
    [InitializeOnLoad]
    public static class InventoryCurrentSpriteValidation
    {
        [Serializable] private sealed class Fixture { public string receipt_sha256; public ExpectedSprite[] sprites; public ExpectedTexture[] textures; }
        [Serializable] private sealed class Success { public string receipt_sha256; public bool native_and_play_passed; }
        [Serializable] private sealed class OriginalRect { public float x, y, width, height; }
        [Serializable] private sealed class PhysicsShape { public Vector2[] points; }
        [Serializable] private sealed class ExpectedSprite
        {
            public string alias, name, path, texture_path;
            public OriginalRect rect;
            public Vector2 pivot;
            public Vector4 border;
            public Vector2[] vertices, uv;
            public int[] triangles;
            public float ppu;
            public PhysicsShape[] physics_shapes;
        }
        [Serializable] private sealed class Mip { public int level; public string sha256; }
        [Serializable] private sealed class ExpectedTexture
        {
            public string path;
            public int width, height, format, mip_count, filter, aniso, wrapU, wrapV;
            public bool readable;
            public Mip[] mips;
        }
        private const string Active = "Zjcs.InventoryCurrentSpriteValidation.Active";
        private static IEnumerator checks;
        private static int frame = -1, errors;
        private static string Work => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        static InventoryCurrentSpriteValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
                AudioListener.volume = 0;
                Application.runInBackground = true;
                Application.logMessageReceived += OnLog;
                checks = CheckPlay();
            };
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(Active, false)) return;
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                { Finish(new TimeoutException("Inventory current Sprite Play validation timed out")); return; }
                if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
                frame = Time.frameCount;
                try { if (!checks.MoveNext()) Finish(errors == 0 ? null : new Exception("Unity errors: " + errors)); }
                catch (Exception error) { Finish(error); }
            };
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Current inventory art: " + message); }
        private static void Near(float actual, float expected, string label)
        { Require(Math.Abs(actual - expected) <= 0.00001f, label + " actual=" + actual + " expected=" + expected); }
        private static void Vector(Vector2 actual, Vector2 expected, string label)
        { Near(actual.x, expected.x, label + ".x"); Near(actual.y, expected.y, label + ".y"); }
        private static T Field<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Finish(Exception error)
        {
            checks = null;
            SessionState.SetBool(Active, false);
            Application.logMessageReceived -= OnLog;
            if (error == null)
            {
                Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Work, "inventory-native-fixture.json")));
                File.WriteAllText(Path.Combine(Work, "inventory-native-play-success.json"), JsonUtility.ToJson(
                    new Success { receipt_sha256 = fixture.receipt_sha256, native_and_play_passed = true }));
                Debug.Log("INVENTORY_CURRENT_SPRITES_PLAY_PASSED: original native art bindings, category/item detail/close/reopen, rendered UI and isolated profile recovery.");
            }
            else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        public static void Run()
        {
            Require(!string.IsNullOrEmpty(Work) && Directory.Exists(Work), "Select a validation workspace explicitly");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ConfigureViewport();
            OfficialInventoryAssetTools.ImportAndValidate();
            ValidateNative();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorApplication.EnterPlaymode();
        }
        private static void ValidateNative()
        {
            Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Work, "inventory-native-fixture.json")));
            var catalog = OfficialInventoryAssetCatalog.LoadDefault();
            Require(SystemInfo.supportsAsyncGPUReadback, "GPU readback support");
            foreach (ExpectedSprite e in fixture.sprites)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.path);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.texture_path);
                Require(sprite != null && texture != null && sprite.texture == texture, e.alias + " native texture binding");
                Require(catalog.FindSprite(e.alias) == sprite && sprite.name == e.name, e.alias + " runtime alias/name");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id) && id == 21300000, e.alias + " native local ID");
                Near(sprite.rect.x, e.rect.x, "rect.x"); Near(sprite.rect.y, e.rect.y, "rect.y");
                Near(sprite.rect.width, e.rect.width, "rect.width"); Near(sprite.rect.height, e.rect.height, "rect.height");
                Vector(sprite.pivot, e.pivot, "pivot"); Near(sprite.pixelsPerUnit, e.ppu, "ppu");
                Near(sprite.border.x, e.border.x, "border.x"); Near(sprite.border.y, e.border.y, "border.y");
                Near(sprite.border.z, e.border.z, "border.z"); Near(sprite.border.w, e.border.w, "border.w");
                Vector2[] vertices = sprite.vertices, uv = sprite.uv;
                Require(vertices.Length == e.vertices.Length && uv.Length == e.uv.Length, e.alias + " original mesh size");
                for (int i = 0; i < vertices.Length; i++) { Vector(vertices[i], e.vertices[i], "vertex"); Vector(uv[i], e.uv[i], "uv"); }
                Require(sprite.triangles.Select(value => (int)value).SequenceEqual(e.triangles), e.alias + " original indices");
                PhysicsShape[] shapes = e.physics_shapes ?? Array.Empty<PhysicsShape>();
                Require(sprite.GetPhysicsShapeCount() == shapes.Length, e.alias + " original physics shape count");
                var points = new List<Vector2>();
                for (int i = 0; i < shapes.Length; i++)
                {
                    sprite.GetPhysicsShape(i, points);
                    Require(points.Count == shapes[i].points.Length, e.alias + " physics shape point count");
                    for (int j = 0; j < points.Count; j++) Vector(points[j], shapes[i].points[j], "physics shape point");
                }
            }
            int mipCount = 0;
            foreach (ExpectedTexture e in fixture.textures)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
                Require(texture != null && texture.width == e.width && texture.height == e.height
                    && texture.mipmapCount == e.mip_count && (int)texture.format == e.format && texture.isReadable == e.readable,
                    e.path + " native dimensions/mips/format/readability");
                Require((int)texture.filterMode == e.filter && texture.anisoLevel == e.aniso
                    && (int)texture.wrapModeU == e.wrapU && (int)texture.wrapModeV == e.wrapV, e.path + " sampler");
                foreach (Mip mip in e.mips)
                {
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, mip.level);
                    request.WaitForCompletion();
                    Require(!request.hasError, e.path + " GPU readback " + mip.level);
                    using (var hash = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(hash.ComputeHash(request.GetData<byte>().ToArray())).Replace("-", "").ToLowerInvariant();
                        Require(actual == mip.sha256, e.path + " exact original decoded mip " + mip.level);
                    }
                    mipCount++;
                }
            }
            Debug.Log("INVENTORY_CURRENT_SPRITES_NATIVE_PASSED: " + fixture.sprites.Length + " Sprite bindings; "
                + fixture.textures.Length + " native textures; " + mipCount + " exact GPU mip readbacks.");
        }
        private static IEnumerator CheckPlay()
        {
            ConfigureViewport();
            for (int i = 0; i < 120 && (Screen.width != 540 || Screen.height != 960); i++)
            {
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                yield return null;
            }
            string root = Path.Combine(Work, "isolated-saves", Guid.NewGuid().ToString("N"));
            var creation = CharacterCreationOfflineService.OpenDefault(root);
            string slot = creation.CreateSlot();
            var draft = creation.OpenDraft(slot); draft.name = "行囊验证";
            Require(creation.TryCreateCharacter(slot, draft, out string error), error);
            Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
            var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            var assets = OfficialInventoryAssetCatalog.LoadDefault();
            var rules = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            var tabs = assets.LoadBagTabs();
            int[] items = tabs.SelectMany(tab => rules.Rows("item").Where(row => tab.ItemTypes.Contains(Convert.ToInt32(row["ItemType"]))
                && row.TryGetValue("CanOverlap", out object overlap) && Convert.ToBoolean(overlap)
                && row.TryGetValue("ShowInBag", out object visible) && Convert.ToBoolean(visible)
                && assets.FindItemSprite(Convert.ToInt32(row["ClassId"])) != null).Take(4))
                .Select(row => Convert.ToInt32(row["ClassId"])).Distinct().ToArray();
            Require(items.Length > 0, "Selected original stackable item icons");
            Require(profile.ApplyCharacterMutation(player =>
            {
                foreach (int item in items)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(row => row.ItemId == item);
                    if (stack == null) player.Inventory.Add(new OfflineItemStack { ItemId = item, Count = 23 });
                    else stack.Count = 23;
                }
            }, "inventory-art:seed").IsApplied, "Isolated native icon test inventory");
            var saved = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
            GameObject go = new GameObject("CurrentInventoryArtProbe", typeof(RectTransform));
            var runtime = go.AddComponent<InventoryFeatureRuntime>();
            runtime.OpenProfile(slot, root);
            foreach (BaseInputModule input in Object.FindObjectsOfType<BaseInputModule>()) input.enabled = false;
            yield return null; yield return null;
            Require(Screen.width == 540 && Screen.height == 960, "Owned portrait game viewport: " + Screen.width + "x" + Screen.height);
            var panel = go.GetComponent<OfficialInventoryPanelView>();
            var nodes = Field<Dictionary<string, RectTransform>>(panel, "nodes");
            var currentSprites = new HashSet<Sprite>(JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Work, "inventory-native-fixture.json")))
                .sprites.Select(row => assets.FindSprite(row.alias)));
            int category = 0, detailItem = 0;
            foreach (OfficialInventoryTab tab in tabs)
            {
                Toggle toggle = nodes[tab.Path].GetComponent<Toggle>();
                Require(toggle != null, "Original category toggle");
                if (!toggle.isOn) Click(toggle.gameObject);
                yield return null; yield return null;
                Require(Field<OfficialInventoryTab>(panel, "currentTab").Path == tab.Path, "Category pointer selection: " + tab.Path);
                var displayed = Field<List<OfflineInventoryItemView>>(panel, "displayedItems");
                foreach (Image image in go.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null) Require(currentSprites.Contains(image.sprite), "Rendered image uses selected native art: " + image.name);
                if (displayed.Any(row => items.Contains(row.ItemId)))
                {
                    Capture(go, "inventory-category-" + category + ".png");
                    detailItem = displayed.First(row => items.Contains(row.ItemId)).ItemId;
                    Require(panel.ReopenItemDetail(detailItem), "Native item detail opens");
                    yield return null; yield return null;
                    Capture(go, "inventory-detail-" + category + ".png");
                    panel.CloseItemDetail();
                    yield return null;
                }
                category++;
            }
            Require(detailItem > 0, "Native item/detail rendering coverage");
            bool closed = false; runtime.Closed += () => closed = true;
            Button close = go.GetComponentsInChildren<Button>(true).First(button => button.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 && button.gameObject.activeInHierarchy);
            Click(close.gameObject); yield return null; yield return null;
            Require(closed, "Original close pointer invokes the production event");
            Object.Destroy(go); yield return null;
            var reopened = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            Require(items.All(item => reopened.InventoryCount(item) == 23), "Cold profile recovery retains icon item counts");
            go = new GameObject("CurrentInventoryArtReopen", typeof(RectTransform));
            runtime = go.AddComponent<InventoryFeatureRuntime>(); runtime.OpenProfile(slot, root);
            foreach (BaseInputModule input in Object.FindObjectsOfType<BaseInputModule>()) input.enabled = false;
            yield return null; yield return null;
            Capture(go, "inventory-reopened.png");
            Require(saved.Count == Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length
                && saved.All(row => File.ReadAllBytes(row.Key).SequenceEqual(row.Value)), "UI browsing leaves the isolated save unchanged");
            Object.Destroy(go);
        }
        private static void ConfigureViewport()
        {
            // Reuse the existing host synchronization as well as its size preset.
            // It updates only the owned Editor surface, without focusing a window.
            typeof(Zjcs.Editor.Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 540, 960 });
        }
        private static void Click(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Original UI top pointer hit: " + target.name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void Capture(GameObject go, string name)
        {
            Canvas canvas = go.GetComponent<Canvas>();
            var cameraObject = new GameObject("InventoryArtCapture", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 24);
            var capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(color => color.r > 90 || color.g > 90 || color.b > 100) > 10000, "Native inventory UI render");
                File.WriteAllBytes(Path.Combine(Work, name), capture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                RenderTexture.active = active; camera.targetTexture = null;
                Object.Destroy(cameraObject); target.Release(); Object.Destroy(target); Object.Destroy(capture);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
