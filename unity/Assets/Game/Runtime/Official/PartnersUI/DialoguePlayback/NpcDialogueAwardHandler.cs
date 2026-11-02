// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections;
using UnityEngine;

public class NpcDialogueAwardHandler : MonoBehaviour
{
    private IPartnerDialogueAwardServices services;
    public void Configure(IPartnerDialogueAwardServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	private float mTimeSecond = 1f;

	private void OnEnable()
	{
		services.RegisterHandler(HandleAward);
	}

	private void OnDisable()
	{
		services.RemoveHandler(HandleAward);
	}

	private bool HandleAward(object msg)
	{
		if (!services.IsConversationAward(msg))
		{
			return false;
		}
		((MonoBehaviour)this).StartCoroutine(DelayShowAward(msg));
		return true;
	}

	private IEnumerator DelayShowAward(object msg)
	{
		yield return (object)new WaitForSeconds(mTimeSecond);
		services.PopupAward(msg);
	}
}

}
