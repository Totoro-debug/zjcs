// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UITouchPass : MonoBehaviour, IPointerClickHandler, IEventSystemHandler, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler
{
    private IPartnerStaticHyperLinkServices services;
    public void Configure(IPartnerStaticHyperLinkServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	public enum EventType
	{
		None,
		PointerDown,
		PointerUp,
		PointerClick,
		PointerDrag
	}

	public Func<PointerEventData, EventType, bool> EventLimit;


	public bool IsDrag { get; protected set; }

	public void OnPointerDown(PointerEventData eventData)
	{
		IsDrag = true;
		this.PassEvent<IPointerDownHandler>(eventData, ExecuteEvents.pointerDownHandler);
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		IsDrag = false;
		this.PassEvent<IPointerUpHandler>(eventData, ExecuteEvents.pointerUpHandler);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		this.PassEvent<IPointerClickHandler>(eventData, ExecuteEvents.pointerClickHandler);
	}

	public void OnPointerMove(PointerEventData eventData)
	{
		if (IsDrag)
		{
			this.PassEvent<IPointerMoveHandler>(eventData, ExecuteEvents.pointerMoveHandler);
		}
	}

	public void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T : IEventSystemHandler
	{
		if (EventLimit != null && EventLimit(data, GetType<T>(function)))
		{
			return;
		}
		RaycastResult pointerCurrentRaycast = data.pointerCurrentRaycast;
		GameObject gameObject = pointerCurrentRaycast.gameObject;
		foreach (RaycastResult item in services.ForeachRaycastResults(data.position))
		{
			RaycastResult current = item;
			if ((UnityEngine.Object)(object)gameObject != (Object)(object)current.gameObject && (Object)(object)current.gameObject.GetComponent<UITouchPass>() == (UnityEngine.Object)null)
			{
				ExecuteEvents.Execute<T>(current.gameObject, (BaseEventData)(object)data, function);
				break;
			}
		}
	}

	private EventType GetType<T>(ExecuteEvents.EventFunction<T> function) where T : IEventSystemHandler
	{
		if (function is ExecuteEvents.EventFunction<IPointerClickHandler>)
		{
			return EventType.PointerClick;
		}
		if (function is ExecuteEvents.EventFunction<IPointerDownHandler>)
		{
			return EventType.PointerDown;
		}
		if (function is ExecuteEvents.EventFunction<IPointerUpHandler>)
		{
			return EventType.PointerUp;
		}
		if (function is ExecuteEvents.EventFunction<IPointerMoveHandler>)
		{
			return EventType.PointerDrag;
		}
		return EventType.None;
	}
}

}
