using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;
public class WindowMaskEffectBase : MonoBehaviour
{
	public virtual void ApplyEffect(WindowMaskBase mask)
	{
	}

	public virtual void CancelEffect(WindowMaskBase mask)
	{
	}
}

}
