using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Offline.World.NativeMap13Labels;
using Zjcs.Offline.World.NativeLabelResources;

namespace Zjcs.Presentation.World.NativeMap13
{
    // Supplies the existing local player state to the original Map13 label code.
    internal sealed class Map13WorldLabels : IMap13LabelServices, IMap13LabelPlayer,
        ILanguageTypeService, IDisposable
    {
        private readonly IMap13NativeWorldState state;
        private readonly Map13WorldEnvironment environment;
        private readonly LocalizationBinaryTextService texts;
        private readonly MultiLanguageUIService ui;
        private readonly Map13LabelFontInstance font;
        private readonly MapRegionStatus[] statuses;
        private List<int> progress;
        public Map13LabelData Data => environment.Data;
        public CombatRatingConfig Colors { get; }

        internal Map13WorldLabels(IMap13NativeWorldState state, Map13WorldEnvironment environment, TMP_FontAsset sourceFont)
        {
            this.state = state; this.environment = environment;
            foreach (var region in Data.regions) region.ValidateScope();
            Colors = Resources.Load<CombatRatingConfig>("Map13/Native/combat-rating-config");
            if (!Colors) throw new InvalidOperationException("Original Map13 combat color configuration is unbound.");
            font = new Map13LabelFontInstance(sourceFont);
            texts = new LocalizationBinaryTextService(this, null,
                new ILanguageTextDataProvider[] { new LaunchLanguageBinaryTextDataProvider("Assets/Launch/") },
                new LocalLanguageFiles(Application.streamingAssetsPath));
            texts.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/"));
            LanguageManager.Initialize(texts);
            ui = new MultiLanguageUIService(this);
            Level = state.PlayerLevel; SubRank = (SubRank)state.PlayerSubRank; CombatRating = state.PlayerCombatRating;
            statuses = Data.regions.Select(r => State(r.Region)).ToArray();
            progress = Data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
        }
        public void Bind(GameObject instance) => Map13LabelBinding.Bind(instance, this, ui, this, texts, font);
        public void Dispose() { ui.Dispose(); texts.Dispose(); font.Dispose(); }
        public int MapClassId => 13;
        public object Map => environment.MapGroundRoot;
        public event Action Initialized { add { } remove { } }
        public IMap13LabelPlayer Player => this;
        public bool IsTeamMember => false;
        public int QueryRegion(Vector3 position) => Data.QueryRegion(position);
        public MapRegionInfo GetRegion(int map, int region) => Data.regions.Single(r => r.ClassId == map && r.Region == region);
        public DateTime Now => DateTime.Now;
        public double CalcRemainSecond(DateTime end) => (end - Now).TotalSeconds;
        public int Level { get; private set; }
        public SubRank SubRank { get; private set; }
        public long CombatRating { get; private set; }
        public event Action<int> OnLevelUpdate;
        // All Map13 region rank/date fields are zero, checked by ValidateScope.
        public event Action<Rank> OnPlanesRankUnlock { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
        public event Action<long, long> OnCombatRatingChanged;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        private MapRegionStatus State(int region) => state.IsPublicRegion(region) ? MapRegionStatus.Public : MapRegionStatus.Private;
        public bool RegionIsActived(int map, int region) => GetMapRegionStatus(map, region) == MapRegionStatus.Public;
        public MapRegionStatus GetMapRegionStatus(int map, int region) => map != 13 || region < 0 || region >= 106 ? MapRegionStatus.None : State(region);
        public int GetWorldMapRegionProgress(int map, int region) => map != 13 || region < 0 || region >= 106 ? 0 : state.GetRegionProgress(region);
        public void Refresh()
        {
            int nextLevel = state.PlayerLevel;
            if (nextLevel != Level) { Level = nextLevel; OnLevelUpdate?.Invoke(Level); }
            SubRank = (SubRank)state.PlayerSubRank;
            long rating = state.PlayerCombatRating;
            if (CombatRating != rating) { long old = CombatRating; CombatRating = rating; OnCombatRatingChanged?.Invoke(old, rating); }
            foreach (var region in Data.regions)
            {
                int id = region.Region; var next = State(id); var old = statuses[id];
                if (next == old) continue;
                statuses[id] = next; OnMapStatusUpdate?.Invoke(13, id, old, next);
            }
            var nextProgress = Data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
            if (!progress.SequenceEqual(nextProgress)) { progress = nextProgress; OnWorldMapProgressUpdate?.Invoke(13, progress); }
        }
        public LanguageType CurrentLanguage
        {
            get => LanguageType.zh_CN;
            set { if (value != LanguageType.zh_CN) throw new NotSupportedException("Map13 currently binds the official Simplified Chinese text closure."); }
        }
        public bool IsShowLanguageKey { get; set; }
        public bool IsShowMissingKeyText { get; set; }
        public event Action<LanguageType> OnLanguageChanged { add { } remove { } }
    }
}
