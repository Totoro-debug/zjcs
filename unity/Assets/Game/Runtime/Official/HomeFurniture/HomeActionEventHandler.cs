// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public abstract class HomeActionEventHandler : MonoBehaviour
{
	public string EventName;

	private IDisposable mDisposable;
	public IHomeFurnitureActionEvents ActionEvents { get; set; }

	private void Awake()
	{
		mDisposable = ActionEvents.Subscribe(OnHandleEvent);
	}

	private void OnDestroy()
	{
		mDisposable?.Dispose();
	}

	private void OnHandleEvent(ActionEventData data)
	{
		if (string.Equals(EventName, data.EventName))
		{
			OnHandle(data);
		}
	}

	protected abstract void OnHandle(ActionEventData data);
}

}
