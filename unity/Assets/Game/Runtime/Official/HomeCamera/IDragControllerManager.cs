using System;
namespace Zjcs.Official.HomeCamera
{
public interface IDragControllerManager
{
	DragControllerBase CurrentController { get; }
	event Action<DragControllerBase> OnCurrentControllerChanged;
	void RegisterController(DragControllerBase controller);
	void UnregisterController(DragControllerBase controller);
}
}
