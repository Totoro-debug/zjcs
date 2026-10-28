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
        public OfficialMainlineHero[] heroes;
        public OfficialMainlineEntityParameter[] parameters;
        public OfficialMainlineInteractionRegion[] regions;
        public OfficialMainlineInteractionArea[] areas;

        public static OfficialMainlineInteractionCatalog Load(int selectedMapClassId = 11)
        {
            bool currentDragonCountry = selectedMapClassId == 14;
            TextAsset source = Resources.Load<TextAsset>(currentDragonCountry
                ? "Map14/interactions" : "World/Mainline/interactions");
            var result = source == null ? null : JsonUtility.FromJson<OfficialMainlineInteractionCatalog>(source.text);
            if (result == null || result.manifest != (currentDragonCountry ? "871_156598" : "854_156383") || result.heroes?.Length != 4
                || result.parameters == null || result.regions == null || result.areas == null
                || (currentDragonCountry && (result.mapClassId != 14 || result.regions.Length != 111
                    || result.regions.Any(region => region.MapClassId != 14)))
                || result.heroes.Any(hero => hero.PeriodicAwards.Length != 3
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
