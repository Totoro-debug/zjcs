using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.World.NativeLabelResources;
using Object = UnityEngine.Object;
using SimulatedMouse = Zjcs.Editor.ProfessionUI.ProfessionItemIconValidation.SimulatedMouse;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Values = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig.PropItemValue;
using Value = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage.PropValue;

namespace Zjcs.Editor.ProfessionUI
{
    // Original upgrade-property prefab and native callback-button graphics.
    // Extra preview/icon/button references below are explicit test fixtures;
    // this is not the full profession/entry/blessing popup application flow.
    [InitializeOnLoad]
    public static class ProfessionPropertyRowValidation
    {
        private const string Active = "Zjcs.ProfessionPropertyRow.Active";
        private const string Native = "Assets/Official/ProfessionUI/Native/";
        private static string Cache => Path.Combine(ProfessionValidationWorkspace.Repository, ".tools/source-packages/current/871_156598/profession-ui/typed");
        [Serializable] private sealed class Fixture { public string config, catalog; public ExpectedSprite[] sprites; public ExpectedTexture[] textures; }
        [Serializable] private sealed class SourceRect { public float x, y, width, height; }
        [Serializable] private sealed class ExpectedSprite
        {
            public string name, path; public SourceRect rect; public Vector2 pivot; public Vector4 border;
            public Vector2[] vertices, uv; public int[] triangles; public float ppu;
        }
        [Serializable] private sealed class ExpectedMip { public int level, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV; public float m_MipBias; }
        [Serializable] private sealed class ExpectedTexture { public string path; public int width, height, format; public Sampler settings; public ExpectedMip[] mips; }
        [Serializable] private sealed class FontFixture { public string config_asset, resource_catalog; }
        [Serializable] private sealed class Tables { public Property[] prop_cfg; public Entry[] entry; }
        [Serializable] private sealed class Property { public int PropType, EntryId; public bool Percent, Float; public string SmallIcon, Unit; }
        [Serializable] private sealed class Entry { public int Id; public string Icon; }
        private sealed class Language : ILanguageTypeService
        {
            public LanguageType CurrentLanguage { get; set; } = LanguageType.zh_CN;
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged;
        }
        private sealed class CountingLoader : IProfessionImageResourceLoader
        {
            private readonly IProfessionImageResourceLoader source;
            public int Loads, Disposed;
            public readonly List<string> Paths = new List<string>();
            public CountingLoader(IProfessionImageResourceLoader value) { source = value; }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object
            { Loads++; Paths.Add(path); return new Lease<T>(source.LoadAsset<T>(path), () => Disposed++); }
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => LoadAsset<T>(path);
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object => throw new InvalidOperationException("Unexpected GUID property request");
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => LoadAssetByGuid<T>(guid);
            private sealed class Lease<T> : IProfessionAssetOperation<T> where T : Object
            {
                private readonly IProfessionAssetOperation<T> value; private Action dispose;
                public Lease(IProfessionAssetOperation<T> value, Action done) { this.value = value; dispose = done; }
                public bool IsValid => value.IsValid; public int Status => value.Status; public T Result => value.Result;
                public event Action<IProfessionAssetOperation<T>> Completed { add { value(this); } remove { } }
                public void Dispose() { Require(dispose != null, "no double resource release"); value.Dispose(); var done = dispose; dispose = null; done(); }
            }
        }
        private static Fixture fixture;
        private static Tables tables;
        private static LocalizationBinaryTextService text;
        private static OfflineProfessionPropertyBindings bindings;
        private static IProfessionPropertyBindings previous;
        private static CountingLoader counted;
        private static FontAssetFontService fonts;
        private static Func<string, string> previousText;
        private static Func<TMP_FontAsset> previousFont;
        private static GameObject root, inactive;
        private static Camera camera;
        private static RenderTexture target;
        private static EventSystem events;
        private static SimulatedMouse mouse;
        private static ProfessionPlayerPropItem row, fromRow, toRow;
        private static ProfessionBlessPropBtn bless;
        private static Button detailButton, blessingButton;
        private static int errors, phase, frame, clickPhase, entryPopups, blessingPopups;
        private static bool ready, initialized, shuttingDown;
        private static string lastBless;
        private static bool lastEffect;
        private static int lastRank;
        static ProfessionPropertyRowValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                typeof(ProfessionUpgradePanelValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true; SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true; Application.logMessageReceived += Log;
            Require(Screen.width == 1080 && Screen.height == 1920, "portrait Game View dimensions");
            fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Cache, "property-rows-fixture.json")));
            ValidateNative();
            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(fixture.config); tables = JsonUtility.FromJson<Tables>(config.text);
            Require(tables.prop_cfg.Length == 352 && tables.entry.Length == 891, "complete original property/entry tables");
            var languageRoot = Path.Combine(Application.streamingAssetsPath, "Map13/NativeText");
            HashFile(Path.Combine(languageRoot, "launch-zh_CN.bytes"), "a068ee47db184c96c8923172a104ed90b37f72e2b4adb5d73211d894d7ec9001");
            HashFile(Path.Combine(languageRoot, "game-zh_CN.bytes"), "7e8be8308de8c611097b37b116c697510827379b1d6f7aca99949d1e05832f24");
            text = new LocalizationBinaryTextService(new Language(), null, new ILanguageTextDataProvider[] { new LaunchLanguageBinaryTextDataProvider("Assets/Launch/") }, new LocalLanguageFiles(Application.streamingAssetsPath));
            text.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/")); LanguageManager.Initialize(text);
            var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(fixture.catalog));
            var font = JsonUtility.FromJson<FontFixture>(File.ReadAllText(Path.Combine(Cache, "font-service-fixture.json")));
            catalog.Entries = catalog.Entries.Concat(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(font.resource_catalog).Entries).ToArray();
            var loader = new OfflineProfessionImageResourceLoader(catalog); counted = new CountingLoader(loader);
            fonts = new FontAssetFontService(loader, AssetDatabase.LoadAssetAtPath<OfflineProfessionFontConfig>(font.config_asset).Deserialize); fonts.Initialize();
            var originalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Native + "TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
            previousText = WorldMultiLanguageText.Localize; WorldMultiLanguageText.Localize = key => text.GetText(key, LanguageType.zh_CN);
            previousFont = WorldMultiLanguageText.LanguageFont; WorldMultiLanguageText.LanguageFont = () => originalFont;
            bindings = new OfflineProfessionPropertyBindings(config, counted, text,
                (owner, path, name, desc, x) => { Require(owner == row && path == tables.entry.First(e => e.Id == 26).Icon && name.Key == "entry_26_name" && desc.Key == "entry_26_desc" && (string)desc.Args[0] == "argument" && !x, "original entry popup payload/order"); entryPopups++; },
                (owner, rank, desc, effect, key, season) => { Require(owner == bless && key == "ui_bless_prop_not_effect" && season == "ui_daily_equip_suit_extra_rank_desc", "original blessing popup keys/owner"); lastRank = rank; lastBless = desc; lastEffect = effect; blessingPopups++; });
            previous = ProfessionPropertyBindings.Current; ProfessionPropertyBindings.Current = bindings;
            inactive = new GameObject("PropertyValidationInactivePool"); inactive.SetActive(false);
            camera = new GameObject("NativePropertyCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.88f, .88f, .88f);
            target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            root = new GameObject("NativePropertyFixtureCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            row = MakeRow(300); fromRow = MakeRow(200); toRow = MakeRow(100);
            ValidateValuesAndRows();
            foreach (var existing in Object.FindObjectsOfType<EventSystem>(true)) { existing.enabled = false; foreach (var module in existing.GetComponents<BaseInputModule>()) module.enabled = false; }
            var input = new GameObject("NativePropertyInput", typeof(EventSystem), typeof(StandaloneInputModule)); events = input.GetComponent<EventSystem>();
            mouse = input.AddComponent<SimulatedMouse>(); input.GetComponent<StandaloneInputModule>().inputOverride = mouse;
            detailButton = MakeButton(-150); blessingButton = MakeButton(-350);
            row.gameObject.SetActive(false); row.PropDetailBtn = detailButton; row.gameObject.SetActive(true);
            row.Init(PropType.Attack, 60, arg: "argument");
            blessingButton.gameObject.SetActive(false); bless = blessingButton.gameObject.AddComponent<ProfessionBlessPropBtn>();
            Set(bless, "mOpenBtn", blessingButton); bless.Configure(bindings); blessingButton.gameObject.SetActive(true);
            bless.Init(3, 4, PropType.Attack, 60, 5, true);
            frame = Time.frameCount;
        }
        private static ProfessionPlayerPropItem MakeRow(float y)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionUpgradePropItem.prefab");
            var go = Object.Instantiate(prefab, inactive.transform, false); var result = go.GetComponent<ProfessionPlayerPropItem>(); result.Configure(bindings);
            go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(0, y, 0); go.SetActive(true); return result;
        }
        private static Button MakeButton(float y)
        {
            var native = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionPanel.prefab").GetComponentInChildren<ProfessionUpgradePanel>(true).BtnUpgrade;
            var go = Object.Instantiate(native.gameObject, inactive.transform, false);
            // Isolate a native button's input/graphics for dispatch checks.
            foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(script is Button || script is Graphic || script is WorldMultiLanguageText)) Object.DestroyImmediate(script);
            go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(0, y, 0); go.SetActive(true); return go.GetComponent<Button>();
        }
        private static void ValidateValuesAndRows()
        {
            Require(bindings.Text("PropType.Attack") == "攻击", "original Chinese property text");
            Require(ProfessionPropertyLanguage.FormatPropString(PropType.Attack, -123456) == "12.3万", "source absolute compact number");
            Require(ProfessionPropertyLanguage.FormatPropString(PropType.AttackPercent, 1400) == "14%", "source fixed-point percent");
            Require(ProfessionPropertyLanguage.FormatPropString(PropType.Attack, 60, true, true) == "攻击+60" + bindings.Text(bindings.GetProperty(PropType.Attack).Unit), "original name/sign/unit order");
            Require(ProfessionPropertyFormula.DoubleToString(.01234) == "0.012" && ProfessionPropertyFormula.DoubleToString(.12345) == "0.12", "source float precision boundary");
            var current = new Values { PropValue = new Value(PropType.Attack, 100), ExtraPropValue = new Value(PropType.Attack, 6) };
            var next = new Values { PropValue = new Value(PropType.Attack, 100), ExtraPropValue = new Value(PropType.Attack, 5) };
            Require(next.IsPropSameWith(current) == (true, false), "preserve original main-value versus previous-extra comparison");
            var delta = next - current; Require(delta.PropValue == null && delta.ExtraPropValue.PropVal == -1 && !delta.IsEmpty(), "original compound subtraction and zero omission");
            Require(new Values().IsEmpty() && new Values { ExtraPropValue = new Value(PropType.Attack, 5) }.ToSubString() == "5", "empty and extra-only subtraction display");
            row.DefaultTextColor = new Color32(17, 34, 51, 255); row.PreviewTextColor = new Color32(170, 0, 0, 255);
            row.PropValueFromText = fromRow.myPropValueText; row.PropValueToText = toRow.myPropValueText;
            row.Init(PropType.Attack, current, next);
            Require(toRow.myPropValueText.Text == "<color=#112233>100</color><color=#AA0000>+5</color>", "original independent main/extra preview colors");
            row.PropValueFromText = null; row.PropValueToText = null;
            row.Init("value", 1, 0); Require(row.myPropValueText.gameObject.activeSelf, "original untyped long overload ignores zero preview");
            row.Init(PropType.Attack, 1, 0); Require(!row.myPropValueText.gameObject.activeSelf, "typed zero preview is retained");
            row.Init(new ProfessionComplexProp { PropType = PropType.Attack, CurValueObj = new Value(PropType.Attack, 123451), NextValueObj = new Value(PropType.Attack, 123452) });
            Require(row.myPropValueText.gameObject.activeSelf, "formatted equality suppresses unchanged compact preview");
            row.ShowSign = true; row.Init(PropType.Attack, -60); Require(row.myPropValueText.Text == bindings.TextFormatted("ui_common_prop_plus", "60"), "original typed absolute/sign branch"); row.ShowSign = false;
            row.UseOneText = true; row.Init(PropType.Attack, 60, formatKey: "{0} {1}"); Require(row.myPropValueText.Text == "攻击 60", "one-text localized property name"); row.UseOneText = false;
            bool overflow = false; try { ProfessionComplexProp.MakePropValueObj(PropType.Attack, long.MinValue); } catch (OverflowException) { overflow = true; } Require(overflow, "original Math.Abs overflow contract");
            var resourceRow = MakeRow(0); resourceRow.myIcon = resourceRow.myBg;
            foreach (var item in tables.prop_cfg)
            {
                resourceRow.Init((PropType)item.PropType, 123456);
                string address = "Assets/Image/" + item.SmallIcon;
                Require(counted.Paths.Last() == address, "original typed LoadSprite path");
                Require(resourceRow.myIcon.sprite != null == !string.IsNullOrEmpty(item.SmallIcon), "all original configured property icon bindings");
            }
            resourceRow.gameObject.SetActive(false); Require(counted.Loads == 352 && counted.Disposed == 0, "resource bag retained across repeated Init and disable");
            Object.DestroyImmediate(resourceRow.gameObject); Require(counted.Disposed == 352, "all source icon handles released once on destruction");
            row.Init(PropType.Attack, 123456); fromRow.Init(PropType.Defence, 300); toRow.Init(PropType.MaxHp, 3000);
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Property validation timed out");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount <= frame + 2) return; frame = Time.frameCount;
                if (phase == 0) { Capture(); phase++; }
                else if (phase == 1) { if (!Click(detailButton)) return; Require(entryPopups == 1, "simulated original row entry dispatch"); phase++; }
                else if (phase == 2)
                {
                    if (!Click(blessingButton)) return;
                    Require(blessingPopups == 1 && lastRank == 3 && !lastEffect && lastBless == ProfessionBlessDescription.GetBlessDesc(bindings.TextFormatted("ui_equip_attribuate_prop_ PropArg", "攻击", "60"), "5", true, false), "rank mismatch and original blessing description");
                    bless.SetCanClick(false); phase++;
                }
                else if (phase == 3) { if (!Click(blessingButton)) return; Require(blessingPopups == 1, "original click lock"); bless.SetCanClick(true); bless.Init(3, 3, PropType.Attack, 60, 5, false); phase++; }
                else if (phase == 4)
                {
                    if (!Click(blessingButton)) return; Require(blessingPopups == 1, "no-blessing click does not dispatch");
                    bless.Init(4, "PropType.Attack", new Values { PropValue = new Value(PropType.Attack, 60) }, new Values { ExtraPropValue = new Value(PropType.Attack, 5) }, false);
                    blessingButton.gameObject.SetActive(false); blessingButton.gameObject.SetActive(true); phase++;
                }
                else if (phase == 5)
                {
                    if (!Click(blessingButton)) return; Require(blessingPopups == 2 && lastRank == 4 && lastEffect, "compound blessing uses sub-value emptiness and reenable has one listener");
                    row.gameObject.SetActive(false); row.gameObject.SetActive(true); row.Init("none", "1"); phase++;
                }
                else if (phase == 6)
                {
                    if (!Click(detailButton)) return; Require(entryPopups == 1, "None property skips original entry dispatch");
                    row.Init(PropType.Attack, 60, arg: "argument"); phase++;
                }
                else if (phase == 7)
                {
                    if (!Click(detailButton)) return; Require(entryPopups == 2 && errors == 0, "reenable restores one original detail listener and no runtime errors");
                    Debug.Log("Original typed property Play Mode passed: full original property/entry configuration, 40 native Sprite meshes and two full GPU textures, actual Chinese binary/culture formatting, signed/compact/percent/float/preview/compound color branches, original zero/sentinel/overflow behavior, all 352 icon requests retained until destruction, and simulated native-button entry/blessing dispatch/lock/absence/rank/re-enable checks. Preview/icon/button references are test fixtures; complete popup content and profession/player/save/Windows flows remain incomplete."); Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static bool Click(Button button)
        {
            if (clickPhase == 0)
            {
                Canvas.ForceUpdateCanvases(); mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position);
                var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
                Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "native callback button is unoccluded; phase=" + phase + "; point=" + mouse.Point + "; hits=" + string.Join(",", hits.Select(h => h.gameObject.name)) + "; targets=" + string.Join(",", button.GetComponentsInChildren<Graphic>(true).Select(g => g.name + ":" + g.raycastTarget)) + "; interactable=" + button.IsInteractable()); clickPhase++; return false;
            }
            if (clickPhase == 1) { mouse.Down = mouse.Held = true; clickPhase++; return false; }
            if (clickPhase == 2) { mouse.Down = mouse.Held = false; mouse.Up = true; clickPhase++; return false; }
            mouse.Up = false; clickPhase = 0; return true;
        }
        private static void ValidateNative()
        {
            foreach (var e in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
                Require(texture && texture.width == e.width && texture.height == e.height && texture.mipmapCount == e.mips.Length && (int)texture.format == e.format, "original texture dimensions/format/mips");
                Require((int)texture.filterMode == e.settings.m_FilterMode && texture.anisoLevel == e.settings.m_Aniso && (int)texture.wrapModeU == e.settings.m_WrapU && (int)texture.wrapModeV == e.settings.m_WrapV && Math.Abs(texture.mipMapBias - e.settings.m_MipBias) < .000001f, "original sampler");
                foreach (var mip in e.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level); request.WaitForCompletion(); Require(!request.hasError, "GPU readback");
                    var bytes = request.GetData<byte>().ToArray(); Require(bytes.Length == mip.rgba_size && Hash(bytes) == mip.rgba_sha256, "all original mip pixels on GPU");
                }
            }
            foreach (var e in fixture.sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.path); Require(sprite && sprite.name == e.name && sprite.texture, "native Sprite binding");
                Near(sprite.rect.x, e.rect.x); Near(sprite.rect.y, e.rect.y); Near(sprite.rect.width, e.rect.width); Near(sprite.rect.height, e.rect.height); Near(sprite.pixelsPerUnit, e.ppu);
                Near(sprite.pivot.x, e.pivot.x); Near(sprite.pivot.y, e.pivot.y);
                for (int i = 0; i < 4; i++) Near(sprite.border[i], e.border[i]);
                var vertices = sprite.vertices; var uv = sprite.uv; Require(vertices.Length == e.vertices.Length && uv.Length == e.uv.Length, "native Sprite vertex/UV lengths");
                for (int i = 0; i < vertices.Length; i++) { Near(vertices[i].x, e.vertices[i].x); Near(vertices[i].y, e.vertices[i].y); Near(uv[i].x, e.uv[i].x); Near(uv[i].y, e.uv[i].y); }
                Require(sprite.triangles.Select(v => (int)v).SequenceEqual(e.triangles), "original native triangles");
            }
        }
        private static void Capture()
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1080, 1920, TextureFormat.RGBA32, false, true); image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); image.Apply(); RenderTexture.active = old;
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("property-rows-native.png"), image.EncodeToPNG()); Object.Destroy(image);
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void HashFile(string path, string expected) => Require(Hash(File.ReadAllBytes(path)) == expected, "selected original Chinese binary hash");
        private static void Near(float value, float expected) => Require(Math.Abs(value - expected) < .000001f, "native Sprite scalar");
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Profession property: " + message); }
        private static void Log(string message, string trace, LogType type) { if (!shuttingDown && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++; }
        private static void Finish(int result)
        {
            shuttingDown = true; SessionState.SetBool(Active, false); Application.logMessageReceived -= Log;
            if (root) Object.DestroyImmediate(root); if (inactive) Object.DestroyImmediate(inactive);
            fonts?.Dispose(); text?.Dispose(); ProfessionPropertyBindings.Current = previous;
            WorldMultiLanguageText.Localize = previousText; WorldMultiLanguageText.LanguageFont = previousFont;
            if (camera) camera.targetTexture = null; if (target) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(result);
        }
    }
}
