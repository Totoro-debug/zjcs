namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
public interface IFullScrMiniMapStateListener
{
	void OnShowFullScreenMiniMap(MiniMapCameraController cameraController);

	void OnHideFullScreenMiniMap(MiniMapCameraController cameraController);
}

}
