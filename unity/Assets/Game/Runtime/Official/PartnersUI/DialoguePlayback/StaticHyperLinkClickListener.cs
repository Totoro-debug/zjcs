// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StaticHyperLinkClickListener : MonoBehaviour, IPointerClickHandler, IEventSystemHandler, IWindowOperator
{
    private IPartnerStaticHyperLinkServices services;
    public void Configure(IPartnerStaticHyperLinkServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        if (mProvider is HyperLinkInfoProvider provider) provider.Configure(services);
    }

	private TextMeshProUGUI mText;

	private UITouchPass mUiTouchPass;

	private bool mInHyperLink;

	private IHyperLinkInfoProvider mProvider;


	public GameObject Operator => ((Component)this).gameObject;

	private void Awake()
	{
		mText = ((Component)this).GetComponent<TextMeshProUGUI>();
		mUiTouchPass = ((Component)this).GetComponent<UITouchPass>();
		if (mUiTouchPass != null)
		{
			mUiTouchPass.EventLimit = TouchPassEventLimit;
		}
		if (!((Component)this).TryGetComponent<IHyperLinkInfoProvider>(out mProvider))
		{
			mProvider = ((Component)this).gameObject.AddComponent<HyperLinkInfoProvider>();
		}
        if (services != null && mProvider is HyperLinkInfoProvider provider) provider.Configure(services);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		mInHyperLink = false;
		Vector3 val = new Vector3(eventData.position.x, eventData.position.y, 0f);
		int num = TMP_TextUtilities.FindIntersectingLink((TMP_Text)(object)mText, val, eventData.pressEventCamera);
		if (num < 0)
		{
			return;
		}
		TMP_LinkInfo val2 = ((TMP_Text)mText).textInfo.linkInfo[num];
		PartnerStaticHyperLinkInfo hyperLinkInfo = mProvider.GetHyperLinkInfo(val2.GetLinkID());
		if (hyperLinkInfo == null)
		{
			return;
		}
		mInHyperLink = true;
		IPartnerStaticHyperLinkHandler hyperLinkClickHandler = services.CreateHandler(hyperLinkInfo.HyperLinkType, hyperLinkInfo.HyperLinkData);
		if (hyperLinkClickHandler == null)
		{
			Debug.LogError("StaticHyperLinkClickListener: original Error branch.");
		}
		else
		{
			hyperLinkClickHandler.Init(this);
			hyperLinkClickHandler.OnClick();
		}
	}

	private bool TouchPassEventLimit(PointerEventData data, UITouchPass.EventType type)
	{
		if (type == UITouchPass.EventType.PointerClick)
		{
			return mInHyperLink;
		}
		return false;
	}
}

}
