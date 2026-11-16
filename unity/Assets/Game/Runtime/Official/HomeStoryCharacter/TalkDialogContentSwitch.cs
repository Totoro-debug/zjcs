// Adapted from the selected original client; see home-story-character source manifest.
namespace Zjcs.Official.HomeStoryCharacter
{
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryPortrait;
using Object = UnityEngine.Object;

public class TalkDialogContentSwitch : MonoBehaviour
{
	[SerializeField]
	private Animation mBattomBoardChangeAni;

	[SerializeField]
	private string mBattomBoardLeftToRight;

	[SerializeField]
	private string mBattomBoardRightToLeft;

	private Action mOnSwitchFinish;

	private void OnTalkDialogContentSwitchHalf()
	{
		mOnSwitchFinish?.Invoke();
	}

	public void SwithLeftToRight(Action onSwitchFinish)
	{
		mOnSwitchFinish = onSwitchFinish;
		mBattomBoardChangeAni.Play(mBattomBoardLeftToRight);
	}

	public void SwithRightToLeft(Action onSwitchFinish)
	{
		mOnSwitchFinish = onSwitchFinish;
		mBattomBoardChangeAni.Play(mBattomBoardRightToLeft);
	}
}

}
