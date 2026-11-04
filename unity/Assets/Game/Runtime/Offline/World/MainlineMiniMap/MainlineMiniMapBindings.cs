using System;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13MiniMap;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Internal integration seam: the original systems keep their own state and
    // resource contracts. Map callers create one instance from these bindings.
    public sealed class MainlineMiniMapBindings
    {
        public MiniMapManagedContext State;
        public MiniMapSettings Settings;
        public int MinimapConfigId;
        public int MapViewRangeExtraSize;
        public IMiniMapCharacterManager Characters;
        public MiniMapNonMaskableManager NonMaskableManager;
        public GridVisibilityAgentComponent Visibility;
        public IMapRegionTime RegionTime;
        public MiniMapResourcePointContext ResourcePoints;
        public IMiniMapOfflineDataSource OfflineData;
        public IServiceProvider MarkerServices;
        public IMiniMapMarkPerformance MarkerPerformance;
        public IMiniMapClickInput ClickInput;
        public IMiniMapClickCameraControl CameraControl;
        public IMiniMapClickPublisher ClickPublisher;

        internal MainlineMiniMapBindings WithResourcePoints(MiniMapResourcePointContext resources)
        {
            var copy = (MainlineMiniMapBindings)MemberwiseClone();
            copy.ResourcePoints = resources;
            return copy;
        }
    }
}
