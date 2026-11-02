using System;

namespace Zjcs.Official.World.Map13MiniMap
{
    // The source must complete after Request returns, matching the original
    // request handle's OnComplete subscription. The owner supplies local data.
    public interface IMiniMapOfflineDataSource
    {
        MiniMapOfflineDataRequest Request(int mapClassId);
    }

    public sealed class MiniMapOfflineDataRequest
    {
        public event Action<PlayerOfflineMiniMapDataResponse, int> OnComplete;
        public void Complete(PlayerOfflineMiniMapDataResponse response, int errorCode)
            => OnComplete?.Invoke(response, errorCode);
    }
}
