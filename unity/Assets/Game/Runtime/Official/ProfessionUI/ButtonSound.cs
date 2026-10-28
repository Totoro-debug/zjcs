using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour
{
	public OfficialWwiseEvent ClickEvent;

	private Button mBtn;

	private void Start()
	{
		mBtn = ((Component)this).GetComponent<Button>();
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)onBtnClick);
	}

	private void onBtnClick()
	{
		OfficialWwiseEvent clickEvent = ClickEvent;
		if (clickEvent != null)
		{
			clickEvent.Post(((Component)this).gameObject);
		}
	}
}
}
