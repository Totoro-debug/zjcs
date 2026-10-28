using System;
namespace Zjcs.Official.World.NativeCamera
{
public interface IDragControllerManager
{
	DragControllerBase CurrentController { get; }
	event Action<DragControllerBase> OnCurrentControllerChanged;
	void RegisterController(DragControllerBase controller);
	void UnregisterController(DragControllerBase controller);
}
}
