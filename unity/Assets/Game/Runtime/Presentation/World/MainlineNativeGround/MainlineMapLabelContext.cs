using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Offline.World;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13Labels;

namespace Zjcs.Presentation.World
{
    // Original world/minimap label ports over the shared map's local state.
    // Callers bind inactive original prefabs, refresh state, then dispose after
    // those views release their subscriptions. Source data/assets are borrowed.
    public sealed class MainlineMapLabelContext : IMap13LabelServices, IMap13LabelPlayer,
        ILanguageTypeService, IDisposable
    {
        private readonly IMainlineWorldLabelState state;
        private readonly object map;
        private readonly IMapRegionTime regionTime;
        private readonly LocalizationBinaryTextService texts;
        private readonly MultiLanguageUIService ui;
        private readonly Map13LabelFontInstance font;
        private readonly MapRegionStatus[] statuses;
        private List<int> progress;
        private int rank;
        private bool disposed;

        public MainlineMapLabelContext(Map13LabelData source, IMainlineWorldLabelState state, object map,
            CombatRatingConfig colors, TMP_FontAsset sourceFont, ILocalRawFileLoader languageFiles,
            IMapRegionTime regionTime = null)
        {
            if (source == null || source.mapClassId <= 0 || source.width <= 0 || source.height <= 0
                || source.origin == null || source.origin.Length != 3 || source.regionGrid == null
                || source.regionGrid.Length != source.width * source.height || source.regions == null
                || source.maxProgress == null || source.maxProgress.Length != source.regions.Length
                || source.regions.Any(row => row == null || row.ClassId != source.mapClassId
                    || row.Region < 0 || row.Region >= source.regions.Length)
                || source.regions.Select(row => row.Region).Distinct().Count() != source.regions.Length)
                throw new InvalidDataException("Original label map identities or dimensions are malformed.");
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            Colors = colors ? colors : throw new ArgumentNullException(nameof(colors));
            if (languageFiles == null) throw new ArgumentNullException(nameof(languageFiles));
            if (regionTime != null && regionTime.MapClassId != source.mapClassId)
                throw new InvalidDataException("Original label data and region clock belong to different maps.");
            this.regionTime = regionTime;
            rank = regionTime?.Rank ?? 0;
            // A second map view must not replace callbacks or arrays in the
            // source resource, the terrain environment, or another open map.
            Data = JsonUtility.FromJson<Map13LabelData>(JsonUtility.ToJson(source));
            foreach (var row in Data.regions)
                if (regionTime == null) row.ValidateScope();
                else row.BindUnlockTime(() => regionTime.IsUnlockTimeReached(row.Region),
                    () => regionTime.GetUnlockTime(row.Region));
            Level = state.PlayerLevel;
            SubRank = (SubRank)state.PlayerSubRank;
            CombatRating = state.PlayerCombatRating;
            statuses = Enumerable.Range(0, Data.regions.Length).Select(Status).ToArray();
            progress = Enumerable.Range(0, Data.regions.Length).Select(state.GetRegionProgress).ToList();
            try
            {
                font = new Map13LabelFontInstance(sourceFont);
                texts = new LocalizationBinaryTextService(this, null,
                    new ILanguageTextDataProvider[] { new LaunchLanguageBinaryTextDataProvider("Assets/Launch/") }, languageFiles);
                texts.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/"));
                LanguageManager.Initialize(texts);
                ui = new MultiLanguageUIService(this);
            }
            catch { Dispose(); throw; }
        }

        public void Bind(GameObject inactiveRoot)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMapLabelContext));
            Map13LabelBinding.Bind(inactiveRoot, this, ui, this, texts, font);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ui?.Dispose(); texts?.Dispose(); font?.Dispose();
        }
        public Map13LabelData Data { get; }
        public CombatRatingConfig Colors { get; }
        public int MapClassId => Data.mapClassId;
        public object Map => disposed ? null : map;
        public event Action Initialized { add { } remove { } }
        public IMap13LabelPlayer Player => this;
        public bool IsTeamMember => false;
        public int QueryRegion(Vector3 position) => Data.QueryRegion(position);
        public MapRegionInfo GetRegion(int mapClassId, int region) => Data.regions.Single(row => row.ClassId == mapClassId && row.Region == region);
        public DateTime Now => regionTime?.Now ?? DateTime.Now;
        public double CalcRemainSecond(DateTime endTime) => (endTime - Now).TotalSeconds;
        public int Level { get; private set; }
        public SubRank SubRank { get; private set; }
        public long CombatRating { get; private set; }
        public event Action<int> OnLevelUpdate;
        public event Action<Rank> OnPlanesRankUnlock;
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
        public event Action<long, long> OnCombatRatingChanged;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        private MapRegionStatus Status(int region) => state.IsPublicRegion(region) ? MapRegionStatus.Public : MapRegionStatus.Private;
        public bool RegionIsActived(int mapClassId, int region) => GetMapRegionStatus(mapClassId, region) == MapRegionStatus.Public;
        public MapRegionStatus GetMapRegionStatus(int mapClassId, int region) => mapClassId != MapClassId || region < 0 || region >= statuses.Length
            ? MapRegionStatus.None : Status(region);
        public int GetWorldMapRegionProgress(int mapClassId, int region) => mapClassId != MapClassId || region < 0 || region >= statuses.Length
            ? 0 : state.GetRegionProgress(region);

        public void Refresh()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMapLabelContext));
            // Preserve the original world-label notification order.
            int nextLevel = state.PlayerLevel;
            if (nextLevel != Level) { Level = nextLevel; OnLevelUpdate?.Invoke(Level); }
            SubRank = (SubRank)state.PlayerSubRank;
            long rating = state.PlayerCombatRating;
            if (CombatRating != rating) { long previous = CombatRating; CombatRating = rating; OnCombatRatingChanged?.Invoke(previous, rating); }
            int nextRank = regionTime?.Rank ?? 0;
            if (nextRank != rank) { rank = nextRank; OnPlanesRankUnlock?.Invoke((Rank)rank); }
            foreach (var row in Data.regions)
            {
                int id = row.Region; var next = Status(id); var previous = statuses[id];
                if (next == previous) continue;
                statuses[id] = next; OnMapStatusUpdate?.Invoke(MapClassId, id, previous, next);
            }
            var nextProgress = Enumerable.Range(0, Data.regions.Length).Select(state.GetRegionProgress).ToList();
            if (!progress.SequenceEqual(nextProgress)) { progress = nextProgress; OnWorldMapProgressUpdate?.Invoke(MapClassId, progress); }
        }
        public LanguageType CurrentLanguage
        {
            get => LanguageType.zh_CN;
            set { if (value != LanguageType.zh_CN) throw new NotSupportedException("World labels bind the official Simplified Chinese text closure."); }
        }
        public bool IsShowLanguageKey { get; set; }
        public bool IsShowMissingKeyText { get; set; }
        public event Action<LanguageType> OnLanguageChanged { add { } remove { } }
    }
}
