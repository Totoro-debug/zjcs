using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    /// <summary>Direct conversion of the shared EC result indices and Map Luban rows.</summary>
    [Serializable] internal sealed class OfficialMainlineInteractionCatalog
    {
        public string manifest;
        public int mapClassId;
        public int[] heroTemplateIds;
        public OfficialMainlineHero[] heroes;
        public OfficialMainlineEntityParameter[] parameters;
        public OfficialMainlineInteractionRegion[] regions;
        public OfficialMainlineInteractionArea[] areas;

        public static OfficialMainlineInteractionCatalog Load(int selectedMapClassId = 11)
        {
            var profile = OfficialMainlineMapProfile.Get(selectedMapClassId);
            return Load(profile.InteractionResource, profile.InteractionManifest, selectedMapClassId, profile.RegionCount);
        }

        public static OfficialMainlineInteractionCatalog Load(string resource, string expectedManifest, int selectedMapClassId, int regionCount)
        {
            TextAsset source = Resources.Load<TextAsset>(resource);
            var result = source == null ? null : JsonUtility.FromJson<OfficialMainlineInteractionCatalog>(source.text);
            int[] expectedHeroes = result?.heroTemplateIds ?? new[] { 6570, 6571, 6572, 6573 };
            if (result == null || result.manifest != expectedManifest || result.heroes == null
                || expectedHeroes.Length == 0 || expectedHeroes.Distinct().Count() != expectedHeroes.Length
                || result.heroes.Any(hero => hero == null)
                || !result.heroes.Select(hero => hero.EntityClassId).SequenceEqual(expectedHeroes)
                || result.parameters == null || result.regions == null || result.areas == null
                || (resource != "World/Mainline/interactions" && (result.mapClassId != selectedMapClassId
                    || result.regions.Length != regionCount || result.regions.Any(region => region == null || region.MapClassId != selectedMapClassId)))
                || result.heroes.Any(hero => hero.Effects == null || hero.PeriodicAwards == null || hero.PeriodicAwards.Length != 3
                    || hero.PeriodicAwards.Sum(award => award.Weight) != 100))
                throw new InvalidDataException("The official shared map interaction catalog is incomplete.");
            return result;
        }
    }

    [Serializable] internal sealed class OfficialMainlineHero
    {
        public int EntityClassId;
        public Map11RuntimeWitchResult[] Effects;
        public Map11RuntimePeriodicityAward[] PeriodicAwards;
    }
    [Serializable] public sealed class OfficialMainlineExtraProp
    {
        public int PropId, Level, PropType;
        public long Value;
    }
    [Serializable] internal sealed class OfficialMainlineEntityParameter
    {
        public int EntityClassId, CompleteState, PublicDefaultState, Progress;
        public OfficialMainlineCost[] CostItems;
    }
    [Serializable] internal sealed class OfficialMainlineCost { public OfficialMainlineCostItem[] CostItems; }
    [Serializable] internal sealed class OfficialMainlineCostItem { public int ItemId; public long Amount; }
    [Serializable] internal sealed class OfficialMainlineInteractionRegion
    {
        public int MapClassId, Region, Level, SubRank;
        public bool ForbidExplore;
        public int[] DropItems;
    }
    [Serializable] internal sealed class OfficialMainlineInteractionArea
    {
        public int ClassId, Area;
        public int[] RefreshGroups, RefreshIntervals;
    }
}
