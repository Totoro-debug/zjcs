using System;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightRandomStream
    {
        public const int Modulus = 2147483647;
        public int Seed;

        public FightRandomStream(int seed)
        {
            if (seed < 1 || seed >= Modulus)
                throw new ArgumentOutOfRangeException(nameof(seed));
            Seed = seed;
        }

        public double Next()
        {
            Seed = (int)((long)Seed * 48271 % Modulus);
            return (double)Seed / Modulus;
        }
    }
}
