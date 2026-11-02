using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14Labels;
using Zjcs.Offline.World;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap14Labels;
using LabelRegionStatus = Zjcs.Official.World.NativeMap14Labels.MapRegionStatus;

namespace Zjcs.Presentation.World
{
    public interface IMainlineWorldLabelState
    {
        int PlayerLevel { get; }
        int PlayerSubRank { get; }
        long PlayerCombatRating { get; }
        bool IsPublicRegion(int region);
        int GetRegionProgress(int region);
    }

    /// <summary>Original world labels behind one map-data/player-state Interface.</summary>
    public sealed class MainlineWorldLabels : IDisposable
    {
        private readonly Services services;
        public MainlineWorldLabels(Map14LabelData data, IMainlineWorldLabelState state, TMP_FontAsset font,
            Func<int, bool> regionTimeReached = null, IMapRegionTime regionTime = null)
        {
            services = new Services(data ?? throw new ArgumentNullException(nameof(data)),
                state ?? throw new ArgumentNullException(nameof(state)), font, regionTimeReached, regionTime);
        }
        public void Bind(GameObject inactiveRoot) => services.Bind(inactiveRoot);
        public void Refresh() => services.Refresh();
        public void Dispose() => services.Dispose();
        private sealed class Services : IMap14LabelServices, IMap14LabelPlayer,
            ILanguageTypeService, ILocalizationTextService, IDisposable
        {
            private readonly Map14LabelData data;
            private readonly IMainlineWorldLabelState state;
            private readonly Dictionary<string, string> texts;
            private readonly MultiLanguageUIService ui;
            private readonly Map14LabelFontInstance font;
            private readonly LabelRegionStatus[] statuses;
            private readonly MapRegionInfo[] regions;
            private readonly IMapRegionTime regionTime;
            private int rank;
            private List<int> progress;

            public Services(Map14LabelData data, IMainlineWorldLabelState state, TMP_FontAsset sourceFont,
                Func<int, bool> regionTimeReached, IMapRegionTime regionTime)
            {
                if (data.mapClassId <= 0 || data.width <= 0 || data.height <= 0 || data.origin == null
                    || data.origin.Length != 3 || data.regionGrid == null || data.regionGrid.Length != data.width * data.height
                    || data.regions == null || data.maxProgress == null || data.maxProgress.Length != data.regions.Length
                    || data.regions.Any(region => region == null || region.ClassId != data.mapClassId || region.Region < 0 || region.Region >= data.regions.Length)
                    || data.regions.Select(region => region.Region).Distinct().Count() != data.regions.Length)
                    throw new InvalidDataException("Original world-label region identities or dimensions are malformed.");
                this.data = data;
                this.state = state;
                this.regionTime = regionTime;
                rank = regionTime?.Rank ?? 0;
                // Binding callbacks are instance state. Never mutate the resource's
                // shared region rows when another view may use the same data.
                regions = data.regions.Select(row => JsonUtility.FromJson<MapRegionInfo>(JsonUtility.ToJson(row))).ToArray();
                foreach (var region in regions)
                    if (regionTime != null)
                        region.BindUnlockTime(() => regionTime.IsUnlockTimeReached(region.Region), () => regionTime.GetUnlockTime(region.Region));
                    else if (regionTimeReached != null)
                        region.BindUnlockTimeReached(() => regionTimeReached(region.Region));
                texts = data.texts.ToDictionary(value => value.key, value => value.value);
                font = new Map14LabelFontInstance(sourceFont);
                ui = new MultiLanguageUIService(this);
                Level = state.PlayerLevel;
                SubRank = state.PlayerSubRank;
                CombatRating = state.PlayerCombatRating;
                // Configuration enumeration order need not equal Region (Map14
                // enumerates 76 before 75). State/progress arrays use Region IDs.
                statuses = Enumerable.Range(0, data.regions.Length).Select(Status).ToArray();
                progress = Enumerable.Range(0, data.regions.Length).Select(state.GetRegionProgress).ToList();
            }

            public void Bind(GameObject instance) => Map14LabelBinding.Bind(instance, this, ui, this, this, font);
            public void Dispose() { ui.Dispose(); font.Dispose(); }
            public int MapClassId => data.mapClassId;
            public bool IsMapInitialized => true;
            public event Action Initialized { add { } remove { } }
            public IMap14LabelPlayer Player => this;
            public bool IsTeamMember => false;
            public int QueryRegion(Vector3 position) => data.QueryRegion(position);
            public MapRegionInfo GetRegion(int mapClassId, int region) => regions.Single(value => value.ClassId == mapClassId && value.Region == region);
            public string GetRegionNameKey(int mapClassId, int region) => "region_" + mapClassId + "_" + region;
            public object CompactNumber(long value) => Zjcs.Offline.Progression.OfficialPropFormat.Compact(value);
            public int CalcWorldMapRegionProgress(int mapClassId, int region, int value) => data.CalcWorldMapRegionProgress(mapClassId, region, value);
            public int CalcRankSuppression(int mine, int other) => data.CalcRankSuppression(mine, other);
            public bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color) => data.TryGetLevelColor(type, level, out color);
            public DateTime Now => (regionTime ?? throw new InvalidOperationException("Timed-region clock is not bound.")).Now;
            public double CalcRemainSecond(DateTime endTime) => (endTime - Now).TotalSeconds;
            public object FormatTimeSpan(TimeSpan span, int format) => new CultureTimeSpanArg(span,
                (ETimeSpanFormatType)format, new BoundTimeText(this));
            public int Level { get; private set; }
            public int SubRank { get; private set; }
            public long CombatRating { get; private set; }
            public event Action<int> OnLevelUpdate;
            public event Action<long, long> OnCombatRatingChanged;
            public event Action<int, List<int>> OnWorldMapProgressUpdate;
            public event Action<int, int, LabelRegionStatus, LabelRegionStatus> OnMapStatusUpdate;
            public event Action<int> OnPlanesRankUnlock;
            public event Action<long> OnDebugTimeOffset { add { } remove { } }
            public bool RegionIsActived(int mapClassId, int region) => GetMapRegionStatus(mapClassId, region) == LabelRegionStatus.Public;
            public LabelRegionStatus GetMapRegionStatus(int mapClassId, int region) => mapClassId != MapClassId || region < 0 || region >= statuses.Length
                ? LabelRegionStatus.None : statuses[region];
            public int GetWorldMapRegionProgress(int mapClassId, int region) => mapClassId != MapClassId || region < 0 || region >= statuses.Length
                ? 0 : state.GetRegionProgress(region);

            public void Refresh()
            {
                SubRank = state.PlayerSubRank;
                int nextRank = regionTime?.Rank ?? 0;
                if (nextRank != rank) { rank = nextRank; OnPlanesRankUnlock?.Invoke(rank); }
                int nextLevel = state.PlayerLevel;
                if (nextLevel != Level) { Level = nextLevel; OnLevelUpdate?.Invoke(Level); }
                long nextRating = state.PlayerCombatRating;
                if (nextRating != CombatRating)
                {
                    long previous = CombatRating;
                    CombatRating = nextRating;
                    OnCombatRatingChanged?.Invoke(previous, nextRating);
                }
                foreach (MapRegionInfo region in data.regions)
                {
                    int id = region.Region;
                    LabelRegionStatus next = Status(id), previous = statuses[id];
                    if (next == previous) continue;
                    statuses[id] = next;
                    OnMapStatusUpdate?.Invoke(MapClassId, id, previous, next);
                }
                List<int> nextProgress = Enumerable.Range(0, data.regions.Length).Select(state.GetRegionProgress).ToList();
                if (progress.SequenceEqual(nextProgress)) return;
                progress = nextProgress;
                OnWorldMapProgressUpdate?.Invoke(MapClassId, progress);
            }

            private LabelRegionStatus Status(int region) => state.IsPublicRegion(region)
                ? LabelRegionStatus.Public : LabelRegionStatus.Private;
            public LanguageType CurrentLanguage
            {
                get => LanguageType.zh_CN;
                set { if (value != LanguageType.zh_CN) throw new NotSupportedException("World labels bind the official Simplified Chinese text closure."); }
            }
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged { add { } remove { } }
            public bool ContainsKey(string key) => texts.ContainsKey(key);
            public string GetText(string key, LanguageType language, params object[] args) => texts.TryGetValue(key, out string value)
                ? value : throw new KeyNotFoundException("Original world-label key is unbound: " + key);
            public bool TryGetOriginalText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
            public bool TryGetText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
            public bool TryGetText(string key, out string result) => texts.TryGetValue(key, out result);
            public string GetTextFormated(string key, LanguageType language, params object[] args) => string.Format(GetText(key, language), args);
            public void ReloadData() => throw new NotSupportedException("Original world-label text data is immutable.");
            public void SetReplacer(ITextReplacer replacer) => throw new NotSupportedException("No world-label text replacer is configured.");
            public void AddDataProvider(ILanguageTextDataProvider provider) => throw new NotSupportedException("Original world-label text data is immutable.");
            private sealed class BoundTimeText : ICultureTextFormatter
            {
                private readonly Services services;
                public BoundTimeText(Services services) { this.services = services; }
                public LanguageType LanguageType => services.CurrentLanguage;
                public string GetText(string key) => services.GetText(key, LanguageType);
                public string GetTextFormatted(string key, params object[] args) => services.GetTextFormated(key, LanguageType, args);
            }
        }
    }
}
