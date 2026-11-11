// Original menu peripheral component; local service and observable bindings.
using System.Linq;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomePlayerPanelUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class WikiEntry : MonoBehaviour
{
	public IHomeMenuWikiServices Services { private get; set; }

	[SerializeField]
	private string WikiType;

	[SerializeField]
	private GameObject mButtonGo;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private bool mClickClearNewTip;

	private HomePanelDisposableBag mDisposableBag;






	protected void OnEnable()
	{
		mDisposableBag = default;
		HomeMenuSubscriptions.SubscribeClick(mButton,OnClick).AddTo(ref mDisposableBag);
		bool flag = IsLimit();
		mButtonGo.SetActive(!flag);
	}

	protected void OnDisable()
	{
		mDisposableBag.Dispose();
	}

	private void OnClick()
	{
		if (IsLimit())
		{
			string text = Services.GetText("Wiki_Disabled");
			Services.SystemMessage(text);
			return;
		}
		string uRL = Services.GetUrl(WikiType);
		if (string.IsNullOrEmpty(uRL))
		{
			string text2 = Services.GetText("Wiki_Url_Empty");
			Services.SystemMessage(text2);
			return;
		}
		string titleKey = Services.GetTitleKey(WikiType);
		Goto(uRL, titleKey).Forget();
		ReportEvent(WikiType);
		if (mClickClearNewTip)
		{
			Services.PublishNewTipClick();
		}
	}

	private bool IsLimit()
	{
		if (Services.CheckWikiDisabled())
		{
			return true;
		}
		if (Services.Player.IsFunctionLimit(FunctionType.Wiki))
		{
			return true;
		}
		HomeMenuWikiInfo wikiInfo = Services.GetWikiInfo(WikiType);
		if (wikiInfo == null)
		{
			return true;
		}
		IHomeHudCondition condition = Services.GetCondition(wikiInfo.LimitConditionId);
		if (condition == null)
		{
			return true;
		}
		bool flag2 = Services.IsMet(condition);
		return !flag2;
	}

	private async UniTask Goto(string url, string title)
	{
		string text = await Services.OpenHtml(url, "2", "2", "1", null);
		if (!string.IsNullOrEmpty(text))
		{
			Services.TrySystemMessage(text);
		}
	}

	private void ReportEvent(string type)
	{
		string shuShuAccountId = Services.Player.GetShuShuAccountId();
		string account = Services.Player.Account;
		string text = "ClickWiki";
		Dictionary<string, object> dictionary = Services.CreateEventData(text, shuShuAccountId, account);
		dictionary.Add("WikiEntry", type);
		dictionary.Add("ServerId", Services.Player?.ServerId);
		dictionary.Add("PlayerId", Services.Player?.Id);
		dictionary.Add("PlayerName", Services.Player?.Name);
		dictionary.Add("PlayerLevel", Services.Player?.Level);
		Services.ReportEvent(text, dictionary);
	}
}
}
