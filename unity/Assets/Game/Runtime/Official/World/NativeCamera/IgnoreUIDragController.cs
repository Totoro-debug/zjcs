using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[DefaultExecutionOrder(1)]
public class IgnoreUIDragController : DragControllerBase
{
	public IInputOperationModule InputOperationModule { get; set; }
	protected override void OnEnable()
	{
		base.OnEnable();
		HookInput();
	}
	protected override void OnDisable()
	{
		UnhookInput();
		base.OnDisable();
	}
	protected void HookInput()
	{
		InputOperationModule.HookOnInput<OnIgnoreUIZoomBeginResult>(OnIgnoreUIZoomBegin);
		InputOperationModule.HookOnInput<OnIgnoreUIDragBeginResult>(OnIgnoreUIDragBegin);
		InputOperationModule.HookOnInput<OnGlobalDragResult>(OnGlobalDragDrag);
		InputOperationModule.HookOnInput<OnGlobalDragEndResult>(OnGlobalDragEnd);
	}
	protected void UnhookInput()
	{
		InputOperationModule.UnhookOnInput<OnIgnoreUIZoomBeginResult>(OnIgnoreUIZoomBegin);
		InputOperationModule.UnhookOnInput<OnIgnoreUIDragBeginResult>(OnIgnoreUIDragBegin);
		InputOperationModule.UnhookOnInput<OnGlobalDragResult>(OnGlobalDragDrag);
		InputOperationModule.UnhookOnInput<OnGlobalDragEndResult>(OnGlobalDragEnd);
	}
	private void OnIgnoreUIZoomBegin(OnIgnoreUIZoomBeginResult result)
	{
		OnZoomBegin();
	}
	private void OnIgnoreUIDragBegin(OnIgnoreUIDragBeginResult result)
	{
		OnDragBegin();
	}
	private void OnGlobalDragDrag(OnGlobalDragResult result)
	{
		if (result.Moved)
		{
			OnDrag();
		}
	}
	private void OnGlobalDragEnd(OnGlobalDragEndResult result)
	{
		OnDragEnd();
	}
}
}
