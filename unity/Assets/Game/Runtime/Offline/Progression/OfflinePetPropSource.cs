using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    /// <summary>Original PetPropChunck and CalculatePetPropsAsOrnament for every owned pet.</summary>
    public sealed class OfflinePetPropSource : IOfflineCharacterPropSource
    {
        private readonly OfficialCharacterCatalog catalog;

        public OfflinePetPropSource(OfficialCharacterCatalog characterCatalog)
        {
            catalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
        }

        public int HandleType => CharacterPropHandleType.Pet;

        // BattleFormulaHandler.CalculatePetAbilityProps, also used by the
        // original PetSkillEnergyEntryNavigateHandler outside a battle.
        public IReadOnlyDictionary<int, long> CalculateAbilityProps(int petClassId, List<OfflinePetAbilityLevel> abilities)
        {
            var result = new Dictionary<int, long>();
            var definition = catalog.Find("pet", petClassId)
                ?? throw new InvalidOperationException("Original pet definition is absent: " + petClassId);
            AddAbilityProps(result, definition, abilities);
            HandleSystemPercentProps(result);
            return result;
        }

        public IReadOnlyDictionary<int, long> CalculatePetEntityBaseProps(int petClassId, int level,
            IReadOnlyDictionary<int, long> inheritFixedProps)
        {
            Dictionary<string, object> definition = catalog.Find("pet", petClassId)
                ?? throw new InvalidOperationException("Original pet definition is absent: " + petClassId);
            var result = new Dictionary<int, long>();
            CharacterPropFormula.Merge(result, inheritFixedProps);
            CharacterPropFormula.Merge(result, catalog.GetLevelProps(
                Integer(definition, "EntityExtraPropId"), Math.Max(1, level)));
            CharacterPropFormula.TurnBaseProps(result, catalog.Props);
            return result;
        }

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            if (player?.Pets?.Pets == null) return result;

            foreach (OfflinePetInstance pet in player.Pets.Pets)
                CharacterPropFormula.Merge(result, CalculatePetProps(player, pet));
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context)
        {
            if (player?.Pets?.Pets == null || context == null) return 0L;
            var records = new List<PetCombatRatingRecord>();
            foreach (OfflinePetInstance pet in player.Pets.Pets)
            {
                Dictionary<string, object> definition = pet == null ? null : catalog.Find("pet", pet.ClassId);
                Dictionary<string, object> evolution = pet == null ? null : catalog.Rows("pet_evolution")
                    .FirstOrDefault(row => Integer(row, "ClassId") == pet.ClassId
                        && Integer(row, "EvolutionPhase") == pet.EvolutionPhase);
                List<object> supportSkills = evolution != null
                    && evolution.TryGetValue("SupportSkills", out object rawSkills)
                    ? rawSkills as List<object> : null;
                if (definition == null || supportSkills == null || supportSkills.Count == 0) continue;

                int supportSkillId = Convert.ToInt32(supportSkills[0], CultureInfo.InvariantCulture);
                int supportRank = SkillRank(pet, supportSkillId);
                int entitySkillId = Integer(evolution, "EntitySkill");
                int entityRank = entitySkillId > 0 ? SkillRank(pet, entitySkillId) : 0;
                records.Add(new PetCombatRatingRecord
                {
                    PetClassId = pet.ClassId,
                    PropsRating = context.Common(CalculatePetProps(player, pet)),
                    SupportRating = SkillCombatRating(definition, "SupportSkillLevelId", supportRank, context),
                    EntityRating = entitySkillId > 0
                        ? SkillCombatRating(definition, "EntitySkillLevelId", entityRank, context) : 0L
                });
            }

            if (records.Count == 0) return 0L;
            int bestSupportPet = records.Last(value => value.SupportRating == records.Max(item => item.SupportRating)).PetClassId;
            int bestEntityPet = records.Last(value => value.EntityRating == records.Max(item => item.EntityRating)).PetClassId;
            return records.Sum(value => value.PropsRating)
                + records.Sum(value => value.PetClassId == bestSupportPet
                    ? value.SupportRating : (long)(value.SupportRating * 0.1))
                + records.Sum(value => value.PetClassId == bestEntityPet
                    ? value.EntityRating : (long)(value.EntityRating * 0.1));
        }

        public long CalculatePetCombatRating(OfflinePlayerProgress player, OfflinePetInstance pet,
            CharacterPropScoreContext context)
        {
            return context == null || pet == null ? 0L : context.Common(CalculatePetProps(player, pet));
        }

        private Dictionary<int, long> CalculatePetProps(OfflinePlayerProgress player, OfflinePetInstance pet)
        {
            var result = new Dictionary<int, long>();
            if (player == null || pet == null || pet.ClassId <= 0) return result;
            Dictionary<string, object> definition = catalog.Find("pet", pet.ClassId);
            if (definition == null) return result;

            int finalLevel = Math.Max(1, pet.Level);
            int baseLevel = Math.Min(PetBaseLevelLimit(player.SubRank), finalLevel);
            int blessLevel = finalLevel - baseLevel;
            result = new Dictionary<int, long>(catalog.GetLevelProps(
                PetLevelPropId(definition, Math.Max(0, player.SeasonRank)), baseLevel));
            ApplyBlessScale(result, PetPropClass(player.SubRank), blessLevel);
            AddAbilityProps(result, definition, pet.Abilities);
            HandleSystemPercentProps(result);
            TurnBaseProps(result);
            ScaleByFormationAndPotential(result, pet);
            return result;
        }

        private long SkillCombatRating(Dictionary<string, object> pet, string levelNumberField, int rank,
            CharacterPropScoreContext context)
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            if (settings == null || !settings.TryGetValue("SkillScoreBase", out object rawScoreBase)
                || !(rawScoreBase is List<object> scoreBase)) return 0L;
            int[] propTypes = scoreBase.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
            long basis = context.ByProps(context.BasicRef.RateFormateBaseProps, null, propTypes);
            int levelNumberId = Integer(pet, levelNumberField);
            float factor = context.LevelNumber(levelNumberId, rank);
            return (long)(factor * (float)basis);
        }

        private int SkillRank(OfflinePetInstance pet, int skillId)
        {
            OfflinePetSkillRank saved = pet.SkillRanks?.FirstOrDefault(value => value != null
                && value.SkillId == skillId);
            if (saved != null) return saved.Rank;

            Dictionary<string, object> item = catalog.Find("item", skillId);
            int quality = Integer(item, "Quality");
            return catalog.Rows("skill_rank").Where(row => Integer(row, "Quality") == quality)
                .Select(row => Integer(row, "Rank")).DefaultIfEmpty(0).Min();
        }

        private sealed class PetCombatRatingRecord
        {
            public int PetClassId;
            public long PropsRating;
            public long SupportRating;
            public long EntityRating;
        }

        private int PetPropClass(int subRank)
        {
            Dictionary<string, object> info = catalog.Find("player_subrank", subRank);
            return Integer(info, "PetPropBless");
        }

        private int PetBaseLevelLimit(int subRank)
        {
            int rank = catalog.RankOfSubRank(subRank);
            int highSubRank = catalog.Rows("player_subrank")
                .Where(row => catalog.RankOfSubRank(Integer(row, "SubRank")) == rank)
                .Select(row => Integer(row, "SubRank")).DefaultIfEmpty(subRank).Max();
            return catalog.Rows("system_level_limit")
                .Where(row => Integer(row, "SubRank") == highSubRank && Integer(row, "PetBlessLevel") == 0)
                .Select(row => Integer(row, "PetLevel")).DefaultIfEmpty(1).Max();
        }

        private static int PetLevelPropId(Dictionary<string, object> pet, int seasonId)
        {
            if (!pet.TryGetValue("LevelPropId", out object raw)
                || !(raw is Dictionary<string, object> values)) return 0;
            if (values.TryGetValue(seasonId.ToString(CultureInfo.InvariantCulture), out object value))
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return values.TryGetValue("0", out value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private void ApplyBlessScale(Dictionary<int, long> props, int blessPropClass, int blessLevel)
        {
            if (blessPropClass <= 0 || blessLevel <= 0) return;
            IReadOnlyDictionary<int, long> scale = catalog.GetLevelProps(blessPropClass, blessLevel);
            foreach (int property in props.Keys.ToArray())
                if (scale.TryGetValue(property, out long factor))
                    props[property] = (long)(props[property] * (1.0 + factor / 10000.0));
        }

        private void AddAbilityProps(Dictionary<int, long> destination, Dictionary<string, object> pet,
            List<OfflinePetAbilityLevel> abilities)
        {
            if (abilities == null || abilities.Count == 0) return;
            int abilityClassId = Integer(pet, "AbilityId");
            foreach (OfflinePetAbilityLevel ability in abilities)
            {
                if (ability == null || ability.NodeId <= 0 || ability.Level <= 0) continue;
                Dictionary<string, object> row = catalog.Rows("pet_ability_prop").FirstOrDefault(value =>
                    Integer(value, "ClassId") == abilityClassId
                    && Integer(value, "NodeId") == ability.NodeId
                    && Integer(value, "NodeLevel") == ability.Level);
                if (row == null) continue;

                int propClassId = Integer(row, "PropId");
                int scaleClassId = Integer(row, "PropScaleFactorId");
                Dictionary<string, object> scaleRow = catalog.Find("pet_ability_prop_scale_factor", scaleClassId);
                Dictionary<string, object> scaleFactors = scaleRow != null
                    && scaleRow.TryGetValue("ScaleFactors", out object rawScale)
                    ? rawScale as Dictionary<string, object> : null;
                foreach (KeyValuePair<int, long> property in catalog.GetLevelProps(propClassId, ability.Level))
                {
                    long factor = 0;
                    if (scaleFactors != null
                        && scaleFactors.TryGetValue(property.Key.ToString(CultureInfo.InvariantCulture), out object rawFactor))
                        factor = Convert.ToInt64(rawFactor, CultureInfo.InvariantCulture);
                    Add(destination, property.Key,
                        (long)(property.Value * (1.0 + factor / 10000.0)));
                }
            }
        }

        private void HandleSystemPercentProps(Dictionary<int, long> props)
        {
            ApplySystemPercent(props, "SystemAttackPercent", "Attack");
            ApplySystemPercent(props, "SystemDefencePercent", "Defence");
            ApplySystemPercent(props, "SystemMaxHpPercent", "MaxHp");
            ApplySystemPercent(props, "SystemSpeedPercent", "Speed");
        }

        private void ApplySystemPercent(Dictionary<int, long> props, string percentName, string baseName)
        {
            int percentId = catalog.PropertyId(percentName);
            long percent = Get(props, percentId);
            if (percent == 0) return;
            int baseId = catalog.PropertyId(baseName);
            props[baseId] = (long)(Get(props, baseId) * (1.0 + percent / 10000.0));
            props.Remove(percentId);
        }

        private void TurnBaseProps(Dictionary<int, long> props)
        {
            TurnBase(props, "BaseMaxHp", "MaxHp");
            TurnBase(props, "BaseAttack", "Attack");
            TurnBase(props, "BaseDefence", "Defence");
            TurnBase(props, "BaseSpeed", "Speed");
        }

        private void TurnBase(Dictionary<int, long> props, string sourceName, string destinationName)
        {
            int source = catalog.PropertyId(sourceName);
            long value = Get(props, source);
            if (value == 0) return;
            props.Remove(source);
            int destination = catalog.PropertyId(destinationName);
            props[destination] = checked(Get(props, destination) + value);
        }

        private void ScaleByFormationAndPotential(Dictionary<int, long> props, OfflinePetInstance pet)
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            if (settings == null || !settings.TryGetValue("PetSetting", out object rawPetSetting)
                || !(rawPetSetting is Dictionary<string, object> petSetting)) return;

            double factor = Double(petSetting, "PetIdlePropChangeFactor", 0.2);
            if (pet.SlotIndex >= 0 && petSetting.TryGetValue("PetFormationPropChangeFactor", out object rawFactors)
                && rawFactors is List<object> factors && pet.SlotIndex < factors.Count)
                factor = Convert.ToDouble(factors[pet.SlotIndex], CultureInfo.InvariantCulture);

            int potentialValue = catalog.Rows("pet_potential")
                .Where(row => Integer(row, "Quality") == pet.Quality && Integer(row, "Level") == pet.PotentialLevel)
                .Select(row => Integer(row, "PropValue")).DefaultIfEmpty(0).First();
            factor += potentialValue / 10000.0;
            foreach (int property in props.Keys.ToArray())
                props[property] = (long)(props[property] * factor);
        }

        private static int Integer(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static double Double(Dictionary<string, object> values, string key, double fallback)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static long Get(Dictionary<int, long> props, int property)
        {
            return property > 0 && props.TryGetValue(property, out long value) ? value : 0;
        }

        private static void Add(Dictionary<int, long> props, int property, long value)
        {
            if (property <= 0) return;
            props[property] = checked(Get(props, property) + value);
        }
    }
}
