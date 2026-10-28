using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class DragControllerManager : IDragControllerManager
{
	private List<DragControllerBase> mControllerList = new List<DragControllerBase>();
	DragControllerBase IDragControllerManager.CurrentController => mControllerList.LastOrDefault();
	public event Action<DragControllerBase> OnCurrentControllerChanged;
	void IDragControllerManager.RegisterController(DragControllerBase controller)
	{
		DragControllerBase prev = mControllerList.LastOrDefault();
		mControllerList.Add(controller);
		NotifyIfCurrentChanged(prev);
	}
	void IDragControllerManager.UnregisterController(DragControllerBase controller)
	{
		DragControllerBase prev = mControllerList.LastOrDefault();
		mControllerList.Remove(controller);
		NotifyIfCurrentChanged(prev);
	}
	private void NotifyIfCurrentChanged(DragControllerBase prev)
	{
		DragControllerBase dragControllerBase = mControllerList.LastOrDefault();
		if ((Object)(object)dragControllerBase != (Object)(object)prev)
		{
			OnCurrentControllerChanged?.Invoke(dragControllerBase);
		}
	}
}
}
