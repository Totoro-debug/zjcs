using System;
using System.Collections.Generic;
using Zjcs.Offline.World;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialWorldMapSession
    {
        public WorldMapAssets Assets { get; }
        public OfficialMapDefinition Definition => Assets.Definition;
        public OfficialMapGroundRules Movement { get; private set; }

        internal void SetMovement(OfficialMapGroundRules movement) =>
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));

        private OfficialWorldMapSession(WorldMapAssets assets)
        {
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Movement = new OfficialMapGroundRules(assets.Definition);
        }

        public static OfficialWorldMapSession Load(IWorldMapAssets assetProvider, int classId)
        {
            if (assetProvider == null) throw new ArgumentNullException(nameof(assetProvider));
            WorldMapAssets assets = assetProvider.Load(classId);
            if (assets.Definition.ClassId != classId)
                throw new InvalidOperationException("Loaded map definition differs from requested ClassId " + classId);
            return new OfficialWorldMapSession(assets);
        }

        public static OfficialWorldMapSession Load(OfficialWorldMapAssetCatalog catalog, int classId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            WorldMapAssets assets = catalog.LoadMap(classId);
            if (assets.Definition.ClassId != classId)
                throw new InvalidOperationException("Loaded map definition differs from requested ClassId " + classId);
            return new OfficialWorldMapSession(assets);
        }

        public List<MapGridPoint> FindPath(OfficialMapPathRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            OfficialMapNavigation navigation = Definition.Navigation;
            if (navigation == null)
                throw new InvalidOperationException("Official map navigation is missing for ClassId " + Definition.ClassId);

            var boundedRequest = new OfficialMapPathRequest
            {
                Start = request.Start,
                End = request.End,
                PreferDistance = request.PreferDistance,
                PreciseTarget = request.PreciseTarget,
                MaxSteps = navigation.MaxSteps,
                BodyOffsets = request.BodyOffsets,
                FirstSteps = request.FirstSteps,
                CanEnd = request.CanEnd
            };
            return OfficialMapPathfinder.FindPath(Movement, boundedRequest);
        }
    }
}
