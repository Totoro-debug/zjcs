using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Official.ProfessionUI
{
public class UIClickSound : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	public OfficialWwiseEvent AkEvent;

	private void Start()
	{
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if ((int)eventData.button == 0)
		{
			AkEvent.Post(((Component)this).gameObject);
		}
	}
}
}
