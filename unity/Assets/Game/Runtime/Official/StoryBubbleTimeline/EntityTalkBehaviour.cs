// Migrated original source; see map14-story-bubble-timeline receipt.
using Object = UnityEngine.Object;
using MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;
using Zjcs.Official.StoryBubbles;
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryBubbleTimeline
{
[Serializable]
public class EntityTalkBehaviour : EntityBehaviour
{
	[Tooltip("bubble_dialog.csv里的Id")]
	public int DialogId;

	[Tooltip("bubble_dialog.csv里的Order")]
	public int Order;

	[Tooltip("文字显示时间间隔")]
	public float WordInterval = -1f;

	[Tooltip("是否立即显示对话内容")]
	public bool Instant;

	private GameObject mWindowGo;

	private float mDuration;

	public void SetDuration(float duration)
	{
		mDuration = duration;
		if (WordInterval > 0f)
		{
			BubbleDialogInfo bubbleDialogInfo = BubbleServices.Current.Get(DialogId, Order);
			if (bubbleDialogInfo != null)
			{
				string text = BubbleServices.Current.Localize("bubble_dialog_" + bubbleDialogInfo.Id + "_" + bubbleDialogInfo.Order);
				mDuration = (float)text.Length * WordInterval;
			}
		}
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		if (!((Object)(object)mBindEntity == (Object)null) && !((Object)(object)mWindowGo != (Object)null))
		{
			((IBubblePopupServices)BubbleServices.Current).Popup((BubbleDialog go) =>
			{
				mWindowGo = ((Component)go).gameObject;
			}).OnComplete += (Action<GameObject>)((GameObject go) =>
			{
				go.GetComponent<BubbleDialog>().Play(DialogId, Order, mBindEntity, mDuration, Instant);
			});
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		if ((Object)(object)mWindowGo != (Object)null)
		{
			((IBubblePopupServices)BubbleServices.Current).Close(mWindowGo);
			mWindowGo = null;
		}
	}
}

}
