using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using SharedSlider = Zjcs.Official.HomeCountControlsUI.SetUseCountSlider;
using FastSlider = Zjcs.Official.HomeFastHangUpUI.SetUseCountSlider;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ItemUseCountValidation
{
    const string Pending = "Zjcs.ItemUseCount.Pending";
    static readonly List<string> checks = new List<string>();
    static ItemUseCountValidation() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        bool success = false;
        GameObject staging = null;
        FontAssetFontService fonts = null;
        try
        {
            Zjcs.Official.World.NativeLabelUI.Framework.LanguageManager.Initialize(
                new OfflinePartnerLocalizationService(OfficialHomeFastHangUpAssetCatalog.LoadDefault().texts));
            staging = new GameObject("Inactive original use-count fixture"); staging.SetActive(false);
            fonts = OfflineHomeHudAssets.LoadDefault().CreateFontService(); fonts.Initialize();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/UI/FastHangUp/FurnitureSpeedUsedItem.prefab");
            Check(prefab, "original acceleration slider prefab loads");
            var template = prefab.GetComponentInChildren<FastSlider>(true);
            Check(template, "existing native script identity resolves");
            foreach (bool legacy in new[] { false, true })
            {
                var root = Object.Instantiate(template.gameObject, staging.transform, false);
                var slider = root.GetComponent<FastSlider>();
                SharedSlider shared = slider;
                if (!legacy)
                {
                    var add = slider.AddBtn; var minus = slider.MinusBtn; var max = slider.MaxBtn;
                    var gauge = slider.Slider; var text = slider.CurrentCountText;
                    var addHold = slider.AddLongTouch; var minusHold = slider.MinusLongTouch;
                    Object.DestroyImmediate(slider);
                    shared = root.AddComponent<SharedSlider>();
                    shared.AddBtn = add; shared.MinusBtn = minus; shared.MaxBtn = max;
                    shared.Slider = gauge; shared.CurrentCountText = text;
                    shared.AddLongTouch = addHold; shared.MinusLongTouch = minusHold;
                }
                var services = new Services();
                if (legacy)
                {
                    var fast = new FastServices(); slider.Services = fast;
                    Check(typeof(FastSlider).GetProperty("Services").PropertyType == typeof(IHomeFastHangUpServices),
                        "existing reflective service binding stays unambiguous");
                    var adapter = (IUseCountSliderServices)typeof(SharedSlider).GetProperty("LimitServices").GetGetMethod(true).Invoke(slider, null);
                    Check(adapter.FastItemMaxCountMessageColdDown == fast.FastItemMaxCountMessageColdDown, "legacy service adapter retains configured cooldown");
                }
                shared.LimitServices = services;
                shared.GlobalColors = new Colors();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.font) fonts.SetupFont(text.font);
                foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = key => key == "ui_common_contrast_1" ? "{0}/{1}" : key;
                foreach (var hold in root.GetComponentsInChildren<LongTouch>(true)) hold.PointerPositionOverride = () => Vector2.zero;
                root.transform.SetParent(null, false); root.SetActive(true);
                Exercise(shared, services, legacy ? "legacy" : "shared");
                root.SetActive(false); Object.DestroyImmediate(root);
            }
            success = true;
        }
        catch (Exception error) { Debug.LogException(error); checks.Add(error.ToString()); }
        finally
        {
            if (staging) Object.DestroyImmediate(staging);
            fonts?.Dispose();
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_USE_COUNT_OUTPUT"), "report.json"),
                JsonConvert.SerializeObject(new { success, checks }, Formatting.Indented));
            SessionState.SetBool(Pending, false); SessionState.SetInt(Pending + ".exit", success ? 0 : 1);
            EditorApplication.playModeStateChanged += Exit; EditorApplication.isPlaying = false;
        }
    }
    static void Exercise(SharedSlider slider, Services services, string name)
    {
        int events = 0; slider.OnCurrentCountUpdate += _ => events++;
        slider.Init(1, 1000, 700, 99, "limit");
        Check(slider.CurrentCount == 99 && slider.Slider.maxValue == 1000, name + " initialization clamps current, retains inventory range");
        slider.Slider.value = 7; slider.AddBtn.onClick.Invoke();
        Check(slider.CurrentCount == 8, name + " original add button");
        slider.MinusBtn.onClick.Invoke(); Check(slider.CurrentCount == 7, name + " original minus button");
        slider.Slider.value = 1000;
        Check(slider.CurrentCount == 99 && slider.Slider.value == 99 && services.Messages.Count == 1
            && services.Messages[0] == "limit:99", name + " drag cap and original formatted notice");
        slider.Slider.value = 1000;
        Check(services.Messages.Count == 1, name + " configured message cooldown");
        slider.Init(1, 4, 1); slider.Slider.value = 4;
        Check(slider.CurrentCount == 4, name + " unspecified cap uses inventory count");
        slider.Init(1, 1, 7, 99);
        Check(!slider.Slider.interactable && slider.CurrentCount == 7 && slider.Slider.value == 1,
            name + " original single-item range and requested current count");
        slider.Init(1, 0, 1, 99);
        Check(slider.CurrentCount == 0 && !slider.Slider.interactable, name + " empty inventory range");
        slider.Init(5, 100, -3, 2);
        Check(slider.CurrentCount == 2 && slider.Slider.value == 5, name + " original clamp order when cap is below minimum");
        slider.Init(1, 100, 20, 2, ""); slider.Slider.value = 100;
        Check(slider.CurrentCount == 2 && services.Messages.Count == 1, name + " empty notice key stays silent");
        int before = events; slider.gameObject.SetActive(false); slider.gameObject.SetActive(true);
        slider.Init(1, 100, 1, 99); slider.AddBtn.onClick.Invoke();
        Check(slider.CurrentCount == 2 && events == before + 2, name + " reenable retains one native button callback");
    }
    sealed class Services : IUseCountSliderServices
    {
        public readonly List<string> Messages = new List<string>();
        public double FastItemMaxCountMessageColdDown => 10000;
        public string TextFormatted(string key, params object[] args) => key + ":" + args[0];
        public void SystemMessage(string value) => Messages.Add(value);
    }
    sealed class Colors : IHomeCountColors
    { public Color LightRed => Color.red; public string WrapColor(string value, Color color) => value; }
    sealed class FastServices : IHomeFastHangUpServices
    {
        public Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowServices Window => null;
        public IHomeFastHangUpPlayer Player => null;
        public int DefaultFastItemUseCount => 1;
        public double FastItemMaxCountMessageColdDown => 17.5;
        public HomeFastHangUpInfo GetFastHangUpInfo(int id) => throw new NotSupportedException();
        public bool TryGetFastHangUpItemHours(int id, out int hours) { hours = 0; throw new NotSupportedException(); }
        public IProfessionItemInfo GetHangUpRewardItem(int id) => throw new NotSupportedException();
        public ItemIcon GetIcon(int id, Transform parent) => throw new NotSupportedException();
        public ItemIcon GetIcon(IProfessionItemInfo item, Transform parent) => throw new NotSupportedException();
        public void RecycleIcon(ItemIcon icon) => throw new NotSupportedException();
        public double GetHangUpUnsettledRewardCount(int id, out double seconds) { seconds = 0; throw new NotSupportedException(); }
        public void RequestFastHangUp(HomeFastHangUpRequest request, Action<HomeFastHangUpResponse, int> completed) => throw new NotSupportedException();
        public void OpenProgress(int id, Action finished) => throw new NotSupportedException();
        public void OpenAward(Zjcs.Official.HomeHangUpWindowsUI.IHomeWindowAwardMessage message) => throw new NotSupportedException();
    }
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add("PASS " + name); }
    static void Exit(PlayModeStateChange state)
    { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(SessionState.GetInt(Pending + ".exit", 1)); }
}
