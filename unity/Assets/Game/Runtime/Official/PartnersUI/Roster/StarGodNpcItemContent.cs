// Ported selected official client; see partner-star-god-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;
using UnityEngine;

public class StarGodNpcItemContent : MonoBehaviour
{
	public event Action OnUnlockAnimTriggerEvent;

	public void UnlockAnimTriggerEventCall()
	{
		OnUnlockAnimTriggerEvent?.Invoke();
	}
}

}
