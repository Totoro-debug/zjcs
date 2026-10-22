using System;

namespace Zjcs.Offline.Progression
{
    /// <summary>One entry of the original <c>skill_recommendation</c> table (skills.json).</summary>
    [Serializable]
    public sealed class OfficialSkillRecommendation
    {
        public int Id;
        public int Profession;
        public int[] ActiveSkillList = Array.Empty<int>();
        public int[] PassiveSkillList = Array.Empty<int>();
        public int[] CoreSkillList = Array.Empty<int>();
        public string name;
        public string description;
    }

    [Serializable]
    public sealed class OfficialSkillRecommendationSet
    {
        public OfficialSkillRecommendation[] recommendations = Array.Empty<OfficialSkillRecommendation>();
    }
}
