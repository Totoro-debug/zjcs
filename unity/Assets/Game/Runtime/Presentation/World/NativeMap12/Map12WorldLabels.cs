using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap12Labels;
using Zjcs.Offline.World.NativeMap12Labels;

namespace Zjcs.Presentation.World.NativeMap12
{
    internal sealed class Map12WorldLabels : IMap12LabelServices, IMap12LabelPlayer,
        ILanguageTypeService, ILocalizationTextService, IDisposable
    {
        private readonly IMap12NativeWorldState state;
        private readonly Map12LabelData data;
        private readonly Dictionary<string, string> texts;
        private readonly MultiLanguageUIService ui;
        private readonly Map12LabelFontInstance font;
        private readonly MapRegionStatus[] statuses;
        private List<int> progress;
        public Map12WorldLabels(IMap12NativeWorldState state, Map12WorldEnvironment environment, TMP_FontAsset sourceFont)
        {
            this.state = state; data = environment.Data;
            texts = data.texts.ToDictionary(t => t.key, t => t.value);
            font = new Map12LabelFontInstance(sourceFont);
            ui = new MultiLanguageUIService(this);
            Level = state.PlayerLevel; SubRank = state.PlayerSubRank; CombatRating = state.PlayerCombatRating;
            statuses = data.regions.Select(r => State(r.Region)).ToArray();
            progress = data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
        }
        public void Bind(GameObject instance) => Map12LabelBinding.Bind(instance, this, ui, this, this, font);
        public void Dispose() { ui.Dispose(); font.Dispose(); }
        public int MapClassId => 12;
        public bool IsMapInitialized => true;
        public event Action Initialized { add { } remove { } }
        public IMap12LabelPlayer Player => this;
        public bool IsTeamMember => false;
        public int QueryRegion(Vector3 position) => data.QueryRegion(position);
        public MapRegionInfo GetRegion(int map, int region) => data.regions.Single(r => r.ClassId == map && r.Region == region);
        public string GetRegionNameKey(int map, int region) => "region_" + map + "_" + region;
        public object CompactNumber(long value) => LanguageCultureResolver.Resolve(CurrentLanguage).Number.FormatCompact(value);
        public int CalcWorldMapRegionProgress(int map, int region, int value) => data.CalcWorldMapRegionProgress(map, region, value);
        public int CalcRankSuppression(int mine, int other) => data.CalcRankSuppression(mine, other);
        public bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color) => data.TryGetLevelColor(type, level, out color);
        public DateTime Now => DateTime.Now;
        public double CalcRemainSecond(DateTime end) => (end - Now).TotalSeconds;
        public object FormatTimeSpan(TimeSpan span, int format) => throw new InvalidOperationException("Map12 has no configured timed region limits.");
        public int Level { get; private set; }
        public int SubRank { get; private set; }
        public long CombatRating { get; private set; }
        public event Action<int> OnLevelUpdate;
        // These events belong to providers excluded by the verified all-zero
        // timed/server-rank fields in this Map12 world-label configuration.
        public event Action<int> OnPlanesRankUnlock { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
        public event Action<long, long> OnCombatRatingChanged;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        private MapRegionStatus State(int region) => state.IsPublicRegion(region) ? MapRegionStatus.Public : MapRegionStatus.Private;
        public bool RegionIsActived(int map, int region) => GetMapRegionStatus(map, region) == MapRegionStatus.Public;
        public MapRegionStatus GetMapRegionStatus(int map, int region) => map != 12 || region < 0 || region >= 99 ? MapRegionStatus.None : State(region);
        public int GetWorldMapRegionProgress(int map, int region) => map != 12 || region < 0 || region >= 99 ? 0 : state.GetRegionProgress(region);
        public void Refresh()
        {
            int nextLevel = state.PlayerLevel;
            if (nextLevel != Level) { Level = nextLevel; OnLevelUpdate?.Invoke(Level); }
            SubRank = state.PlayerSubRank;
            long rating = state.PlayerCombatRating;
            if (CombatRating != rating) { long old = CombatRating; CombatRating = rating; OnCombatRatingChanged?.Invoke(old, rating); }
            foreach (var region in data.regions)
            {
                int id = region.Region; var next = State(id); var old = statuses[id];
                if (next == old) continue;
                statuses[id] = next; OnMapStatusUpdate?.Invoke(12, id, old, next);
            }
            var nextProgress = data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
            if (!progress.SequenceEqual(nextProgress)) { progress = nextProgress; OnWorldMapProgressUpdate?.Invoke(12, progress); }
        }
        public LanguageType CurrentLanguage
        {
            get => LanguageType.zh_CN;
            set { if (value != LanguageType.zh_CN) throw new NotSupportedException("Map12 currently binds the official Simplified Chinese text closure."); }
        }
        public bool IsShowLanguageKey { get; set; }
        public bool IsShowMissingKeyText { get; set; }
        public event Action<LanguageType> OnLanguageChanged { add { } remove { } }
        public bool ContainsKey(string key) => texts.ContainsKey(key);
        public string GetText(string key, LanguageType language, params object[] args) => texts.TryGetValue(key, out string value)
            ? value : throw new KeyNotFoundException("Original Map12 world-label key is unbound: " + key);
        public bool TryGetOriginalText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
        public bool TryGetText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
        public bool TryGetText(string key, out string result) => texts.TryGetValue(key, out result);
        public string GetTextFormated(string key, LanguageType language, params object[] args) => string.Format(GetText(key, language), args);
        public void ReloadData() => throw new NotSupportedException("Map12 original world-label text data is immutable.");
        public void SetReplacer(ITextReplacer replacer) => throw new NotSupportedException("No world-label text replacer is configured.");
        public void AddDataProvider(ILanguageTextDataProvider provider) => throw new NotSupportedException("Map12 original world-label text data is immutable.");
    }
}
