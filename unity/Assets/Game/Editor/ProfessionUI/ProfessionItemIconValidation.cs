using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionItemIconValidation
    {
        private const string Active = "Zjcs.ProfessionItemIcon.Active";
        [Serializable] private sealed class Fixture { public Item[] items; public Palette global_colors; public string prefab, config, catalog; }
        [Serializable] private sealed class FontFixture { public string config_path, config_asset, resource_catalog; public FontEntry[] entries; }
        [Serializable] private sealed class FontEntry { public string font_path; }
        [Serializable] private sealed class Item { public Info info; public string sprite_path, quality_path; }
        [Serializable] private sealed class Info { public int ClassId, Quality; public string RawIcon, IconEffect; public bool CanOverlap; }
        [Serializable] private sealed class ColorValue { public Color Color; }
        [Serializable] private sealed class ColorList { public int ThemeColorType; public int[] TypeList; public ColorValue[] TextColors; }
        [Serializable] private sealed class ColorConfig { public ColorList[] TextColorLists; }
        [Serializable] private sealed class Palette : IProfessionItemColors
        {
            public Color Green, White;
            public ColorConfig TextColorConfig;
            Color IProfessionItemColors.Green => Green;
            Color IProfessionItemColors.White => White;
            public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
            public Color GetTextColor(ThemeColorType theme, ColorType type)
            {
                foreach (var list in TextColorConfig.TextColorLists)
                    if (list.ThemeColorType == (int)theme)
                        for (int i = 0; i < list.TypeList.Length; i++)
                            if (list.TypeList[i] == (int)type) return list.TextColors[i].Color;
                throw new NullReferenceException("Original text color is absent.");
            }
        }
        private sealed class ItemInfo : IProfessionItemInfo
        {
            public Info Source;
            public int ClassId => Source.ClassId;
            public string Icon => Source.RawIcon;
            public string IconEffect => Source.IconEffect;
            public Quality Quality => (Quality)Source.Quality;
            public bool CanOverlap => Source.CanOverlap;
            public object GetFakeIcon() => throw new InvalidOperationException("Original reward Sprite must be bound; fake icon path is not expected.");
        }
        private sealed class Player : IProfessionItemPlayer
        {
            public int Subscribers;
            public long GetItemAmount(int id) => 0;
            public event Action<IProfessionItemData, long, object> OnItemCountUpdate
            { add { Subscribers++; } remove { Subscribers--; } }
            public bool CanGetItemFromMapLocation(object location) => throw Unexpected();
        }
        private sealed class Context : IProfessionItemIconContext
        {
            public ItemIconConfig Config { get; set; }
            public IProfessionItemColors Colors { get; set; }
            public readonly Player LocalPlayer = new Player();
            public IProfessionItemPlayer Player => LocalPlayer;
            public ItemInfo Item;
            public int PopupCalls;
            public UnityEngine.Object GrayConfig => throw Unexpected();
            public IProfessionItemInfo GetItemInfo(int id) { Require(id == Item.ClassId, "source item lookup"); return Item; }
            public object Compact(long count) { Require(count < 10000, "numeric probe scope"); return count.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            public object CultureText(string key) => throw Unexpected();
            public string GetItemNameKey(int id) => throw Unexpected();
            public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) { throw Unexpected(); }
            public bool CanMerge(int id, bool recursion) => throw Unexpected();
            public object GetLocationInfos(int id) => throw Unexpected();
            public void SetUIGray(UnityEngine.Object config, GameObject target, bool gray) => throw Unexpected();
            public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unexpected();
            public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All) => throw Unexpected();
            public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unexpected();
            public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback) => throw Unexpected();
            public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback)
            { Require(info == Item && owner is ItemIcon && required == 0, "native button dispatches original info popup arguments"); PopupCalls++; }
        }
        private sealed class Attachments : IItemIconAttachmentManager
        {
            public OfflineProfessionImageResourceLoader Loader;
            public int Borrowed, Returned;
            public GameObject GetAttachment(ItemIconAttachmentType type, Transform parent, bool active)
            {
                Require(type == ItemIconAttachmentType.HintTip, "source reward attachment scope"); Borrowed++;
                using (var op = Loader.LoadAssetByGuid<GameObject>("d019ea2d9f143514ebc8c30f545dca16"))
                { var go = Object.Instantiate(op.Result, parent, false); if (active) go.SetActive(true); return go; }
            }
            public void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell cell) { Returned++; Object.Destroy(cell.gameObject); }
        }
        public sealed class SimulatedMouse : BaseInput
        {
            public Vector2 Point; public bool Down, Held, Up;
            public override bool mousePresent => true;
            public override Vector2 mousePosition => Point;
            public override Vector2 mouseScrollDelta => Vector2.zero;
            public override bool GetMouseButtonDown(int button) => button == 0 && Down;
            public override bool GetMouseButtonUp(int button) => button == 0 && Up;
            public override bool GetMouseButton(int button) => button == 0 && Held;
            public override float GetAxisRaw(string name) => 0;
            public override bool GetButtonDown(string name) => false;
        }
        private static Fixture fixture;
        private static Camera camera;
        private static RenderTexture target;
        private static Transform slot;
        private static EventSystem events;
        private static SimulatedMouse mouse;
        private static ItemIcon icon;
        private static Context context;
        private static OfflineProfessionImageResourceLoader loader;
        private static FontAssetFontService fonts;
        private static Func<Image, IProfessionImageResourceLoader> previousProvider;
        private static Func<string, string> previousLanguage;
        private static bool ready, initialized;
        private static int index, phase, changedFrame;
        private static string Repository => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "../../.."));
        static ProfessionItemIconValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
            Require(Application.dataPath.Replace('\\', '/').Contains("/.tools/profession-ui-validation/unity/"), "isolated project");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ProfessionNativeTextureValidation.Run();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Reuse the existing editor gate for acceptance scenes that own
            // their scene and state; do not start the save-selection UI here.
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
            { Debug.LogError("Profession native ItemIcon validation timed out."); Finish(1); return; }
            if (!ready || !EditorApplication.isPlaying) return;
            try
            {
                if (!initialized) { Initialize(); Prepare(); return; }
                if (Time.frameCount <= changedFrame + (phase == 0 ? 3 : 0)) return;
                changedFrame = Time.frameCount;
                if (phase == 0) { CaptureAndAim(); phase = 1; }
                else if (phase == 1) { mouse.Down = true; mouse.Held = true; phase = 2; }
                else if (phase == 2) { mouse.Down = false; mouse.Held = false; mouse.Up = true; phase = 3; }
                else
                {
                    mouse.Up = false;
                    Require(context.PopupCalls == 1, "simulated mouse down/up reaches original ItemIcon listener exactly once");
                    Require(context.LocalPlayer.Subscribers == 1, "one original player-count subscription");
                    icon.Clear();
                    Require(context.LocalPlayer.Subscribers == 0 && icon.myInfo == null && icon.myItemData == null, "original Clear resets and unsubscribes");
                    Require(!icon.Button.enabled && !icon.ClickSound.enabled, "Clear disables both native button and click sound");
                    Debug.Log("Profession native reward " + fixture.items[index].info.ClassId + " rendered and simulated mouse popup dispatch passed.");
                    Object.Destroy(icon.gameObject);
                    index++;
                    if (index == fixture.items.Length) { Debug.Log("Profession native ItemIcon four-reward import/render/simulated-input/lifecycle validation passed; popup contents and complete profession panel are not covered."); Finish(0); }
                    else Prepare();
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true;
            fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Repository, ".tools/profession-native-ui/871_156598/typed/item-icon-fixture.json")));
            Require(fixture.items.Length == 4, "four actual source reward definitions");
            var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(fixture.catalog));
            var fontFixture = JsonUtility.FromJson<FontFixture>(File.ReadAllText(Path.Combine(Repository, ".tools/profession-native-ui/871_156598/typed/font-service-fixture.json")));
            var fontEntries = catalog.Entries.ToList();
            var fontCatalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(fontFixture.resource_catalog);
            fontEntries.AddRange(fontCatalog.Entries);
            var config = AssetDatabase.LoadAssetAtPath<OfflineProfessionFontConfig>(fontFixture.config_asset);
            Require(config != null && config.Entries.Length > 2, "complete original font configuration dictionary");
            var originalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/ProfessionUI/Native/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
            Require(originalFont != null && originalFont.atlasTextures.Length == 1 && originalFont.atlasTextures[0] != null, "original font native atlas binding");
            catalog.Entries = fontEntries.ToArray(); loader = new OfflineProfessionImageResourceLoader(catalog);
            fonts = new FontAssetFontService(loader, config.Deserialize);
            fonts.Initialize();
            Require(originalFont.sourceFontFile == AssetDatabase.LoadAssetAtPath<Font>(fontFixture.entries[0].font_path), "official source TTF bound by original font service");
            previousProvider = ProfessionImageResourceBridge.Provider; ProfessionImageResourceBridge.Provider = image => loader;
            previousLanguage = WorldMultiLanguageText.Localize; WorldMultiLanguageText.Localize = key => key == "ui_ItemIcon_count_key" ? "{0}" : key;
            var audio = new GameObject("OriginalItemAudioProbe").AddComponent<OfflineProfessionSoundHost>();
            audio.Bind(AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset"));
            new GameObject("OriginalItemProbeListener").AddComponent<AudioListener>();
            camera = new GameObject("OriginalItemProbeCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 128;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var canvasGo = new GameObject("OriginalItemProbeCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            slot = new GameObject("OriginalItemProbeSlot", typeof(RectTransform)).transform; slot.SetParent(canvas.transform, false);
            foreach (var previous in Object.FindObjectsOfType<EventSystem>(true))
            {
                previous.enabled = false;
                foreach (var input in previous.GetComponents<BaseInputModule>()) input.enabled = false;
            }
            var eventGo = new GameObject("OriginalItemProbeEvents", typeof(EventSystem), typeof(StandaloneInputModule)); events = eventGo.GetComponent<EventSystem>();
            mouse = eventGo.AddComponent<SimulatedMouse>(); var module = eventGo.GetComponent<StandaloneInputModule>(); module.inputOverride = mouse; module.forceModuleActive = true;
        }
        private static void Prepare()
        {
            phase = 0; changedFrame = Time.frameCount;
            var item = fixture.items[index];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fixture.prefab);
            Require(prefab != null && prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "all native item prefab scripts bound");
            var go = Object.Instantiate(prefab, slot, false); icon = go.GetComponent<ItemIcon>(); Require(icon != null, "original ItemIcon root binding");
            var config = AssetDatabase.LoadAssetAtPath<ItemIconConfig>(fixture.config);
            Require(config != null && config.QualityBgImages.Count == 8 && config.QualityBgImages[0] == config.QualityBgImages[1], "original ordered quality config and repeated white image");
            context = new Context { Config = config, Colors = fixture.global_colors, Item = new ItemInfo { Source = item.info } };
            foreach (var attachment in go.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(new Attachments { Loader = loader });
            icon.Bind(context); Require(icon.FrameEffectInfo.Count == 0, "original default has no frame effect SDK branch");
            icon.Clear(); icon.Init(item.info.ClassId); icon.SetCount(300L); icon.SetClickPopupInfoDialog(); go.SetActive(true);
            Require(icon.IconSprite == AssetDatabase.LoadAssetAtPath<Sprite>(item.sprite_path), "actual original reward Sprite bound");
            Require(icon.QualityImg.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(item.quality_path), "actual original quality background bound");
            Require(icon.myText.Text == "300" && icon.myTextGo.activeSelf, "native count text updated");
            var countText = icon.myText.GetComponent<TMP_Text>(); countText.ForceMeshUpdate();
            Require(countText.textInfo.characterCount == 3 && countText.textInfo.characterInfo.Take(3).All(c => c.fontAsset == countText.font), "all count glyphs rendered by original font without replacement fallback");
        }
        private static void CaptureAndAim()
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false, true); texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply(); RenderTexture.active = previous;
            Require(texture.GetPixels32().Count(p => p.a > 0) > 100, "original reward icon renders on GPU");
            File.WriteAllBytes(Path.Combine(Repository, "output/profession-ui-native-20261028/item-icon-" + fixture.items[index].info.ClassId + ".png"), texture.EncodeToPNG()); Object.Destroy(texture);
            var rect = (RectTransform)icon.transform; mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
            Debug.Log("Original item pointer=" + mouse.Point + ", screen=" + Screen.width + "x" + Screen.height + ", camera=" + camera.pixelRect + ", hits=" + string.Join(",", hits.Select(h => h.gameObject.name)) + ", graphics=" + string.Join(",", icon.GetComponentsInChildren<Graphic>(true).Select(g => g.name + ":" + g.raycastTarget + ":" + g.isActiveAndEnabled)));
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == icon.gameObject, "simulated pointer reaches the native item without occlusion");
        }
        private static InvalidOperationException Unexpected() => new InvalidOperationException("Reward probe entered a branch outside its source-backed scope.");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Profession native ItemIcon: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false); ProfessionImageResourceBridge.Provider = previousProvider; WorldMultiLanguageText.Localize = previousLanguage;
            fonts?.Dispose();
            if (camera != null) camera.targetTexture = null; if (target != null) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
    }
}
