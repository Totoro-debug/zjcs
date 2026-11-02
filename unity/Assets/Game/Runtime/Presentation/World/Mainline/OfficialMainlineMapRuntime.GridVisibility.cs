using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfflineMapGridVisibility mapGridVisibility;
        private OfficialMainlineGridFog gridFog;

        private void InitializeSharedMapVisibility()
        {
            if (MapProfile.GridVisibilityResource == null) return;
            string encoded = mapClassId == 13 ? localSave.map13GridVisibility : localSave.gridVisibility;
            RestoreMapVisibility(string.IsNullOrEmpty(encoded) ? null : Convert.FromBase64String(encoded));
            AttachMapGridFog();
            SaveLocalProgress();
        }

        private void RestoreMapVisibility(byte[] saved)
        {
            bool reattach = gridFog != null;
            gridFog?.Dispose(); gridFog = null;
            var config = JsonUtility.FromJson<OfflineMapGridVisibility.Configuration>(Resources.Load<TextAsset>(MapProfile.GridVisibilityResource).text);
            var bodies = JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(Resources.Load<TextAsset>(MapProfile.GridBodiesResource).text);
            if (mapClassId == 13) config.characterClassId = 3001;
            var nav = session.Definition.Navigation;
            if (config.mapClassId != mapClassId || config.size?.Length != 2
                || config.size[0] != nav.Width || config.size[1] != nav.Height
                || config.characterClassId != nav.CharacterId)
                throw new InvalidDataException("Map visibility inputs differ from the active ground.");
            var placements = session.Definition.Entities.ToDictionary(e => e.Id);
            if (bodies.entities.Length != placements.Count || bodies.entities.Any(e => !placements.TryGetValue(e.placementId, out var p)
                || p.EntityId != e.classId || p.Grid[0] != e.transform.Pos.X || p.Grid[1] != e.transform.Pos.Y))
                throw new InvalidDataException("Map visibility placement source differs from the active map.");
            Func<Pos2d, int> regionAt;
            if (mapClassId == 13)
            {
                // Preserve the legacy Map13 source and save contract.
                if (config.manifest != "871_156598" || config.viewRange != 5 || config.regions?.Length != 106)
                    throw new InvalidDataException("Original Map13 visibility configuration is unbound.");
                var labels = JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
                regionAt = p => p.X < 0 || p.Y < 0 || p.X >= labels.width || p.Y >= labels.height ? -1 : labels.regionGrid[p.Y * labels.width + p.X];
            }
            else
            {
                var areas = new System.Collections.Generic.Dictionary<int, int>();
                foreach (var region in regions.regionConfigs.Values)
                    foreach (int area in region.Areas) areas[area] = region.Region;
                regionAt = p => OfficialMapNavigationView.TryGetCell(nav, p.X, p.Y, out int area, out _)
                    && areas.TryGetValue(area, out int region) ? region : -1;
            }
            var publicRegions = progressionSave.Save.World.ActivatedGoddessRegions.Where(r => r.MapClassId == mapClassId).Select(r => r.Region);
            mapGridVisibility?.Dispose();
            mapGridVisibility = new OfflineMapGridVisibility(config, bodies, new Pos2d(navigation.currentGrid.X, navigation.currentGrid.Y),
                saved, publicRegions, regionAt, id => mapEntities.TryGetValue(id, out var entity) && entity.Active
                    && !localSave.completedEntityPlacements.Contains(id) && !IsMapLogicEntityDestroyed(id));
            if (reattach) AttachMapGridFog();
        }

        private void AttachMapGridFog()
        {
            if (mapGridVisibility == null || MapProfile.GridFogResource == null) return;
            gridFog?.Dispose();
            gridFog = OfficialMainlineGridFog.Create(mapRoot, worldCamera, Resources.Load<GameObject>(MapProfile.GridFogResource),
                session.Definition.Navigation, mapGridVisibility, () => new Pos2d(navigation.currentGrid.X, navigation.currentGrid.Y));
        }

        private void SnapshotMapVisibility()
        {
            if (mapGridVisibility == null) return;
            mapGridVisibility.MovePlayer(new Pos2d(navigation.currentGrid.X, navigation.currentGrid.Y));
            string encoded = Convert.ToBase64String(mapGridVisibility.Snapshot());
            if (mapClassId == 13) localSave.map13GridVisibility = encoded;
            else localSave.gridVisibility = encoded;
        }

        private static bool ValidMapVisibility(string encoded, int width, int height)
        {
            if (string.IsNullOrEmpty(encoded)) return true;
            if (encoded.Length > 87384) return false;
            return OfflineMapGridVisibility.IsValidSnapshot(Convert.FromBase64String(encoded), width, height);
        }

        private void DisposeMapVisibility()
        {
            gridFog?.Dispose(); gridFog = null;
            mapGridVisibility?.Dispose(); mapGridVisibility = null;
        }
    }
}
