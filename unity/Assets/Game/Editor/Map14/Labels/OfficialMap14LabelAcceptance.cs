using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Labels = Zjcs.Official.World.NativeMap14Labels;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14LabelAcceptance
    {
        public static void Run()
        {
            OfficialMap14LabelTools.RestoreAndValidate();
            OfficialMap14LabeledEnvironmentAcceptance.RunWithLabels();
        }
    }

    public static partial class OfficialMap14LabeledEnvironmentAcceptance
    {
        private static object Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static int State(object owner, string field)
        {
            object value = Field(owner, field);
            return Convert.ToInt32(value.GetType().GetProperty("State").GetValue(value));
        }
        private static void VerifyLabels(GameObject root, LabelFixture service)
        {
            var items = root.GetComponentsInChildren<Labels.MapRegionDescItemUI>(true);
            if (items.Length != 110 || root.GetComponentsInChildren<MultiLanguageText>(true).Length != 660)
                throw new InvalidDataException("Original label prefab component closure differs: " + items.Length);
            foreach (var item in items)
            {
                int region = (int)Field(item, "mRegion");
                if (region < 0 || region >= 111) throw new InvalidDataException("Original label world position did not resolve its region.");
                var cfg = (Labels.MapRegionInfo)Field(item, "mRegionCfg");
                if (cfg.Region != region) throw new InvalidDataException("Original region label bound the wrong region.");
                foreach (var text in (List<MultiLanguageText>)Field(item, "mRegionNameTexts"))
                    if (text.Text != service.GetText("region_14_" + region, LanguageType.zh_CN)) throw new InvalidDataException("Original region name was not localized.");
            }
            VerifyDescriptionStates(root, service);
            service.SetLevel(150);
            VerifyDescriptionStates(root, service);
            service.SetStats(85, 0);
            foreach (var strength in root.GetComponentsInChildren<Labels.MapRegionStrengthDisplayUI>(true))
                if (strength.gameObject.activeInHierarchy) { strength.enabled = false; strength.enabled = true; }
            VerifyDescriptionStates(root, service);
            foreach (var color in root.GetComponentsInChildren<Labels.UIColorByLevel>(true))
            {
                if (!color.gameObject.activeInHierarchy) continue;
                var strength = color.GetComponent<Labels.MapRegionStrengthDisplayUI>();
                if (!strength) strength = color.GetComponentInParent<Labels.MapRegionStrengthDisplayUI>();
                if (!strength) continue;
                var cfg = (Labels.MapRegionInfo)Field(strength, "mRegionCfg");
                int level = Labels.Map14LabelFormula.GetCombatRatingLevel(service.CombatRating, cfg.CombatRatingLevelValues);
                service.TryGetLevelColor((Labels.CombatRateingColorType)Field(color, "mCombatRateColorType"), level, out Color expected);
                foreach (var proxy in (List<TextMeshProColorProxy>)Field(color, "mTmpMediators"))
                    if (((TextMeshPro)Field(proxy, "mTextMeshPro")).color != expected.linear)
                        throw new InvalidDataException("Original combat-rating text color did not preserve linear conversion.");
            }
            service.SetProgress(true);
            foreach (var item in items)
                foreach (var text in (List<MultiLanguageText>)Field(item, "mProgressTexts"))
                {
                    int region = (int)Field(item, "mRegion");
                    string expected = service.RegionIsActived(14, region) ? "0" : (service.CalcWorldMapRegionProgress(14, region, service.GetWorldMapRegionProgress(14, region)) / 100).ToString();
                    if (text.Text != string.Format(service.GetText(text.myKey, LanguageType.zh_CN), expected))
                        throw new InvalidDataException("Original progress text or active-region special case differs.");
                }
            service.SetProgress(false);
            foreach (var item in items) { item.enabled = false; item.enabled = true; }
            service.SetPublic(true);
            foreach (var item in items)
            {
                var slot = (GameObject)Field(item, "mExtendSlot");
                if (slot && slot.activeSelf) throw new InvalidDataException("Original public-region label retained private details.");
            }
            service.SetLevel(1); service.SetStats(5, 100000); service.SetPublic(service.InitialPublic);
            VerifyDescriptionStates(root, service);
            foreach (var text in root.GetComponentsInChildren<TextMeshPro>(true))
                if (text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text))
                {
                    text.ForceMeshUpdate(true, false);
                    if (text.textInfo == null || text.textInfo.characterCount == 0)
                        throw new InvalidDataException("Original enabled text produced no glyphs: " + text.name + ":" + text.text + "; font=" + text.font);
                }
            Debug.Log("Map14 labels passed: 110 original region bindings, 660 localized texts; level limit transitions, progress update/re-enable, public/private details and native TMP glyph generation. No user save fixture is used.");
        }
        private static void VerifyDescriptionStates(GameObject root, LabelFixture service)
        {
            foreach (var strength in root.GetComponentsInChildren<Labels.MapRegionStrengthDisplayUI>(true))
            {
                if (!strength.gameObject.activeInHierarchy) continue;
                var cfg = (Labels.MapRegionInfo)Field(strength, "mRegionCfg");
                var helper = Field(strength, "mRegionDescHelper");
                int expected = service.RegionIsActived(14, cfg.Region) ? 1 : !cfg.ExistLimit() ? 2 : cfg.IsLimited(service.Level) ? 4 : 3;
                if (State(helper, "mRegionStrengthDisplaySwitcher") != expected || State(strength, "mRegionStrengthDisplaySwitcher") != (int)cfg.StrengthDisplayType)
                    throw new InvalidDataException("Original region description/strength state differs: " + cfg.Region);
                if (State(strength, "mRankSuppressionSwitcher") != (service.CalcRankSuppression(service.SubRank, cfg.SubRank) > 0 ? 1 : 0))
                    throw new InvalidDataException("Original rank suppression style differs: " + cfg.Region);
            }
        }
        private static Vector3 LabelView(GameObject root)
        {
            var text = root.GetComponentsInChildren<MultiLanguageText>(true).First(t => t.myKey == "region_14_0");
            Vector3 p = text.transform.position; return new Vector3(p.x, p.y, -100);
        }
        private sealed class LabelFixture : Labels.IMap14LabelServices, Labels.IMap14LabelPlayer, ILanguageTypeService, ILocalizationTextService, IMultiLanguageFontService, IDisposable
        {
            private readonly Services environment;
            private readonly Labels.Map14LabelData data;
            private readonly MultiLanguageUIService ui;
            private readonly TMP_FontAsset font;
            private readonly Zjcs.Offline.World.NativeMap14Labels.Map14LabelFontInstance fontInstance;
            private bool? publicOverride;
            private bool fullProgress;
            private readonly Dictionary<string, string> texts;
            public bool InitialPublic => (int)environment.GetCurrentMapStatus()[0] == (int)Labels.MapRegionStatus.Public;
            public LabelFixture(Services environment)
            {
                this.environment = environment;
                data = JsonUtility.FromJson<Labels.Map14LabelData>(Resources.Load<TextAsset>("Map14/Native/region-label-data").text);
                if (data.manifest != "871_156598" || data.regions.Length != 111) throw new InvalidDataException("Original label configuration is not bound.");
                fontInstance = new Zjcs.Offline.World.NativeMap14Labels.Map14LabelFontInstance(
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset"));
                font = fontInstance.Font;
                texts = data.texts.ToDictionary(t => t.key, t => t.value); ui = new MultiLanguageUIService(this);
            }
            public void Bind(GameObject instance) => Zjcs.Offline.World.NativeMap14Labels.Map14LabelBinding.Bind(instance, this, ui, this, this, this);
            public void Dispose()
            {
                if (OnMapStatusUpdate != null || OnWorldMapProgressUpdate != null || OnCombatRatingChanged != null || OnLevelUpdate != null || OnPlanesRankUnlock != null || Initialized != null)
                    throw new InvalidDataException("Original label destruction retained player/map subscriptions.");
                ui.Dispose();
                fontInstance.Dispose();
            }
            public int MapClassId => 14;
            public bool IsMapInitialized => true;
            public event Action Initialized;
            public Labels.IMap14LabelPlayer Player => this;
            public bool IsTeamMember => false;
            public int QueryRegion(Vector3 p) => data.QueryRegion(p);
            public Labels.MapRegionInfo GetRegion(int map, int region) => data.regions.Single(r => r.ClassId == map && r.Region == region);
            public string GetRegionNameKey(int map, int region) => "region_" + map + "_" + region;
            public object CompactNumber(long value) => Zjcs.Offline.Progression.OfficialPropFormat.Compact(value);
            public int CalcWorldMapRegionProgress(int map, int region, int progress) => data.CalcWorldMapRegionProgress(map, region, progress);
            public int CalcRankSuppression(int a, int b) => data.CalcRankSuppression(a, b);
            public bool TryGetLevelColor(Labels.CombatRateingColorType type, int level, out Color color) => data.TryGetLevelColor(type, level, out color);
            public DateTime Now => throw new InvalidOperationException("Map14 has no timed region limits.");
            public double CalcRemainSecond(DateTime end) => throw new InvalidOperationException("Map14 has no timed region limits.");
            public object FormatTimeSpan(TimeSpan span, int format) => throw new InvalidOperationException("Map14 has no timed region limits.");
            public int Level { get; private set; } = 1;
            public int SubRank { get; private set; } = 5;
            public long CombatRating { get; private set; } = 100000;
            public event Action<int> OnLevelUpdate;
            public event Action<int> OnPlanesRankUnlock;
            public event Action<long, long> OnCombatRatingChanged;
            public event Action<int, List<int>> OnWorldMapProgressUpdate;
            public event Action<int, int, Labels.MapRegionStatus, Labels.MapRegionStatus> OnMapStatusUpdate;
            public event Action<long> OnDebugTimeOffset;
            public bool RegionIsActived(int map, int region) => GetMapRegionStatus(map, region) == Labels.MapRegionStatus.Public;
            public Labels.MapRegionStatus GetMapRegionStatus(int map, int region) => map != 14 || region < 0 || region >= 111 ? Labels.MapRegionStatus.None : publicOverride.HasValue ? (publicOverride.Value ? Labels.MapRegionStatus.Public : Labels.MapRegionStatus.Private) : (Labels.MapRegionStatus)(int)environment.GetCurrentMapStatus()[region];
            public int GetWorldMapRegionProgress(int map, int region) => region < 0 ? 0 : fullProgress ? data.maxProgress[region] : 0;
            public void SetLevel(int value) { Level = value; OnLevelUpdate?.Invoke(value); }
            public void SetStats(int subRank, long combatRating) { SubRank = subRank; long previous = CombatRating; CombatRating = combatRating; OnCombatRatingChanged?.Invoke(previous, combatRating); }
            public void SetProgress(bool full) { fullProgress = full; OnWorldMapProgressUpdate?.Invoke(14, data.regions.Select(r => GetWorldMapRegionProgress(14, r.Region)).ToList()); }
            public void SetPublic(bool value)
            {
                var previous = data.regions.Select(r => GetMapRegionStatus(14, r.Region)).ToArray();
                publicOverride = value;
                foreach (var region in data.regions) OnMapStatusUpdate?.Invoke(14, region.Region, previous[region.Region], GetMapRegionStatus(14, region.Region));
            }
            public LanguageType CurrentLanguage { get; set; } = LanguageType.zh_CN;
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged;
            public MultiLanguageFontAsset GetMultiLanguageFontAsset() => new MultiLanguageFontAsset(font);
            public bool ContainsKey(string key) => texts.ContainsKey(key);
            public string GetText(string key, LanguageType language, params object[] args) => texts.TryGetValue(key, out string value) ? value : throw new KeyNotFoundException("Original Map14 text key: " + key);
            public bool TryGetOriginalText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
            public bool TryGetText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
            public bool TryGetText(string key, out string result) => texts.TryGetValue(key, out result);
            public string GetTextFormated(string key, LanguageType language, params object[] args) => string.Format(GetText(key, language), args);
            public void ReloadData() => throw new NotSupportedException("Read-only fixture.");
            public void SetReplacer(ITextReplacer replacer) => throw new NotSupportedException("No text replacer in this fixture.");
            public void AddDataProvider(ILanguageTextDataProvider provider) => throw new NotSupportedException("Read-only fixture.");
        }
    }
}
