// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeNpcChatEntry : FurnitureBase
{
	private Coroutine mRefreshDailyCo;

	protected void OnEnable()
	{
		Services.Player.OnDebugTimeOffset += MainPlayer_OnDebugTimeOffset;
		mRefreshDailyCo = ((MonoBehaviour)this).StartCoroutine(TryRefreshDailyDialogues());
	}

	protected void OnDisable()
	{
		if (Services != null && Services.Player != null)
		{
			Services.Player.OnDebugTimeOffset -= MainPlayer_OnDebugTimeOffset;
		}
		if (mRefreshDailyCo != null)
		{
			((MonoBehaviour)this).StopCoroutine(mRefreshDailyCo);
			mRefreshDailyCo = null;
		}
	}

	protected override void OnClick()
	{
		if (!IsNpcDialogueLocked())
		{
			Services.PopupNpcPanel();
		}
	}

	private bool IsNpcDialogueLocked()
	{
		return Services.IsFunctionLimit(57);
	}

	private IEnumerator TryRefreshDailyDialogues()
	{
		while (true)
		{
			DateTime dateTime = Services.GetDateTime();
			double totalSeconds = (Services.GetDailyResetTimeFromDate(dateTime).AddDays(1.0) - dateTime).TotalSeconds;
			yield return (object)new WaitForSeconds((float)totalSeconds);
			Services.RefreshDailyDialogues(invokeByPlayer: false);
		}
	}

	private void MainPlayer_OnDebugTimeOffset(long obj)
	{
		if (!Services.IsFunctionLimit(57))
		{
			Services.RefreshDailyDialogues(invokeByPlayer: false);
		}
	}
}

}
