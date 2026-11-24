using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusPropertyRuntime
    {
        public static void RebuildAll(IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightDamageFormula formula)
        {
            if (units == null || catalog == null || formula == null) return;
            var hpChanges = new List<KeyValuePair<FightBattleUnitState, long>>();
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null) continue;
                long previousHp = unit.Hp;
                Rebuild(unit, units, catalog, formula);
                if (unit.Hp != previousHp)
                    hpChanges.Add(new KeyValuePair<FightBattleUnitState, long>(unit, unit.Hp - previousHp));
            }
            foreach (var change in hpChanges)
            {
                // Freeze every delta before callbacks may recursively rebuild other units.
                change.Key.HpUpdated?.Invoke(change.Key);
                if (change.Value > 0) change.Key.HpIncreased?.Invoke(change.Key, change.Value);
            }
        }

        public static void Rebuild(FightBattleUnitState unit,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightDamageFormula formula)
        {
            if (unit == null || catalog == null || formula == null) return;
            if (unit.Properties == null) unit.Properties = new List<FightPropertyValue>();
            if (unit.BaseProperties == null) unit.BaseProperties = Copy(unit.Properties);

            var properties = Map(unit.BaseProperties);
            foreach (FightBattleStatusState status in unit.Statuses ?? new List<FightBattleStatusState>())
            {
                if (status == null) continue;
                EnsurePropertyDefinition(status, catalog);
                InitializeProperties(status, unit, units, properties, catalog, formula);
                if ((status.HasPropertyComponent || status.ShieldKind != FightBattleShieldKind.None
                    || status.HasCreatorDamageBoost) && status.ResolvedProperties == null)
                    status.ResolvedProperties = ToValues(Resolve(status, unit, units, properties,
                        catalog, formula));
                FightBattleShieldRuntime.Initialize(status, status.ResolvedProperties,
                    formula.PropertyId("Shield"));
                // FightStatusPropComponent removes its contribution while forbidden;
                // cached values, layers and the live instance remain available on release.
                if (status.HasPropertyComponent && status.ForbiddenType != 0) continue;
                if (!status.HasPropertyComponent)
                {
                    if (status.ShieldKind != FightBattleShieldKind.None && status.ShieldInitialized)
                        Add(properties, formula.PropertyId("Shield"), status.ShieldRemainValue);
                    continue;
                }
                Dictionary<int, long> resolved = Map(status.ResolvedProperties);
                if (status.DamageReducePer != null)
                {
                    // This component owns the conditional pair. Merge it only for
                    // an eligible incoming hit, alongside the component's counters.
                    int percent = formula.PropertyId("StatusDmgReducePer");
                    resolved.Remove(percent);
                    resolved.Remove(FightStatusPropertyPairs.FixedProperty(percent));
                }
                int shieldId = formula.PropertyId("Shield");
                if (status.ShieldKind != FightBattleShieldKind.None
                    && status.ShieldInitialized && resolved.ContainsKey(shieldId))
                    resolved[shieldId] = status.ShieldRemainValue;
                Merge(properties, resolved);
            }

            ApplyScaleProperties(properties, formula);
            ApplyFixedProperties(properties, formula);
            ApplyFinalHpScale(properties, formula);

            long oldMaxHp = unit.MaxHp > 0 ? unit.MaxHp : unit.LastMaxHp;
            int maxHpId = formula.PropertyId("MaxHp");
            long newMaxHp = Raw(properties, maxHpId);
            if (newMaxHp > 0)
            {
                if (oldMaxHp > 0 && oldMaxHp != newMaxHp && unit.Hp > 0)
                {
                    BigInteger scaledHp = (BigInteger)Math.Min(unit.Hp, oldMaxHp) * newMaxHp;
                    unit.Hp = (long)((scaledHp + oldMaxHp - 1) / oldMaxHp);
                }
                unit.LastMaxHp = newMaxHp;
                unit.MaxHp = newMaxHp;
            }
            unit.Properties = ToValues(properties);
            unit.MoveDist = formula.CalculateMoveDist(properties);
        }

        private static void EnsurePropertyDefinition(FightBattleStatusState status,
            FightOfficialCombatCatalog catalog)
        {
            if (status.PropertyDefinitionResolved) return;
            status.PropertyDefinitionResolved = true;
            if (!catalog.ContainsEntity(status.ClassId)) return;

            FightBattleStatusState definition = catalog.CreateStatus(status.ClassId,
                status.OwnerId, status.CreatorId, status.InstanceId, status.RootSkillId,
                status.ApplyingSkillItemId, status.SourceSkillRank, status.SourceSkillLevel,
                status.SourceSubRank, status.SourceProperties, replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            status.HasPropertyComponent = definition.HasPropertyComponent;
            status.HasCreatorDamageBoost = definition.HasCreatorDamageBoost;
            status.PropertyTransitions = definition.PropertyTransitions;
            if (status.SourceProperties == null)
                status.SourceProperties = definition.SourceProperties;
        }

        internal static void ResolveForApplication(FightBattleStatusState status,
            FightBattleUnitState owner, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightDamageFormula formula)
        {
            EnsurePropertyDefinition(status, catalog);
            var properties = Map(owner.BaseProperties ?? owner.Properties);
            InitializeProperties(status, owner, units, properties, catalog, formula);
            if (status.ResolvedProperties == null)
                status.ResolvedProperties = ToValues(Resolve(status, owner, units, properties, catalog, formula));
        }

        private static Dictionary<int, long> Resolve(FightBattleStatusState status,
            FightBattleUnitState owner, IReadOnlyList<FightBattleUnitState> units,
            Dictionary<int, long> ownerProperties, FightOfficialCombatCatalog catalog,
            FightDamageFormula formula)
        {
            var statusProperties = Map(status.StatusProperties);
            if (!status.HasPropertyComponent) return statusProperties;
            Dictionary<int, long> sourceProperties = SourceProperties(status, units);
            sourceProperties = ResolveByStatusShieldProperties(status, sourceProperties, units,
                catalog, formula);
            var result = new Dictionary<int, long>();
            foreach (FightBattleStatusPropertyTransition transition in status.PropertyTransitions
                ?? new List<FightBattleStatusPropertyTransition>())
            {
                if (transition == null || transition.PropertyId <= 0) continue;
                Dictionary<int, long> targetProperties = TransitionTarget(status, transition.TargetType,
                    owner, units, ownerProperties, sourceProperties);
                if (targetProperties == null) continue;
                double targetValue = formula.ReadProperty(targetProperties, transition.TargetPropertyId);
                double addition = formula.ReadProperty(statusProperties, transition.AdditionPropertyId);
                double fixedAddition = formula.ReadProperty(statusProperties,
                    transition.FixedAdditionPropertyId);
                Add(result, transition.PropertyId, (long)(targetValue * addition + fixedAddition));
            }

            AddStatusProperties(result, sourceProperties, ownerProperties, owner.Hp,
                statusProperties, formula);
            if (status.IsOpenStack && status.StackCount > 0) Scale(result, status.StackCount);
            return result;
        }

        private static Dictionary<int, long> ResolveByStatusShieldProperties(
            FightBattleStatusState shield, Dictionary<int, long> sourceProperties,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightDamageFormula formula)
        {
            if (shield == null || sourceProperties == null || units == null
                || !IsShieldStatus(shield, formula)) return sourceProperties;
            FightBattleUnitState creator = FindUnit(units, shield.CreatorId);
            if (creator == null) return sourceProperties;

            // Confirmed 10593 scope includes shields the player casts on allies. Pet casts
            // reuse the owner's unit id, so retain the actual creator role in this check.
            bool playerCreatedShield = creator.CharacterType == 1
                && shield.CreatorCharacterType == 1;

            int[] shieldModifiers = {
                formula.PropertyId("StatusShieldAddPercent"),
                formula.PropertyId("StatusFixedShieldAdd")
            };
            if (shieldModifiers.All(propertyId => propertyId <= 0)) return sourceProperties;
            var remove = new Dictionary<int, long>();
            foreach (FightBattleStatusState modifier in creator.Statuses ?? new List<FightBattleStatusState>())
            {
                FightBattleStatusByStatusPropertyState definition = modifier?.Components?.ShieldByStatusProperty;
                if (definition == null || !definition.IsShieldStatus || modifier.ForbiddenType != 0
                    || modifier.OwnerId != creator.Id) continue;
                EnsurePropertyDefinition(modifier, catalog);
                InitializeProperties(modifier, creator, units,
                    Map(creator.BaseProperties ?? creator.Properties), catalog, formula);
                if (modifier.ResolvedProperties == null)
                    modifier.ResolvedProperties = ToValues(Resolve(modifier, creator, units,
                        Map(creator.BaseProperties ?? creator.Properties), catalog, formula));
                if (playerCreatedShield && IncludesShieldProperty(definition, shield)) continue;
                IList<FightPropertyValue> properties = modifier.ResolvedProperties
                    ?? modifier.StatusProperties ?? modifier.StatusConfigProperties;
                if (properties == null) continue;
                foreach (FightPropertyValue property in properties)
                    if (property != null && shieldModifiers.Contains(property.Id))
                    {
                        remove.TryGetValue(property.Id, out long current);
                        remove[property.Id] = current + property.Value;
                    }
            }
            foreach (KeyValuePair<int, long> entry in remove)
                if (entry.Value > 0 && sourceProperties.TryGetValue(entry.Key, out long value) && value > 0)
                    sourceProperties[entry.Key] = value - Math.Min(value, entry.Value);
            return sourceProperties;
        }

        private static bool IncludesShieldProperty(FightBattleStatusByStatusPropertyState definition,
            FightBattleStatusState shield)
        {
            if (definition.IncludePropertyTypes == null || definition.IncludePropertyTypes.Length == 0)
                return true;
            IList<FightPropertyValue> properties = shield.StatusProperties
                ?? shield.ResolvedProperties ?? shield.StatusConfigProperties;
            if (properties == null) return false;
            foreach (FightPropertyValue property in properties)
                if (property != null && property.Value != 0
                    && definition.IncludePropertyTypes.Contains(property.Id)) return true;
            return false;
        }

        private static bool IsShieldStatus(FightBattleStatusState status, FightDamageFormula formula)
        {
            if (status.ShieldKind != FightBattleShieldKind.None) return true;
            int fixedShield = formula.PropertyId("SkillFixedShield");
            int[] shieldProperties = {
                fixedShield,
                formula.PropertyId("ShieldByDefence"),
                formula.PropertyId("ShieldByAttack"),
                formula.PropertyId("ShieldByHp"),
                formula.PropertyId("ShieldByTargetHp"),
                formula.PropertyId("ShieldByConvertedCurHp")
            };
            IList<FightPropertyValue> properties = status.StatusProperties
                ?? status.ResolvedProperties ?? status.StatusConfigProperties;
            if (properties == null) return false;
            foreach (FightPropertyValue property in properties)
                if (property != null && property.Value != 0 && shieldProperties.Contains(property.Id)) return true;
            return false;
        }

        private static Dictionary<int, long> TransitionTarget(FightBattleStatusState status,
            int targetType, FightBattleUnitState owner, IReadOnlyList<FightBattleUnitState> units,
            Dictionary<int, long> ownerProperties, Dictionary<int, long> sourceProperties)
        {
            if (targetType == FightStatusLifecycle.TargetCreator) return sourceProperties;
            int targetId;
            switch (targetType)
            {
                case FightStatusLifecycle.TargetApplicator:
                    return ownerProperties;
                case FightStatusLifecycle.TargetCreatorMaster:
                    targetId = MasterOf(status.CreatorId, units);
                    break;
                case FightStatusLifecycle.TargetApplicatorMaster:
                    targetId = MasterOf(status.OwnerId > 0 ? status.OwnerId : owner.Id, units);
                    break;
                default:
                    return null;
            }
            return UnitProperties(FindUnit(units, targetId));
        }

        private static void AddStatusProperties(Dictionary<int, long> result,
            Dictionary<int, long> sourceProperties, Dictionary<int, long> targetProperties,
            long targetCurrentHp, Dictionary<int, long> statusProperties,
            FightDamageFormula formula)
        {
            int shieldId = formula.PropertyId("Shield");
            foreach (KeyValuePair<int, long> property in statusProperties)
            {
                switch (formula.PropertyName(property.Key))
                {
                    case "SkillFixedShield":
                        Add(result, shieldId, (long)formula.ReadProperty(statusProperties, property.Key));
                        break;
                    case "ShieldByAttack":
                        Add(result, shieldId, (long)(Read(sourceProperties, formula, "Attack")
                            * formula.ReadProperty(statusProperties, property.Key)));
                        break;
                    case "ShieldByHp":
                        Add(result, shieldId, (long)(Read(sourceProperties, formula, "MaxHp")
                            * formula.ReadProperty(statusProperties, property.Key)));
                        break;
                    case "ShieldByTargetHp":
                        Add(result, shieldId, (long)(Read(targetProperties, formula, "MaxHp")
                            * formula.ReadProperty(statusProperties, property.Key)));
                        break;
                    case "ShieldByDefence":
                        Add(result, shieldId, (long)(Read(sourceProperties, formula, "Defence")
                            * formula.ReadProperty(statusProperties, property.Key)));
                        break;
                    case "AttackByAttack":
                        Add(result, formula.PropertyId("Attack"),
                            (long)(Read(sourceProperties, formula, "Attack")
                                * formula.ReadProperty(statusProperties, property.Key)));
                        break;
                    case "ShieldByConvertedCurHp":
                        Add(result, formula.PropertyId("ConvertedCurHpShield"), targetCurrentHp > 1
                            ? (long)(targetCurrentHp * formula.ReadProperty(statusProperties, property.Key)) : 0L);
                        break;
                    default:
                        Add(result, property.Key, property.Value);
                        break;
                }
            }

            double shield = Raw(result, shieldId);
            shield = shield * (1.0 + Read(sourceProperties, formula, "StatusShieldAddPercent"))
                + Read(sourceProperties, formula, "StatusFixedShieldAdd");
            shield = Math.Max(0.0, shield);
            if (shield > 0.0) result[shieldId] = (long)shield;

            double finalShieldScale = Read(sourceProperties, formula, "FinalShieldScale");
            shield = Raw(result, shieldId);
            if (finalShieldScale != 0.0 && shield != 0.0)
                result[shieldId] = Math.Max(0L, (long)(shield * (1.0 + finalShieldScale)));
        }

        private static void ApplyScaleProperties(Dictionary<int, long> properties,
            FightDamageFormula formula)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed", "EffectDodge" })
            {
                int scaleId = formula.PropertyId(name + "Scale");
                double scale = formula.ReadProperty(properties, scaleId);
                if (scale == 0.0) continue;
                int propertyId = formula.PropertyId(name);
                long value = Raw(properties, propertyId);
                long reference = value + Raw(properties, formula.PropertyId("Base" + name));
                properties[propertyId] = Math.Max(1L, value + (long)(reference * scale));
                properties.Remove(scaleId);
            }
        }

        private static void ApplyFixedProperties(Dictionary<int, long> properties,
            FightDamageFormula formula)
        {
            AddFixed(properties, formula, "FixedAttack", "Attack");
            AddFixed(properties, formula, "FixedDefence", "Defence");
            AddFixed(properties, formula, "FixedMaxHp", "MaxHp");
            AddFixed(properties, formula, "FixedSpeed", "Speed");
            AddFixed(properties, formula, "FixedDmgReduce", "DmgReduce");
            AddFixed(properties, formula, "FixedDmgAdd", "DmgAdd");
            AddFixed(properties, formula, "FixedCritPower", "CritPower");
            AddFixed(properties, formula, "FixedBlockValue", "BlockValue");
            AddFixed(properties, formula, "FixedMoveDist", "MoveDist");
        }

        private static void AddFixed(Dictionary<int, long> properties, FightDamageFormula formula,
            string fixedName, string propertyName)
        {
            int fixedId = formula.PropertyId(fixedName);
            double value = formula.ReadProperty(properties, fixedId);
            if (value != 0.0) Add(properties, formula.PropertyId(propertyName), (long)value);
        }

        private static void ApplyFinalHpScale(Dictionary<int, long> properties,
            FightDamageFormula formula)
        {
            int scaleId = formula.PropertyId("FinalHpScale");
            double scale = formula.ReadProperty(properties, scaleId);
            if (scale == 0.0) return;
            int maxHpId = formula.PropertyId("MaxHp");
            properties[maxHpId] = (long)(Raw(properties, maxHpId) * (1.0 + scale));
            properties.Remove(scaleId);
        }

        private static double Read(Dictionary<int, long> properties,
            FightDamageFormula formula, string propertyName)
        {
            return formula.ReadProperty(properties, formula.PropertyId(propertyName));
        }

        private static Dictionary<int, long> UnitProperties(FightBattleUnitState unit)
        {
            return unit == null ? new Dictionary<int, long>() : Map(unit.Properties);
        }

        private static FightBattleUnitState FindUnit(IReadOnlyList<FightBattleUnitState> units, int id)
        {
            if (units == null || id <= 0) return null;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].Id == id) return units[i];
            return null;
        }

        private static int MasterOf(int id, IReadOnlyList<FightBattleUnitState> units)
        {
            FightBattleUnitState unit = FindUnit(units, id);
            return unit == null ? 0 : unit.MasterId;
        }

        private static Dictionary<int, long> Map(IEnumerable<FightPropertyValue> values)
        {
            var result = new Dictionary<int, long>();
            if (values == null) return result;
            foreach (FightPropertyValue value in values)
                if (value != null && value.Id > 0) Add(result, value.Id, value.Value);
            return result;
        }

        private static Dictionary<int, long> Map(IReadOnlyDictionary<int, long> values)
        {
            var result = new Dictionary<int, long>();
            if (values == null) return result;
            foreach (KeyValuePair<int, long> value in values) result[value.Key] = value.Value;
            return result;
        }

        private static List<FightPropertyValue> ToValues(Dictionary<int, long> properties)
        {
            var result = new List<FightPropertyValue>();
            foreach (KeyValuePair<int, long> property in properties)
                result.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
            return result;
        }

        private static List<FightPropertyValue> Copy(IEnumerable<FightPropertyValue> properties)
        {
            var result = new List<FightPropertyValue>();
            if (properties == null) return result;
            foreach (FightPropertyValue property in properties)
                if (property != null) result.Add(new FightPropertyValue
                { Id = property.Id, Value = property.Value });
            return result;
        }

        private static long Raw(IDictionary<int, long> properties, int id)
        {
            return id > 0 && properties != null && properties.TryGetValue(id, out long value)
                ? value : 0L;
        }

        private static void Merge(Dictionary<int, long> target, Dictionary<int, long> addition)
        {
            foreach (KeyValuePair<int, long> property in addition) Add(target, property.Key, property.Value);
        }

        private static void Add(Dictionary<int, long> properties, int id, long value)
        {
            if (id <= 0) return;
            properties[id] = Raw(properties, id) + value;
        }

        private static void Scale(Dictionary<int, long> properties, int scale)
        {
            foreach (int id in new List<int>(properties.Keys))
                properties[id] = unchecked(properties[id] * scale);
        }
    }
}
