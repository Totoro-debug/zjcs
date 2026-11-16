// Adapted from the selected original client; see home-story-character source manifest.
namespace Zjcs.Official.HomeStoryCharacter
{
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryPortrait;
using Object = UnityEngine.Object;

public sealed class TalkDialogCharacterController : MonoBehaviour
{
	[SerializeField]
	private TalkDialogContentSwitch mContentSwitch;

	[SerializeField]
	private TalkDialogCharacter mLeftCharacter;

	[SerializeField]
	private TalkDialogCharacter mRightCharacter;

	private TalkDialogCharacter mCurrentTalk;

	public IStoryPortraitServices Services { get; set; }

	private bool IsVoiceOver(int roleId)
	{
		return Services.IsVoiceOver(roleId);
	}

	public void SetPortrait(int roleId, string portraitPath, string face, Action onSwitchPortraitFinish)
	{
		mLeftCharacter.Services = Services;
		mRightCharacter.Services = Services;
		if (IsVoiceOver(roleId))
		{
			TurnToVoiceOverTalk(onSwitchPortraitFinish);
		}
		else if ((Object)null == (Object)(object)mCurrentTalk)
		{
			TurnToFirstPortraitTalk(portraitPath, face, onSwitchPortraitFinish);
		}
		else
		{
			TurnSwitchPortraitTalk(portraitPath, face, onSwitchPortraitFinish);
		}
	}

	private void TurnToFirstPortraitTalk(string portraitPath, string face, Action onSwitchFinish)
	{
		mCurrentTalk = mLeftCharacter;
		mCurrentTalk.ChangeStatus(ETalkPortraitStatusType.Enter, portraitPath, face);
		onSwitchFinish?.Invoke();
	}

	private void TurnSwitchPortraitTalk(string portraitPath, string face, Action onSwitchFinish)
	{
		if (!mCurrentTalk.IsSamePortrait(portraitPath))
		{
			mCurrentTalk.ChangeStatus(ETalkPortraitStatusType.Shutup);
			bool flag = (Object)(object)mCurrentTalk == (Object)(object)mLeftCharacter;
			BottomBoardContentSwitch(flag, onSwitchFinish);
			mCurrentTalk = (flag ? mRightCharacter : mLeftCharacter);
			((Component)mCurrentTalk).transform.SetAsLastSibling();
			mCurrentTalk.ChangeStatus(ETalkPortraitStatusType.Talk, portraitPath, face);
		}
		else
		{
			mCurrentTalk.ChangeFace(face);
			onSwitchFinish?.Invoke();
		}
	}

	private void TurnToVoiceOverTalk(Action onSwitchFinish)
	{
		mLeftCharacter.ChangeStatus(ETalkPortraitStatusType.Leave);
		mRightCharacter.ChangeStatus(ETalkPortraitStatusType.Leave);
		if ((Object)null == (Object)(object)mCurrentTalk)
		{
			onSwitchFinish?.Invoke();
			return;
		}
		if ((Object)(object)mCurrentTalk == (Object)(object)mRightCharacter)
		{
			BottomBoardContentSwitch(isLeftToRight: false, onSwitchFinish);
		}
		else
		{
			onSwitchFinish?.Invoke();
		}
		mCurrentTalk = null;
	}

	private void BottomBoardContentSwitch(bool isLeftToRight, Action onSwitchFinish)
	{
		if (isLeftToRight)
		{
			mContentSwitch.SwithLeftToRight(onSwitchFinish);
		}
		else
		{
			mContentSwitch.SwithRightToLeft(onSwitchFinish);
		}
	}
}

}
