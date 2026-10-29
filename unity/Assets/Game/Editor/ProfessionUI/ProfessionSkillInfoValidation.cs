using System;
using System.Collections.Generic;
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
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using SimulatedMouse = Zjcs.Editor.ProfessionUI.ProfessionItemIconValidation.SimulatedMouse;

namespace Zjcs.Editor.ProfessionUI
{
    // Service/player data are fixtures. Icon, attachment, slot, state, animation,
    // text, material, mesh and input components are actual migrated originals.
    [InitializeOnLoad]
    public static class ProfessionSkillInfoValidation
    {
        private const string Active = "Zjcs.ProfessionSkillInfo.Active";
        private const string Native = "Assets/Official/ProfessionUI/Native/";
        [Serializable] private sealed class Data { public Item[] items; public RankRow[] ranks; public ProfessionRow[] professions; public Skin[] skins; }
        [Serializable] private sealed class Item : IProfessionInfoSkill
        {
            public int ClassId, ItemType, Rank, Quality;
            public string RawIcon, IconEffect;
            public bool CanOverlap;
            int IProfessionItemInfo.ClassId => ClassId;
            int IProfessionInfoSkill.ItemType => ItemType;
            int IProfessionInfoSkill.Rank => Rank;
            string IProfessionItemInfo.Icon => RawIcon;
            string IProfessionItemInfo.IconEffect => IconEffect;
            Quality IProfessionItemInfo.Quality => (Quality)Quality;
            bool IProfessionItemInfo.CanOverlap => CanOverlap;
            public object GetFakeIcon() => throw Unexpected();
        }
        [Serializable] private sealed class RankRow { public int Rank, RankAddition, Quality; }
        [Serializable] private sealed class ProfessionRow { public int Profession; public int[] Skills; }
        [Serializable] private sealed class Skin { public int ClassId, SkillId; }
        [Serializable] private sealed class Fixture { public ExpectedSprite[] sprites; public ExpectedTexture[] textures; }
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
        [Serializable] private sealed class PaletteFixture { public Palette global_colors; }
        [Serializable] private sealed class ColorValue { public Color Color; }
        [Serializable] private sealed class ColorList { public int ThemeColorType; public int[] TypeList; public ColorValue[] TextColors; }
        [Serializable] private sealed class ColorConfig { public ColorList[] TextColorLists; }
        [Serializable] private sealed class Palette : IProfessionItemColors
        {
            public Color Green, White; public ColorConfig TextColorConfig;
            Color IProfessionItemColors.Green => Green;
            Color IProfessionItemColors.White => White;
            public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
            public Color GetTextColor(ThemeColorType theme, ColorType type)
            {
                foreach (var row in TextColorConfig.TextColorLists)
                    if (row.ThemeColorType == (int)theme)
                        for (int i = 0; i < row.TypeList.Length; i++)
                            if (row.TypeList[i] == (int)type) return row.TextColors[i].Color;
                throw Unexpected();
            }
        }
        private sealed class Owned : IProfessionItemData, IProfessionSkillParameter
        {
            public Item Item;
            public int Rank, Skin;
            public int Id => Item.ClassId;
            public long Count => 1;
            public IProfessionItemInfo Info => Item;
            public Quality GetQuality() => (Quality)data.ranks.Single(r => r.Rank == Rank).Quality;
            public bool CanExpire() => false;
            public int GetRank() => Rank;
            public int SkillSkinId => Skin;
        }
        private sealed class Services : IProfessionSkillIconBindings, IProfessionSkillIconPlayer, IProfessionItemIconContext, IProfessionItemPlayer
        {
            public IProfessionSkillIconPlayer MainPlayer => this;
            public IProfessionItemPlayer Player => this;
            public ItemIconConfig Config { get; set; }
            public IProfessionItemColors Colors { get; set; }
            public UnityEngine.Object GrayConfig => throw Unexpected();
            public int Subscribers, CountSubscribers, DebugSubscribers, Popups, TypedPopups, Borrows, Returns, Creates;
            public bool DebugShowSkillRank { get; private set; }
            private Action debugChanged;
            private Action<IProfessionItemData, int, int> rankUp, rankDown;
            private Action<Dictionary<int, int>, Dictionary<int, int>> rankBatch;
            private Action<int, int> skinEquipped;
            public Owned Learned;
            public IProfessionItemData LastPopup;
            public IProfessionItemInfo LastPopupInfo;
            public object LastProvider;
            public event Action DebugShowSkillRankChanged { add { debugChanged += value; DebugSubscribers++; } remove { debugChanged -= value; DebugSubscribers--; } }
            public event Action<IProfessionItemData, int, int> OnSkillRankUp { add { rankUp += value; Subscribers++; } remove { rankUp -= value; Subscribers--; } }
            public event Action<IProfessionItemData, int, int> OnSkillRankDown { add { rankDown += value; Subscribers++; } remove { rankDown -= value; Subscribers--; } }
            public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch { add { rankBatch += value; Subscribers++; } remove { rankBatch -= value; Subscribers--; } }
            public event Action<int, int> OnSkillSkillEquiped { add { skinEquipped += value; Subscribers++; } remove { skinEquipped -= value; Subscribers--; } }
            public event Action<IProfessionItemData, long, object> OnItemCountUpdate { add { CountSubscribers++; } remove { CountSubscribers--; } }
            public void EmitRank(bool batch = false, bool down = false, bool unrelated = false)
            {
                int before = Learned.Rank;
                if (!unrelated) Learned.Rank = down ? 2 : before + 1;
                var item = unrelated ? new Owned { Item = data.items.First(i => i.ClassId != Learned.Id), Rank = 1 } : Learned;
                if (batch) rankBatch?.Invoke(new Dictionary<int, int> { [item.Id] = item.GetRank() }, new Dictionary<int, int> { [item.Id] = before });
                else if (down) rankDown?.Invoke(item, before, item.GetRank());
                else rankUp?.Invoke(item, before, item.GetRank());
            }
            public void SetDebug(bool enabled) { DebugShowSkillRank = enabled; debugChanged?.Invoke(); }
            public void EmitSkin(int id, int skin) { Learned.Skin = skin; skinEquipped?.Invoke(id, skin); }
            public bool HasItem(int id, long amount) => Learned != null && Learned.Id == id && amount <= 1;
            public IProfessionItemData GetItemById(int id) { Require(HasItem(id, 1), "owned lookup"); return Learned; }
            public bool ContainsItemAtIndex(IProfessionItemData item) => item == Learned;
            public bool IsSkillAvailableForCurProfession(int id) => id == data.items[0].ClassId;
            public IProfessionSkillParameter GetItemParamSkill(IProfessionItemData item) => item as Owned;
            public ProfessionSkillRankDefinition GetRank(int rank)
            { var row = data.ranks.Single(r => r.Rank == rank); return new ProfessionSkillRankDefinition { Quality = (Quality)row.Quality, RankAddition = row.RankAddition }; }
            public int GetMaxRankByQuality(Quality quality) => data.ranks.Where(r => r.Quality == (int)quality).Select(r => r.RankAddition).DefaultIfEmpty().Max();
            public int GetDebugProfessionRank(int id) => 3; // explicit debug-display fixture
            public bool IsSkillAvailableForProfession(int id, int profession) => data.professions.Single(p => p.Profession == profession).Skills.Contains(id);
            public bool IsPetSkill(IProfessionItemInfo info) => false;
            public bool IsPlayerTarget(int targetType) => targetType == 1;
            public bool IsSkillGMDisabled(int id) => false;
            public bool HasSkillSkin(int skinId) => data.skins.Any(s => s.ClassId == skinId);
            public IProfessionItemInfo GetItemInfo(int id) => data.items.Single(i => i.ClassId == id);
            public void PopupSkillDetail(IProfessionItemData item, object provider) { TypedPopups++; LastPopup = item; LastProvider = provider; }
            public void PopupSkillDetail(int id, long count) { TypedPopups++; LastPopupInfo = GetItemInfo(id); Require(count == 0, "unowned typed popup count"); }
            public ItemIconSkill GetIcon(int id, Transform slot) { Borrows++; var icon = pool.Borrow(slot); icon.Init(id); return icon; }
            public ItemIconSkill GetIcon(IProfessionItemData item, Transform slot) { Borrows++; var icon = pool.Borrow(slot); icon.Init(item); return icon; }
            public void RecycleIcon(ItemIconSkill icon) { Returns++; pool.Return(icon); }
            public long GetItemAmount(int id) => HasItem(id, 1) ? 1 : 0;
            public bool CanGetItemFromMapLocation(object location) => throw Unexpected();
            public object Compact(long count) => count.ToString();
            public object CultureText(string key) => throw Unexpected();
            public string GetItemNameKey(int id) => throw Unexpected();
            public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) { sources = null; return false; }
            public bool CanMerge(int id, bool recursion) => throw Unexpected();
            public object GetLocationInfos(int id) => throw Unexpected();
            public void SetUIGray(UnityEngine.Object config, GameObject target, bool gray) => throw Unexpected();
            public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unexpected();
            public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All) => throw Unexpected();
            public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unexpected();
            public void PopupInfoDialog(IProfessionItemData item, IWindowOperator owner, long required, Action<GameObject> callback)
            { Popups++; LastPopup = item; Require(required == 0 && owner is ItemIconSkill, "owned original popup arguments"); }
            public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback)
            { Popups++; LastPopupInfo = info; Require(required == 0 && owner is ItemIconSkill, "unowned original popup arguments"); }
        }
        private sealed class Information : IProfessionInfoBindings
        {
            public ProfessionDefinition Target;
            public IProfessionImageResourceLoader ResourceLoader => loader;
            public ProfessionDetailDefinition GetProfession(int profession) => new ProfessionDetailDefinition { Profession = profession, Skills = Target.Ids };
            public string GetProfessionDescKey(int profession) => throw Unexpected();
            public List<int> GetSupportWeaponTypes(int profession) => throw Unexpected();
            public List<int> GetSupportSecondWeaponTypes(int profession) => throw Unexpected();
            public string GetEquipTypeIcon(int itemType) => throw Unexpected();
            public string GetEquipTypeNameKey(int itemType) => throw Unexpected();
            public IProfessionInfoSkill GetItemInfo(int id) => (IProfessionInfoSkill)services.GetItemInfo(id);
            public IReadOnlyDictionary<int, long> GetFeatureProps(int profession) => throw Unexpected();
            public Color GetRadarColor(int profession) => throw Unexpected();
            public void InitPropItem(ProfessionPlayerPropItem item, int type, long value) => throw Unexpected();
        }
        private sealed class ProfessionDefinition { public List<int> Ids; }
        private static Data data;
        private static Services services;
        private static GameObjectPool<ItemIconSkill> pool;
        private static ItemIconAttachmentManager attachments;
        private static FontAssetFontService fonts;
        private static OfflineProfessionImageResourceLoader loader;
        private static GameObject root, row;
        private static ProfessionSkillSlot slot;
        private static ProfessionInfoPanelSkillItem itemView;
        private static ItemIconSkill current;
        private static Camera camera;
        private static RenderTexture target;
        private static EventSystem events;
        private static SimulatedMouse mouse;
        private static bool ready, initialized;
        private static int frame, phase, index;
        private static Func<Image, IProfessionImageResourceLoader> previousImages;
        private static Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        private static Func<string, string> previousLanguage;
        private static object detailProvider;
        private static string Cache => Path.Combine(ProfessionValidationWorkspace.Repository, ".tools/source-packages/current/871_156598/profession-ui/typed");
        static ProfessionSkillInfoValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateNative();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true; EditorApplication.isPaused = false;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 240));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void ValidateNative()
        {
            var expected = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Cache, "skill-icons-fixture.json")));
            long gpuBytes = 0;
            foreach (var e in expected.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
                Require(texture != null && texture.width == e.width && texture.height == e.height && texture.mipmapCount == e.mips.Length && (int)texture.format == e.format, "source texture dimensions/format/mips");
                Require((int)texture.filterMode == e.settings.m_FilterMode && texture.anisoLevel == e.settings.m_Aniso &&
                    (int)texture.wrapModeU == e.settings.m_WrapU && (int)texture.wrapModeV == e.settings.m_WrapV &&
                    Math.Abs(texture.mipMapBias - e.settings.m_MipBias) < .000001f, "source sampler settings");
                foreach (var mip in e.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level); request.WaitForCompletion();
                    Require(!request.hasError, "texture GPU upload/readback");
                    var bytes = request.GetData<byte>().ToArray();
                    using (var hash = SHA256.Create())
                        Require(bytes.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256, "exact original mip bytes on GPU: " + e.path);
                    gpuBytes += bytes.Length;
                }
            }
            foreach (var e in expected.sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.path);
                Require(sprite != null && sprite.name == e.name && sprite.texture != null, "native Sprite identity/import");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id) && id == 21300000, "source Sprite object ID");
                Near(sprite.rect.x, e.rect.x); Near(sprite.rect.y, e.rect.y); Near(sprite.rect.width, e.rect.width); Near(sprite.rect.height, e.rect.height);
                Near(sprite.pixelsPerUnit, e.ppu); Vector(sprite.pivot, e.pivot);
                Near(sprite.border.x, e.border.x); Near(sprite.border.y, e.border.y); Near(sprite.border.z, e.border.z); Near(sprite.border.w, e.border.w);
                var vertices = sprite.vertices; var uv = sprite.uv;
                Require(vertices.Length == e.vertices.Length && uv.Length == e.uv.Length, "source native mesh lengths");
                for (int i = 0; i < vertices.Length; i++) { Vector(vertices[i], e.vertices[i]); Vector(uv[i], e.uv[i]); }
                Require(sprite.triangles.Select(v => (int)v).SequenceEqual(e.triangles), "source native triangles/order");
            }
            Debug.Log("Profession skill resources validated: " + expected.sprites.Length + " native Sprite meshes/UVs; " + expected.textures.Length + " full textures; " + gpuBytes + " exact GPU mip bytes.");
        }
        private static void Initialize()
        {
            initialized = true;
            data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("ProfessionUI/info-config").text);
            Require(data.items.Length == 412 && data.professions.Length == 31, "complete selected original skill/skin data");
            services = new Services { Config = AssetDatabase.LoadAssetAtPath<ItemIconConfig>(Native + "ScriptableConfig/Modules/ItemIcon/ItemIconConfig.asset"),
                Colors = JsonUtility.FromJson<PaletteFixture>(File.ReadAllText(Path.Combine(Cache, "item-icon-fixture.json"))).global_colors };
            var catalog = Object.Instantiate(Resources.Load<OfflineProfessionImageCatalog>("ProfessionUI/skill-images"));
            var font = JsonUtility.FromJson<FontFixture>(File.ReadAllText(Path.Combine(Cache, "font-service-fixture.json")));
            catalog.Entries = catalog.Entries.Concat(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(font.resource_catalog).Entries)
                .Concat(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>("Assets/Official/ProfessionUI/Catalogs/ProfessionItemImages.asset").Entries).ToArray();
            loader = new OfflineProfessionImageResourceLoader(catalog);
            previousImages = ProfessionImageResourceBridge.Provider; ProfessionImageResourceBridge.Provider = image => loader;
            previousPool = ProfessionGameObjectPoolBridge.Provider;
            ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, world) => Object.Instantiate(prefab, parent, world);
            previousLanguage = WorldMultiLanguageText.Localize;
            // Numeric localization fixture; no replacement game translation.
            WorldMultiLanguageText.Localize = key => key == "ui_ItemIcon_not_got" ? "0" : key == "ui_common_level_style_3" || key == "ui_ItemIcon_count_key" ? "{0}" : key;
            fonts = new FontAssetFontService(loader, AssetDatabase.LoadAssetAtPath<OfflineProfessionFontConfig>(font.config_asset).Deserialize); fonts.Initialize();
            var sound = new GameObject("SkillInfoFixtureAudio").AddComponent<OfflineProfessionSoundHost>();
            sound.Bind(AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset"));
            new GameObject("SkillInfoFixtureListener").AddComponent<AudioListener>();
            attachments = new ItemIconAttachmentManager(loader, () => AssetDatabase.LoadAssetAtPath<ItemIconAttachmentConfig>(Native + "ScriptableConfig/Modules/ItemIcon/ItemIconAttachmentConfig.asset"), new GAction());
            camera = new GameObject("SkillInfoFixtureCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 180;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            root = new GameObject("SkillInfoFixtureCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            foreach (var existing in Object.FindObjectsOfType<EventSystem>(true))
            { existing.enabled = false; foreach (var module in existing.GetComponents<BaseInputModule>()) module.enabled = false; }
            var input = new GameObject("SkillInfoFixtureInput", typeof(EventSystem), typeof(StandaloneInputModule)); events = input.GetComponent<EventSystem>();
            mouse = input.AddComponent<SimulatedMouse>(); input.GetComponent<StandaloneInputModule>().inputOverride = mouse;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Item/ItemIconSkill.prefab");
            Require(prefab != null && prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "complete original native skill prefab bindings");
            pool = new GameObjectPool<ItemIconSkill>(root.transform, (parent, _, active) =>
            {
                var go = Object.Instantiate(prefab, parent, false); go.SetActive(false);
                foreach (var attachment in go.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
                var icon = go.GetComponent<ItemIconSkill>(); icon.Bind(services); icon.Configure(services); icon.SkillStarInfo.Configure(services);
                services.Creates++; if (active) go.SetActive(true); return go;
            }, "OriginalProfessionSkillIcon");
            row = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionSkillItem.prefab"), root.transform, false);
            slot = row.GetComponent<ProfessionSkillSlot>(); slot.Configure(services);
            itemView = row.AddComponent<ProfessionInfoPanelSkillItem>(); itemView.Configure(services); itemView.SkillSlot = slot;
            var rect = (RectTransform)row.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
            // Only the fixture host anchors the standalone prefab in the camera.
            // The original nested slot/icon layouts are untouched.
            ValidateSort(); PrepareItem();
        }
        private static void ValidateSort()
        {
            var go = new GameObject("InformationSortFixture"); go.SetActive(false);
            var panel = go.AddComponent<ProfessionInfoPanel>();
            foreach (var profession in data.professions)
            {
                var ids = profession.Skills.ToList();
                Set(panel, "mInfo", new ProfessionDetailDefinition { Skills = ids });
                panel.Configure(new Information { Target = new ProfessionDefinition { Ids = ids } });
                typeof(ProfessionInfoPanel).GetMethod("SortSkills", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
                var actual = (List<IProfessionInfoSkill>)Get(panel, "mAllSkills");
                Require(actual.Count == ids.Count, "all original profession skills retained");
                bool passive = false; int previous = int.MaxValue;
                foreach (var item in actual)
                {
                    if (item.ItemType == 101 && !passive) { passive = true; previous = int.MaxValue; }
                    Require(item.ItemType == (passive ? 101 : 100) && item.Rank <= previous, "active/passive grouping and configured rank descending");
                    previous = item.Rank;
                }
            }
            var a = data.items.First(i => i.ItemType == 100);
            var b = new Item { ClassId = int.MaxValue, Rank = a.Rank };
            Require(ProfessionInfoSkillComparer.sDefault.DefaultComparer(a, b) == 0, "equal-rank order has no added ID tie-break");
            Set(panel, "mInfo", new ProfessionDetailDefinition { Skills = null });
            typeof(ProfessionInfoPanel).GetMethod("SortSkills", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            Require(((List<IProfessionInfoSkill>)Get(panel, "mAllSkills")).Count == 0, "null skills clears previous contents");
            Object.Destroy(go);
        }
        private static void PrepareItem()
        {
            var info = data.items[index]; services.Learned = index % 2 == 0 && (info.ItemType == 100 || info.ItemType == 101) ? new Owned { Item = info, Rank = info.Rank } : null;
            itemView.Init(info); Require(slot.TryGetItemIcon(out current), "native slot owns actual skill icon");
            Require(current.myInfo == info && (services.Learned == null ? current.myItemData == null : current.myItemData == services.Learned), "source owned/unowned item branch");
            Require(current.IconSprite != null && current.IconSprite.name == Path.GetFileName(info.RawIcon), "actual original skill/skin Sprite address binding");
            Require(current.FrameEffectInfo.Count == 0, "original skill prefab has no frame SDK effect");
            Require(services.Subscribers == 4 && services.CountSubscribers == 1 && services.DebugSubscribers == 1, "original active icon subscriptions exactly once");
            var tip = current.GetAttachment(ItemIconAttachmentType.NotGotTip, false);
            Require(tip != null && tip.gameObject.activeSelf == (services.Learned == null), "original NotGotTip visibility");
            frame = Time.frameCount;
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup >= SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            { Debug.LogError("Profession skill information validation timed out."); Finish(1); return; }
            if (!ready || !EditorApplication.isPlaying) return;
            try
            {
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount <= frame + 2) return;
                frame = Time.frameCount;
                if (phase == 0)
                {
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    if (index < 2) Capture(index == 0 ? "skill-owned" : "skill-unowned");
                    if (++index < data.items.Length) { PrepareItem(); return; }
                    Require(services.Creates == 1, "original FIFO pool reuses one icon across all skill/skin definitions");
                    index = Array.FindIndex(data.items, i => data.skins.Any(s => s.SkillId == i.ClassId));
                    PrepareItem();
                    if (services.Learned == null) { services.Learned = new Owned { Item = data.items[index], Rank = data.items[index].Rank }; itemView.Init(data.items[index]); slot.TryGetItemIcon(out current); }
                    services.EmitRank(unrelated: true); Require(services.Learned.Rank == data.items[index].Rank, "unrelated rank event does not change this item");
                    services.EmitRank(); services.EmitRank(batch: true); services.EmitRank(down: true);
                    Require(current.QualityImg.sprite == services.Config.GetQualityBgImage(services.Learned.GetQuality()), "live rank updates preserve original quality binding");
                    services.SetDebug(true); var debug = (TMP_Text)Get(current, "mDebugProfessionRankText"); Require(debug.gameObject.activeSelf && debug.text == "3", "original debug switch callback");
                    services.SetDebug(false); Require(!debug.gameObject.activeSelf, "debug switch hides label");
                    current.ShowSkillLevel(true, 7, true); Require(current.myText.Text == "7" && current.myText.GetComponent<TMP_Text>().enableVertexGradient, "original blessing-level view");
                    current.ShowSkillLevel(false); Require(!current.myTextGo.activeSelf && !current.myText.GetComponent<TMP_Text>().enableVertexGradient, "original level/blessing clear");
                    int rank = data.ranks.First(r => r.RankAddition == current.SkillStarInfo.ShowStarThresholdValue).Rank;
                    current.ShowSkillRank(true, rank); Require(!current.SkillStarInfo.StarNum.gameObject.activeInHierarchy, "threshold equality keeps mini stars");
                    rank = data.ranks.First(r => r.RankAddition > current.SkillStarInfo.ShowStarThresholdValue).Rank;
                    current.ShowSkillRank(true, rank); Require(current.SkillStarInfo.StarNum.gameObject.activeInHierarchy, "strictly above threshold switches to star number");
                    Capture("skill-star-number");
                    var skin = data.skins.First(s => s.SkillId == services.Learned.Id);
                    services.EmitSkin(services.Learned.Id + 1, skin.ClassId); Require(current.IconSprite.name == Path.GetFileName(data.items[index].RawIcon), "unrelated skin event ignored");
                    services.EmitSkin(services.Learned.Id, skin.ClassId); Require(current.IconSprite.name == Path.GetFileName(((Item)services.GetItemInfo(skin.ClassId)).RawIcon), "original live skill-skin Sprite refresh");
                    Aim(); phase = 1;
                }
                else if (phase == 1) { mouse.Down = true; mouse.Held = true; phase = 2; }
                else if (phase == 2) { mouse.Down = false; mouse.Held = false; mouse.Up = true; phase = 3; }
                else if (phase == 3)
                {
                    mouse.Up = false; Require(services.Popups == 1 && services.LastPopup == services.Learned, "actual simulated click reaches original owned popup");
                    detailProvider = new object(); current.SetClickPopupInfoDialog(detailProvider); phase = 4;
                }
                else if (phase == 4) { mouse.Down = true; mouse.Held = true; phase = 5; }
                else if (phase == 5) { mouse.Down = false; mouse.Held = false; mouse.Up = true; phase = 6; }
                else if (phase == 6)
                {
                    mouse.Up = false; Require(services.TypedPopups == 1 && services.LastProvider == detailProvider && services.LastPopup == services.Learned, "actual simulated click reaches original typed skill popup");
                    index = 1; PrepareItem(); Aim(); phase = 7;
                }
                else if (phase == 7) { mouse.Down = true; mouse.Held = true; phase = 8; }
                else if (phase == 8) { mouse.Down = false; mouse.Held = false; mouse.Up = true; phase = 9; }
                else
                {
                    mouse.Up = false; Require(services.Popups == 2 && services.LastPopupInfo == data.items[1], "actual simulated click reaches original unowned info popup");
                    row.SetActive(false); Require(!slot.TryGetItemIcon(out _) && services.Subscribers == 0 && services.CountSubscribers == 0 && services.DebugSubscribers == 0, "disable returns icon and all original listeners");
                    slot.OnGameObjectPoolClear(); slot.OnGameObjectPoolReset(); Require(services.Returns == services.Borrows, "pool callbacks do not double-return");
                    row.SetActive(true); index = 0; PrepareItem(); Require(services.Creates == 1, "disable/re-enable reuses native icon");
                    slot.OnGameObjectPoolReset(); Require(services.Returns == services.Borrows && services.Subscribers == 0, "pool reset releases active icon exactly once");
                    Debug.Log("Profession skill information Play Mode passed: 412 native skill/skin icon bindings, 31 profession sort lists, original star threshold/blessing/debug/skin events, pooled slot/attachment lifetimes and three actual simulated popup clicks. Numeric localization/player/window endpoints are fixtures; full information page and popup contents are not covered.");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Aim()
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)current.transform;
            mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == current.gameObject, "native skill icon raycast without occlusion");
        }
        private static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false, true); texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply(); RenderTexture.active = old;
            Require(texture.GetPixels32().Count(p => p.a != 0) > 100, "native skill prefab GPU render");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output(name + ".png"), texture.EncodeToPNG()); Object.Destroy(texture);
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Vector(Vector2 actual, Vector2 expected) { Near(actual.x, expected.x); Near(actual.y, expected.y); }
        private static void Near(float actual, float expected) => Require(Math.Abs(actual - expected) < .000001f, "native Sprite scalar " + actual + " != " + expected);
        private static InvalidOperationException Unexpected() => new InvalidOperationException("Profession skill fixture entered an unbound service branch.");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Profession skill info: " + message); }
        private static void Finish(int result)
        {
            SessionState.SetBool(Active, false);
            if (row != null) row.SetActive(false); pool?.Destory(); attachments?.Dispose(); fonts?.Dispose();
            ProfessionImageResourceBridge.Provider = previousImages; WorldMultiLanguageText.Localize = previousLanguage;
            ProfessionGameObjectPoolBridge.Provider = previousPool;
            if (camera != null) camera.targetTexture = null; if (target != null) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(result);
        }
    }
}
