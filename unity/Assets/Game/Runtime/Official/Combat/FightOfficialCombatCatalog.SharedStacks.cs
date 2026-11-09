using System;
using System.Collections.Generic;
namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly Dictionary<int, int> sharedStackLimits = new Dictionary<int, int>();
        private void ReadSharedStackLimits(Dictionary<string, object> battleSettings)
        {
            sharedStackLimits.Clear();
            foreach (var pair in Object(Required(battleSettings, "ShareStatusMaxStacks"), "shared status limits"))
                sharedStackLimits.Add(int.Parse(pair.Key, System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt32(pair.Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        private void BindSharedStackDefinition(FightBattleStatusState status, Dictionary<string, object> components)
        {
            var share = Component(components, "FightStatusShareStackComponent");
            status.SharedStackId = share == null ? 0 : Int(share, "ShareId");
            status.SharedStackLimit = 0;
            if (status.SharedStackId > 0 && !sharedStackLimits.TryGetValue(status.SharedStackId, out status.SharedStackLimit))
                throw new InvalidOperationException("Load the original Battle settings before shared status creation.");
        }
        internal void RestoreSharedStackDefinition(FightBattleStatusState status)
        {
            BindSharedStackDefinition(status, RequiredEntity(status.ClassId));
        }
    }
}
