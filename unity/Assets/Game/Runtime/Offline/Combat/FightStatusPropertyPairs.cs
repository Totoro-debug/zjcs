namespace Zjcs.Combat.Official
{
    // Original PropType pairs. The shared damage formula consumes their fixed
    // and percentage channels separately; a conditional gate must keep both.
    public static class FightStatusPropertyPairs
    {
        public static int FixedProperty(int percentProperty)
        {
            switch (percentProperty)
            {
                case 231: return 235; // StatusDmgAddPer / FixedStatusDmgAdd
                case 232: return 236; // StatusDmgReducePer / FixedStatusDmgReduce
                case 245: return 246; // StatusIgnoreDefence / FixedStatusIgnoreDefence
                default: return 0;
            }
        }

        public static bool Matches(int property, int percentProperty) => percentProperty > 0
            && (property == percentProperty || property == FixedProperty(percentProperty));
    }
}
