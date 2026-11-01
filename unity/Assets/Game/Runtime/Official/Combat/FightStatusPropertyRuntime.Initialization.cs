using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusPropertyRuntime
    {
        // FightStatusComponentImplement.InitProps: convert entity props first, then
        // replace raw values, scale stacks, and apply this entity's blessing factors.
        private static void InitializeProperties(FightBattleStatusState status,
            FightBattleUnitState owner, IReadOnlyList<FightBattleUnitState> units,
            Dictionary<int, long> ownerProperties, FightOfficialCombatCatalog catalog,
            FightDamageFormula formula)
        {
            if (status.StatusConfigProperties != null && status.StatusProperties != null) return;
            if (!catalog.ContainsEntity(status.ClassId)) return;
            Dictionary<int, long> source = SourceProperties(status, units);
            var config = new Dictionary<int, long>();
            if (catalog.HasEntityPropertyDefinition(status.ClassId))
                AddStatusProperties(config, source, ownerProperties, owner.Hp,
                    Map(catalog.CalculateSkillProps(status.ClassId, status.SourceSkillRank,
                        status.SourceSkillLevel, status.SourceSubRank)), formula);

            foreach (var replacement in status.ReplacementProperties
                ?? new List<FightStatusReplacementValue>())
                if (replacement != null) config[replacement.PropertyId] = replacement.RawValue;

            Dictionary<int, long> properties;
            if (status.IsOpenStack && status.ScalePropWhenChangeStacked)
            {
                properties = new Dictionary<int, long>();
                if (status.StackCount > 0)
                {
                    Merge(properties, config);
                    Scale(properties, status.StackCount);
                }
            }
            else properties = config;

            foreach (var scale in status.BlessPropertyScales ?? new List<FightStatusPropertyScale>())
                if (scale != null && scale.EntityId == status.ClassId
                    && properties.TryGetValue(scale.PropertyId, out long value))
                    properties[scale.PropertyId] = (long)(value * scale.Factor);

            // When scaling is disabled, the original dictionaries alias: blessing
            // changes StatusConfigProps too. Persist their resulting values explicitly.
            status.StatusConfigProperties = ToValues(config);
            status.StatusProperties = ToValues(properties);
            // Older checkpoints can contain an empty projected cache. Recreate it
            // from the recovered initialization data; shield progress stays intact.
            status.ResolvedProperties = null;
        }

        public static void OnStackChanged(FightBattleStatusState status, int previous)
        {
            if (status == null || !status.IsOpenStack || previous == status.StackCount) return;
            // Original ScaleProps returns before clearing at zero or with no props.
            // SetStackedCount does not reapply the initialization blessing factors.
            if (status.ScalePropWhenChangeStacked && status.StackCount > 0
                && status.StatusConfigProperties != null && status.StatusConfigProperties.Count > 0
                && status.StatusProperties != null)
            {
                var properties = Map(status.StatusConfigProperties);
                Scale(properties, status.StackCount);
                status.StatusProperties = ToValues(properties);
            }
            status.ResolvedProperties = null;
        }

        private static Dictionary<int, long> SourceProperties(FightBattleStatusState status,
            IReadOnlyList<FightBattleUnitState> units)
        {
            return status.SourceProperties != null ? Map(status.SourceProperties)
                : UnitProperties(FindUnit(units, status.CreatorId));
        }
    }
}
