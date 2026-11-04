using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;
public interface IBlurManager
{
	RenderTexture GetRenderTexture();

	void ReleaseRenderTexture(RenderTexture rt);

	void TakeAShot(RenderTexture rt);
}

}
