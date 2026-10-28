// Migrated original source; see map14-story-bubble-runtime source receipt.
using Object = UnityEngine.Object;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.StoryBubbles
{
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour
{
	public Event ClickEvent;

	private Button mBtn;

	private void Start()
	{
		mBtn = ((Component)this).GetComponent<Button>();
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)onBtnClick);
	}

	private void onBtnClick()
	{
		Event clickEvent = ClickEvent;
		if (clickEvent != null)
		{
			clickEvent.Post(((Component)this).gameObject);
		}
	}
}

}
