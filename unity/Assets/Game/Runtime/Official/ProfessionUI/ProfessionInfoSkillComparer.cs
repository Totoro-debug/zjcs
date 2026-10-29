namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionInfoSkillComparer
    {
        public static ProfessionInfoSkillComparer sDefault { get; } = new ProfessionInfoSkillComparer();
        public int DefaultComparer(IProfessionInfoSkill x, IProfessionInfoSkill y)
        {
            long longValue = (long)y.Rank - x.Rank;
            if (longValue == 0L) return 0;
            if (longValue <= 0) return -1;
            return 1;
        }
    }
}
