using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerHealthRules;
using Zjcs.Official.HomePlayerPanelUI;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    // State owned by the scene/property/status services. These server-derived
    // concepts are explicit; there is no empty-status or guessed-rating fallback.
    public interface IOfflineHomePanelState
    {
        long CombatRating { get; }
        long NoBlessCombatRating { get; }
        IReadOnlyCollection<int> Maps { get; }
        IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffects { get; }
        event Action<int,int> BlessLevelDataReset;
        event Action<IHomePanelStatusEffect> StatusEffectDataUpdate;
    }

    public sealed class OfflineHomePanelPlayer : IHomePanelPlayer, IDisposable
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfflineHomeExperiencePlayer experience;
        private readonly OfflinePlayerHealth health;
        private readonly IOfflineHomePanelState state;
        private readonly HomeCureItemQuery cure;
        private Dictionary<int,long> inventory;
        private string name;
        private float hp;
        private long combatRating, noBlessCombatRating;
        private bool disposed;
        public long Id { get; }
        public string Name { get { Check(); return profile.Save.Player.CharacterName; } }
        public int LevelLimit => Experience.LevelLimit;
        public int Rank => Experience.Rank;
        public int BaseLevel => Experience.BaseLevel;
        public int Level => Experience.Level;
        public int SubRank => Experience.SubRank;
        public float HpPercent { get { Check(); return health.HpPercent; } }
        public long CombatRating { get { Check(); return state.CombatRating; } }
        public long NoBlessCombatRating { get { Check(); return state.NoBlessCombatRating; } }
        public int MyPlanesRank => Experience.PlanesRank;
        public IReadOnlyCollection<int> Maps { get { Check(); return state.Maps ?? throw new InvalidOperationException("Active map state is unbound."); } }
        public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffectManager { get { Check(); return state.StatusEffects ?? throw new InvalidOperationException("Player status state is unbound."); } }
        private OfflineHomeExperiencePlayer Experience { get { Check(); return experience; } }
        public event Action OnNameUpdate;
        public event Action<long,long> OnCombatRatingChanged;
        public event Action<long,long> OnNextRankCombatRatingChanged;
        public event Action<float> OnHpUpdate;
        public event Action<int,int> OnBlessLevelDataReset;
        public event Action<IHomePanelStatusEffect> OnStatusEffectDataUpdate;
        public event Action<HomeExperienceItem,long,object> OnItemCountUpdate;
        public event Action<int> OnLevelUpdate;
        public event Action<int> OnSubRankUpdate;
        public event Action OnBlessLevelDataUpdate;
        public event Action<object> OnSeasonRecordDataAdd;
        public event Action<object> OnSeasonRecordDataEndUpdate;

        public OfflineHomePanelPlayer(OfflineMainlineProgressSession profile, OfflineHomeExperiencePlayer experience,
            OfflinePlayerHealth health, IOfflineHomePanelState state, OfflineHomePlayerHealthConfiguration configuration)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.experience = experience ?? throw new ArgumentNullException(nameof(experience));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            Id = profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
            if (Id <= 0) throw new InvalidOperationException("The owning profile must initialize its durable player identity first.");
            cure = new HomeCureItemQuery(configuration.cooking, GetItemAmount);
            inventory = ReadInventory(); name = Name; hp = HpPercent;
            combatRating = CombatRating; noBlessCombatRating = NoBlessCombatRating;
            _ = Maps; _ = StatusEffectManager;
            experience.OnItemCountUpdate += ExperienceItemUpdated;
            experience.OnLevelUpdate += LevelUpdated;
            experience.OnSubRankUpdate += SubRankUpdated;
            experience.OnBlessLevelDataUpdate += BlessUpdated;
            experience.OnSeasonRecordDataAdd += SeasonAdded;
            experience.OnSeasonRecordDataEndUpdate += SeasonEnded;
            health.HpUpdated += HealthUpdated;
            state.BlessLevelDataReset += BlessReset;
            state.StatusEffectDataUpdate += StatusUpdated;
        }

        public bool HasCureItem() { Check(); return cure.HasCureItem(); }
        public bool HasBlessLevel() => Level > BaseLevel;
        public bool IsBlessLevelOpen() => Experience.IsBlessLevelOpen();
        public bool ShowPlayerStoreExpLimit() => Experience.ShowPlayerStoreExpLimit();
        public int GetMaxStoreExpLevel() => Experience.GetMaxStoreExpLevel();
        public bool IsMaxBlessLevel() => Experience.IsMaxBlessLevel();
        public long GetCurBlessPlayerExp() => Experience.GetCurBlessPlayerExp();
        public int GetCurBlessPlayerLevel() => Experience.GetCurBlessPlayerLevel();
        public long GetItemAmount(int id) => Experience.GetItemAmount(id);
        private Dictionary<int,long> ReadInventory() => profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
        private void ExperienceItemUpdated(HomeExperienceItem item,long previous,object source) => OnItemCountUpdate?.Invoke(item,previous,source);
        private void LevelUpdated(int value) => OnLevelUpdate?.Invoke(value);
        private void SubRankUpdated(int value) => OnSubRankUpdate?.Invoke(value);
        private void BlessUpdated() => OnBlessLevelDataUpdate?.Invoke();
        private void SeasonAdded(object value) => OnSeasonRecordDataAdd?.Invoke(value);
        private void SeasonEnded(object value) => OnSeasonRecordDataEndUpdate?.Invoke(value);
        private void HealthUpdated(float value) { hp = value; OnHpUpdate?.Invoke(value); }
        private void BlessReset(int previous,int current) => OnBlessLevelDataReset?.Invoke(previous,current);
        private void StatusUpdated(IHomePanelStatusEffect status) => OnStatusEffectDataUpdate?.Invoke(status);

        public void SynchronizeCommittedPlayer()
        {
            Check(); experience.SynchronizeCommittedPlayer();
            string currentName = Name;
            if (currentName != name) { name = currentName; OnNameUpdate?.Invoke(); }
            float currentHp = HpPercent;
            if (currentHp != hp) { hp = currentHp; OnHpUpdate?.Invoke(currentHp); }
            long currentRating = CombatRating, currentNoBless = NoBlessCombatRating;
            if (currentRating != combatRating) { long previous = combatRating; combatRating = currentRating; OnCombatRatingChanged?.Invoke(previous,currentRating); }
            if (currentNoBless != noBlessCombatRating) { long previous = noBlessCombatRating; noBlessCombatRating = currentNoBless; OnNextRankCombatRatingChanged?.Invoke(previous,currentNoBless); }
            var nextInventory = ReadInventory(); var previousInventory = inventory; inventory = nextInventory;
            foreach (int id in previousInventory.Keys.Union(nextInventory.Keys).OrderBy(value => value))
            {
                if (id == experience.Tables.PlayerExpItemId) continue;
                previousInventory.TryGetValue(id,out long previous); nextInventory.TryGetValue(id,out long current);
                if (previous != current) OnItemCountUpdate?.Invoke(new HomeExperienceItem { Id = id },previous,null);
            }
        }

        private void Check() { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomePanelPlayer)); }
        public void Dispose()
        {
            if (disposed) return;
            experience.OnItemCountUpdate -= ExperienceItemUpdated; health.HpUpdated -= HealthUpdated;
            experience.OnLevelUpdate -= LevelUpdated; experience.OnSubRankUpdate -= SubRankUpdated;
            experience.OnBlessLevelDataUpdate -= BlessUpdated; experience.OnSeasonRecordDataAdd -= SeasonAdded;
            experience.OnSeasonRecordDataEndUpdate -= SeasonEnded;
            state.BlessLevelDataReset -= BlessReset; state.StatusEffectDataUpdate -= StatusUpdated;
            disposed = true; OnNameUpdate = null; OnCombatRatingChanged = null; OnNextRankCombatRatingChanged = null;
            OnHpUpdate = null; OnBlessLevelDataReset = null; OnStatusEffectDataUpdate = null; OnItemCountUpdate = null;
            OnLevelUpdate = null; OnSubRankUpdate = null; OnBlessLevelDataUpdate = null;
            OnSeasonRecordDataAdd = null; OnSeasonRecordDataEndUpdate = null;
        }
    }
}
