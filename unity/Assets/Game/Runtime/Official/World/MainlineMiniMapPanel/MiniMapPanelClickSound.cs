using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniMapPanelClickSound : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	public AudioClip AkEvent;
	public System.Action<AudioClip,GameObject> Post;

	private void Start()
	{
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)eventData.button == 0)
		{
			Post?.Invoke(AkEvent, ((Component)this).gameObject);
		}
	}
}
}
