namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
public class WikiEntry : MonoBehaviour
{
	[SerializeField]
	private string WikiType;

	[SerializeField]
	private GameObject mButtonGo;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private bool mClickClearNewTip;

	private IProfessionWikiContext Context;

	public void Bind(IProfessionWikiContext context) => Context = context ?? throw new ArgumentNullException(nameof(context));






	protected void OnEnable()
	{

		mButton.onClick.AddListener(OnClick);
		bool flag = IsLimit();
		mButtonGo.SetActive(!flag);
	}

	protected void OnDisable()
	{
		mButton.onClick.RemoveListener(OnClick);
	}

	private void OnClick()
	{







		if (IsLimit())
		{
			string text = Context.Localize("Wiki_Disabled");
			Context.SystemMsg(text);
			return;
		}
		string uRL = Context.GetURL(WikiType);
		if (string.IsNullOrEmpty(uRL))
		{
			string text2 = Context.Localize("Wiki_Url_Empty");
			Context.SystemMsg(text2);
			return;
		}
		string titleKey = Context.GetTitleKey(WikiType);
		Forget(Goto(uRL, titleKey));
		ReportEvent(WikiType);
		if (mClickClearNewTip)
		{
			Context.PublishWikiClick();
		}
	}

	private bool IsLimit()
	{










		if (Context.ServerWikiDisabled)
		{
			return true;
		}
		if (Context.Player.IsWikiLimited)
		{
			return true;
		}
		ProfessionWikiInfo wikiInfo = Context.GetWikiInfo(WikiType);
		if (wikiInfo == null)
		{
			return true;
		}
		IProfessionWikiCondition condition = Context.GetCondition(wikiInfo.LimitConditionId);
		if (condition == null)
		{
			return true;
		}
		bool flag2 = condition.IsMet(Context.Player);
		return !flag2;
	}

	private async Task Goto(string url, string title)
	{
		// The source passes these three SDK parameters; title is unused there.
		string result = await Context.OpenHtml(url, "2", "2", "1");
		if (!string.IsNullOrEmpty(result)) Context.SystemMsg(result);
	}

	private static async void Forget(Task task)
	{
		try { await task; }
		catch (Exception exception) { Debug.LogException(exception); }
	}

	private void ReportEvent(string type)
	{
		string shuShuAccountId = Context.Player.GetShuShuAccountId();
		string account = Context.Player.Account;
		string text = "ClickWiki";
		Dictionary<string, object> dictionary = Context.CreateEventData(text, shuShuAccountId, account);
		dictionary.Add("WikiEntry", type);
		dictionary.Add("ServerId", Context.Player?.ServerId);
		dictionary.Add("PlayerId", Context.Player?.Id);
		dictionary.Add("PlayerName", Context.Player?.Name);
		dictionary.Add("PlayerLevel", Context.Player?.Level);
		Context.ReportEvent(text, dictionary);
	}
}

}
