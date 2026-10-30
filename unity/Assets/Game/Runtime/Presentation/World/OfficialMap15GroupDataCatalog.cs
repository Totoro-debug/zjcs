using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap15GroupDataCatalog
    {
        private const string Manifest = "854_156383";
        private const string CatalogPath = "Map15/Group/group-catalog";
        private readonly Dictionary<int, MapReference> maps = new Dictionary<int, MapReference>();
        private readonly Dictionary<int, EntityTemplate> templates = new Dictionary<int, EntityTemplate>();

        [Serializable] public sealed class GroupFile
        {
            public string manifest;
            public int mapGroupId;
            public MapReference[] maps;
            public MapCounts counts;
            public string entityTemplatesResource;
        }

        [Serializable] public sealed class MapCounts
        {
            public int maps, regions, placements, placementComponents, templates, templateComponents, components;
        }

        [Serializable] public sealed class MapReference
        {
            public int ClassId, NextMapClassId, RegionCount, PlacementCount, MapBgmId;
            public string MapGroundPath, Resource;
        }

        [Serializable] public sealed class MapFile
        {
            public string manifest;
            public MapReference map;
            public RegionRecord[] regions;
            public RegionData regionData;
            public EntityComponents[] entityComponents;
        }

        [Serializable] public sealed class RegionRecord
        {
            public int ClassId, Region, LimitServerRank, LimitServerRankUnlockDayOffset;
        }

        [Serializable] public sealed class RegionData
        {
            public int id;
            public PlacementData placement;
        }

        [Serializable] public sealed class PlacementData { public Placement[] entities; }

        [Serializable] public sealed class Placement
        {
            public int id, entity, type, region, area, direction;
            public int[] grid;
            public bool unique;
        }

        [Serializable] public sealed class EntityComponents
        {
            public int id;
            public ComponentValue[] components;
        }

        [Serializable] public sealed class ComponentValue { public string name, json; }

        [Serializable] private sealed class TemplateFile
        {
            public string manifest;
            public EntityTemplate[] templates;
        }

        [Serializable] public sealed class EntityTemplate
        {
            public int entityClassId;
            public ComponentValue[] components;
        }

        private OfficialMap15GroupDataCatalog(GroupFile group, TemplateFile templateFile)
        {
            if (group == null || group.manifest != Manifest || group.mapGroupId != 15
                || group.maps == null || group.maps.Length != 60 || group.counts == null
                || group.counts.regions != 118 || group.counts.placements != 2899
                || group.counts.templates != 82 || group.counts.components != 23291)
                throw new InvalidDataException("Official Map15 group catalog is incomplete or stale.");
            if (templateFile == null || templateFile.manifest != Manifest || templateFile.templates == null
                || templateFile.templates.Length != group.counts.templates)
                throw new InvalidDataException("Official Map15 entity templates are incomplete or stale.");

            foreach (MapReference map in group.maps)
            {
                if (map == null || map.ClassId <= 0 || string.IsNullOrEmpty(map.Resource)
                    || !maps.TryAdd(map.ClassId, map))
                    throw new InvalidDataException("Official Map15 map index contains an invalid or duplicate map.");
            }
            foreach (EntityTemplate template in templateFile.templates)
            {
                if (template == null || template.entityClassId <= 0 || template.components == null
                    || !HasComponents(template.components)
                    || !templates.TryAdd(template.entityClassId, template))
                    throw new InvalidDataException("Official Map15 entity template index contains an invalid or duplicate template.");
            }
        }

        public static OfficialMap15GroupDataCatalog LoadDefault()
        {
            TextAsset groupAsset = Resources.Load<TextAsset>(CatalogPath);
            if (groupAsset == null) throw new InvalidOperationException("Official Map15 group catalog is not installed.");
            GroupFile group = JsonUtility.FromJson<GroupFile>(groupAsset.text);
            if (group == null || string.IsNullOrEmpty(group.entityTemplatesResource))
                throw new InvalidDataException("Official Map15 group catalog has no template resource binding.");
            TextAsset templatesAsset = Resources.Load<TextAsset>(group.entityTemplatesResource);
            if (templatesAsset == null) throw new InvalidOperationException("Official Map15 entity templates are not installed.");
            return new OfficialMap15GroupDataCatalog(group, JsonUtility.FromJson<TemplateFile>(templatesAsset.text));
        }

        public MapFile LoadMap(int mapClassId)
        {
            if (!maps.TryGetValue(mapClassId, out MapReference reference))
                throw new ArgumentOutOfRangeException(nameof(mapClassId), "ClassId is not in the official Map15 group: " + mapClassId);
            TextAsset asset = Resources.Load<TextAsset>(reference.Resource);
            if (asset == null) throw new InvalidOperationException("Official Map15 map resource is not installed: " + reference.Resource);
            MapFile map = JsonUtility.FromJson<MapFile>(asset.text);
            if (map == null || map.manifest != Manifest || map.map == null || map.map.ClassId != mapClassId
                || map.map.NextMapClassId != reference.NextMapClassId || map.regions == null
                || map.regions.Length != reference.RegionCount || map.regionData == null
                || map.regionData.id != mapClassId || map.regionData.placement?.entities == null
                || map.entityComponents == null || map.regionData.placement.entities.Length != reference.PlacementCount
                || map.entityComponents.Length != reference.PlacementCount)
                throw new InvalidDataException("Official Map15 map resource failed its catalog closure: " + mapClassId);

            var componentsByPlacement = new Dictionary<int, EntityComponents>();
            foreach (EntityComponents value in map.entityComponents)
                if (value == null || value.id <= 0 || value.components == null
                    || !HasComponents(value.components)
                    || !componentsByPlacement.TryAdd(value.id, value))
                    throw new InvalidDataException("Official Map15 entity component data has an invalid or duplicate placement.");
            var placementIds = new HashSet<int>();
            foreach (Placement placement in map.regionData.placement.entities)
                if (placement == null || placement.id <= 0 || placement.grid == null || placement.grid.Length != 2
                    || !placementIds.Add(placement.id) || !componentsByPlacement.ContainsKey(placement.id)
                    || !templates.ContainsKey(placement.entity))
                    throw new InvalidDataException("Official Map15 placement, components or entity template are missing.");
            return map;
        }

        private static bool HasComponents(ComponentValue[] components)
        {
            if (components.Length == 0) return false;
            foreach (ComponentValue component in components)
                if (component == null || string.IsNullOrEmpty(component.name) || string.IsNullOrEmpty(component.json)) return false;
            return true;
        }

        public bool ContainsMap(int mapClassId) => maps.ContainsKey(mapClassId);
        public int NextMap(int mapClassId) => maps.TryGetValue(mapClassId, out MapReference map) ? map.NextMapClassId : 0;
        public bool TryGetTemplate(int entityClassId, out EntityTemplate template) => templates.TryGetValue(entityClassId, out template);
    }
}
