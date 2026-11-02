// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public class HyperLinkInfoProvider : MonoBehaviour, IHyperLinkInfoProvider
{
    private IPartnerStaticHyperLinkServices services;
    public void Configure(IPartnerStaticHyperLinkServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	public virtual PartnerStaticHyperLinkInfo GetHyperLinkInfo(string linkId)
	{
		if (!int.TryParse(linkId, out var result))
		{
			return null;
		}
		PartnerStaticHyperLinkInfo hyperLinkInfo = services.GetHyperLinkInfo(result);
		if (hyperLinkInfo == null)
		{
			Debug.LogError("HyperLinkInfoProvider: original Error branch.");
			return null;
		}
		return hyperLinkInfo;
	}
}

}
