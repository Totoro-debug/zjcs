#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;

[InitializeOnLoad]
public static class HomeAwardQueueValidation
{
    const string Key = "Zjcs.HomeAwardQueue.Active";
    static HomeAwardQueueValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("OriginalHomeAwardQueueFixture").AddComponent<HomeAwardQueueRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original award queue timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the owned isolated Unity project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.isPlaying = true;
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("HOME_AWARD_QUEUE_PASSED: original selected queue, XP icon, merged amounts, native animations/pooling, extra split, spring spacing, fullscreen close and muted sound dispatch. Production collection and general icon catalog remain unfinished.");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeAwardQueueRunner : MonoBehaviour
{
    [Serializable] sealed class Data { public Info item; public Palette global_colors; public TextRow[] texts; }
    [Serializable] sealed class TextRow { public string key, text; }
    [Serializable] sealed class Info { public int ClassId, Quality, ItemType, Rank; public string RawIcon, IconEffect; public bool CanOverlap; }
    [Serializable] sealed class ColorValue { public Color Color; }
    [Serializable] sealed class ColorList { public int ThemeColorType; public int[] TypeList; public ColorValue[] TextColors; }
    [Serializable] sealed class ColorConfig { public ColorList[] TextColorLists; }
    [Serializable] sealed class Palette : IProfessionItemColors
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
            throw Unexpected("selected color " + theme + "/" + type);
        }
    }
    sealed class Experience : ItemInfo
    {
        public Info Source;
        public override int ClassId => Source.ClassId;
        public override string Icon => Source.RawIcon;
        public override string IconEffect => Source.IconEffect;
        public override Quality Quality => (Quality)Source.Quality;
        public override bool CanOverlap => Source.CanOverlap;
        public override ItemType ItemType => (ItemType)Source.ItemType;
        public override int Rank => Source.Rank;
        public override object PartInfo => null;
        public override bool IsEquip => false;
        public override bool IsSkill => false;
        public override bool IsSkillPiece => false;
        public override object GetFakeIcon() => throw Unexpected("XP must use its selected sprite");
    }
    sealed class Player : IProfessionItemPlayer
    {
        public int Subscribers;
        public long GetItemAmount(int id) => 0;
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate
        { add { Subscribers++; } remove { Subscribers--; } }
        public bool CanGetItemFromMapLocation(object location) => throw Unexpected("map source");
    }
    sealed class Context : IProfessionItemIconContext
    {
        public HomeAwardQueueRunner Owner;
        public ItemIconConfig Config => Owner.assets.itemIconConfig;
        public Object GrayConfig => throw Unexpected("gray");
        public IProfessionItemColors Colors => Owner.data.global_colors;
        public IProfessionItemPlayer Player => Owner.player;
        public IProfessionItemInfo GetItemInfo(int id) => Owner.GetItemInfo(id);
        public object Compact(long value) => Owner.Compact(value);
        public object CultureText(string key) => Owner.Text(key);
        public string GetItemNameKey(int id) => Owner.GetItemNameKey(id);
        public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) { throw Unexpected("item source"); }
        public bool CanMerge(int id, bool recursion) => throw Unexpected("merge source");
        public object GetLocationInfos(int id) => throw Unexpected("location");
        public void SetUIGray(Object config, GameObject target, bool gray) => throw Unexpected("gray mutation");
        public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unexpected("XP has no frame sound");
        public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All) => Object.Instantiate(prefab, parent, false);
        public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unexpected("XP effect");
        public void PopupInfoDialog(IProfessionItemData item, IWindowOperator owner, long required, Action<GameObject> callback) => throw Unexpected("indexed reward");
        public void PopupInfoDialog(IProfessionItemInfo item, IWindowOperator owner, long required, Action<GameObject> callback)
        { Require(item == Owner.info && owner is ItemIcon && required == 0, "original click arguments"); Owner.infoPopups++; }
    }
    sealed class Asset<T> : IHomeAwardAsset<T>
    {
        T value;
        public Asset(T value) { this.value = value; }
        public T Result => value;
        public void Dispose() { value = default; }
    }
    sealed class Done : IHomeAwardPopupOperation { public bool IsDone => true; }
    sealed class Services : IHomeAwardQueueServices, IHomeAwardItemServices, IHomeAwardIconServices, IItemIconAttachmentManager, IProfessionSoundHost
    {
        public HomeAwardQueueRunner Owner;
        public bool HasPlayer => true;
        public event Action<GameObject> OnWindowOpened;
        public int Subscribers => OnWindowOpened?.GetInvocationList().Length ?? 0;
        public void FullScreen() => OnWindowOpened?.Invoke(Owner.canvas.gameObject);
        public bool IsFullScreen(GameObject window) => window == Owner.canvas.gameObject;
        public void Close(GameObject window) { window.SetActive(false); Owner.closed++; }
        public bool IsIgnoreShowAward(int id) => false;
        public IHomeAwardAsset<GameObject> LoadAssetByGuid(string guid) => new Asset<GameObject>(Owner.assets.itemPrefabs.Single(row => row.originalGuid == guid).prefab);
        public GameObjectPool<AwardListPanelItem> CreatePool(Transform root, AwardListPanelItem prefab)
            => new GameObjectPool<AwardListPanelItem>(root, (parent, _, active) => {
                var clone = Owner.Clone(prefab.gameObject, parent);
                clone.GetComponent<AwardListPanelItem>().Services = this;
                clone.SetActive(active); return clone;
            }, prefab.name);
        public GameObject GetWindow() => Owner.panel && Owner.panel.gameObject.activeInHierarchy ? Owner.panel.gameObject : null;
        public IHomeAwardPopupOperation Popup(IWindowOperator owner, Action<AwardListPanel> initialized, bool instant, bool local, bool autoAdjustByWindowContext)
        {
            Require(!instant && local && autoAdjustByWindowContext, "original popup flags");
            if (!Owner.panel) {
                var clone = Owner.Clone(Owner.assets.queuePrefab, Owner.canvas.transform);
                Owner.panel = clone.GetComponent<AwardListPanel>();
            }
            Owner.panel.gameObject.SetActive(true);
            initialized(Owner.panel); return new Done();
        }
        public ItemInfo GetItemInfo(int id) => Owner.GetItemInfo(id);
        public HomeAwardIcon InstantiateAward(Transform slot, AwardData award, Action<HomeAwardIcon, AwardData> init, bool autoTransition, bool isSelf, bool showOutputAddition)
        {
            var icon = Owner.Clone(Owner.assets.defaultIconPrefab, slot).GetComponent<HomeAwardIcon>();
            icon.Services = this; icon.gameObject.SetActive(true);
            icon.Init(award, init, autoTransition, isSelf, showOutputAddition); return icon;
        }
        public string GetItemNameKey(int id) => Owner.GetItemNameKey(id);
        public object CultureText(string key) => Owner.Text(key);
        public string Text(string key) => Owner.Text(key);
        public object Compact(long count) => Owner.Compact(count);
        public object DecorateExtra(object text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";
        public int MoneyId => 1;
        public int PlayerExpItemId => Owner.info.ClassId;
        public double GetMoneyAdditionPercent() => 0;
        public double GetExperienceAdditionPercent() => Owner.experienceAddition;
        public IHomeAwardAsset<Sprite> LoadSprite(string path) => throw Unexpected("XP orange light");
        public ItemIcon GetIcon(ItemInfo item, Transform parent)
        {
            var icon = Owner.Clone(Owner.assets.itemIconPrefab, parent).GetComponent<ItemIcon>();
            icon.Bind(new Context { Owner = Owner });
            foreach (var attachment in icon.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(this);
            icon.gameObject.SetActive(true); icon.Init(item); return icon;
        }
        public ItemIcon GetIcon(IProfessionItemData item, Transform parent) => throw Unexpected("indexed icon");
        public void RecycleIcon(ItemIcon icon) { icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject); Owner.recycled++; }
        public IProfessionItemData GetItem(ulong index) => throw Unexpected("indexed item");
        public IHomeAwardOutputAddition GetOutputAddition(ItemIcon icon) => throw Unexpected("output attachment disabled on list");
        public GameObject GetAttachment(ItemIconAttachmentType type, Transform parent, bool active)
        {
            var prefab = type == ItemIconAttachmentType.HintTip ? Owner.assets.hintPrefab
                : type == ItemIconAttachmentType.ExtraGetTip ? Owner.assets.extraGetPrefab : null;
            if (!prefab) throw Unexpected("attachment " + type);
            var clone = Owner.Clone(prefab, parent); clone.SetActive(active); return clone;
        }
        public void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell value) { value.gameObject.SetActive(false); Object.Destroy(value.gameObject); }
        public uint PostEvent(uint eventId, GameObject emitter)
        {
            var sound = Owner.assets.sounds.FirstOrDefault(row => row.eventId == eventId);
            var clip = sound?.clip ?? OfflineHomeHudAssets.LoadDefault().Sound(eventId);
            var source = emitter.AddComponent<AudioSource>(); source.clip = clip; source.Play();
            Require(source.clip && source.clip.samples > 0, "original sound clip is decoded");
            if (sound != null) Owner.sounds.Add(Time.time);
            return (uint)Owner.sounds.Count;
        }
    }
    Data data;
    Experience info;
    readonly Player player = new Player();
    OfficialHomeAwardQueueAssetCatalog assets;
    AwardListPanel panel;
    Canvas canvas;
    Camera camera;
    RenderTexture target;
    Transform staging;
    IDisposable scope;
    FontAssetFontService fonts;
    Services services;
    IProfessionSoundHost previousSound;
    readonly List<float> sounds = new List<float>();
    int closed, recycled, infoPopups, errors;
    double experienceAddition;
    string Text(string key) => data.texts.FirstOrDefault(row => row.key == key)?.text ?? key;
    object Compact(long count) { Require(count < 10000, "small numeric fixture scope"); return count.ToString(CultureInfo.InvariantCulture); }
    string GetItemNameKey(int id) { Require(id == info.ClassId, "XP name scope"); return "item_50_name"; }
    ItemInfo GetItemInfo(int id) { Require(id == info.ClassId, "XP catalog scope"); return info; }
    GameObject Clone(GameObject prefab, Transform parent)
    {
        var clone = Object.Instantiate(prefab, staging, false); clone.SetActive(false);
        foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) fonts.SetupFont(text.font);
        clone.transform.SetParent(parent, false); return clone;
    }
    IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.logMessageReceived += OnLog;
        var steps = new Stack<IEnumerator>(); steps.Push(Check());
        Exception failure = null;
        while (steps.Count > 0) {
            object current;
            try { if (!steps.Peek().MoveNext()) { steps.Pop(); continue; } current = steps.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (current is IEnumerator nested) steps.Push(nested); else yield return current;
        }
        if (panel) panel.gameObject.SetActive(false);
        OfficialWwiseEvent.Host = previousSound;
        scope?.Dispose(); fonts?.Dispose();
        Application.logMessageReceived -= OnLog;
        HomeAwardQueueValidation.Finish(failure ?? (errors == 0 ? null : new Exception("Unexpected queue errors: " + errors)));
    }
    void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    IEnumerator Check()
    {
        assets = OfficialHomeAwardQueueAssetCatalog.LoadDefault();
        data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("home-award-queue-fixture").text);
        info = new Experience { Source = data.item };
        Require(info.ClassId == 50 && info.ItemType == ItemType.Prop && info.IconEffect == "", "selected XP item metadata");
        var hud = OfflineHomeHudAssets.LoadDefault(); fonts = hud.CreateFontService(); fonts.Initialize();
        camera = new GameObject("FixtureCamera", typeof(Camera)).GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black; target = new RenderTexture(608, 1080, 24); camera.targetTexture = target;
        canvas = new GameObject("FixtureCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 1;
        staging = new GameObject("InactivePrefabBinding", typeof(RectTransform)).transform; staging.SetParent(canvas.transform, false); staging.gameObject.SetActive(false);
        var bed = Zjcs.Official.HomeHangUpWindowsUI.OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
        var images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        images.Entries = new[] { new OfflineProfessionImageCatalog.Entry { Path = "Assets/Image/" + bed.bedRewardImagePath,
            AssetGuid = "fixture-selected-bed-xp", Asset = bed.bedRewardImage } };
        var loader = new OfflineProfessionImageResourceLoader(images);
        scope = OfflineUiBindingModule.Register(canvas.transform, new OfflineUiBindings { Localize = Text,
            Font = () => hud.defaultFont, Images = _ => loader, Pool = (_, prefab, parent, world) => Object.Instantiate(prefab, parent, world) });
        services = new Services { Owner = this }; previousSound = OfficialWwiseEvent.Host; OfficialWwiseEvent.Host = services;
        new GameObject("FixtureEvents", typeof(EventSystem));
        int complete = 0;
        AwardListPanel.PopupAwardListPanel(services, new List<AwardData> { Award(10), Award(20) }, onComplete: () => complete++);
        yield return new WaitForSeconds(.3f);
        Require(complete == 1 && sounds.Count == 1, "original duplicate XP merge produces one queue dispatch");
        Require(VisibleText().Contains("30"), "merged native count text");
        var icon = panel.GetComponentsInChildren<ItemIcon>().Single();
        Require(icon.IconSprite == bed.bedRewardImage && icon.QualityImg.sprite == assets.itemIconConfig.GetQualityBgImage(info.Quality), "selected XP and exact original ordered quality binding");
        Capture("home-award-queue-normal.png");
        Click(icon.Button, passThrough: true);
        // The original queue item explicitly disables raycasts. Validate its pass-
        // through behavior, then exercise the unchanged default icon separately.
        var pointerProbe = services.GetIcon(info, canvas.transform);
        pointerProbe.SetClickPopupInfoDialog();
        yield return null;
        Capture("home-award-queue-icon-input.png");
        Click(pointerProbe.Button);
        Require(infoPopups == 1, "simulated pointer reaches the original item button");
        services.RecycleIcon(pointerProbe);
        experienceAddition = .1;
        AwardListPanel.PopupAwardListPanel(services, new List<AwardData> { Award(110) }, notMergeAward: true);
        yield return new WaitForSeconds(.3f);
        Require(sounds.Count == 2 && VisibleText().Contains("100") && VisibleText().Contains("10"), "original experience raw/extra split");
        Capture("home-award-queue-extra.png");
        yield return new WaitForSeconds(3);
        Require(!panel.gameObject.activeInHierarchy && services.Subscribers == 0 && player.Subscribers == 0 && recycled >= 2, "original timed close releases subscriptions and icons");
        experienceAddition = 0;
        int before = sounds.Count;
        AwardListPanel.PopupAwardListPanel(services, new List<AwardData> { Award(1), Award(2) }, notMergeAward: true,
            awardListPanelItemType: AwardListPanelItemType.MapSpringFestivalGrandPrize);
        yield return new WaitForSeconds(.3f);
        Require(sounds.Count == before + 1, "single path defers the second reward");
        Capture("home-award-queue-spring.png");
        yield return new WaitForSeconds(.7f);
        Require(sounds.Count == before + 2 && sounds[before + 1] - sounds[before] >= .59f, "original single path minimum interval");
        services.FullScreen(); yield return null;
        Require(!panel.gameObject.activeInHierarchy && player.Subscribers == 0 && services.Subscribers == 0, "fullscreen window closes and releases queue");
    }
    AwardData Award(long amount) => new AwardData { AwardId = info.ClassId, Amount = amount, Catalog = services };
    string VisibleText() => string.Join("|", panel.GetComponentsInChildren<TMP_Text>().Select(text => text.text));
    void Click(Button button, bool passThrough = false)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera,
            rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        if (passThrough) {
            Require(button.GetComponentsInParent<CanvasGroup>().Any(group => !group.blocksRaycasts), "original reward list item is non-interactive");
            Require(!hits.Any(hit => hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform)),
                "simulated pointer preserves original reward pass-through");
            return;
        }
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "reward item button is top simulated pointer hit: " + string.Join(",", hits.Select(hit => hit.gameObject.name))
            + " point=" + pointer.position + " rect=" + rect.rect + " active=" + button.isActiveAndEnabled
            + " graphics=" + string.Join(";", button.GetComponentsInChildren<Graphic>(true).Select(g => g.name + ":" + g.raycastTarget + ":" + g.isActiveAndEnabled)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        RenderTexture.active = previous; Object.Destroy(texture);
    }
    static Exception Unexpected(string value) => new InvalidOperationException("Outside original XP probe: " + value);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
