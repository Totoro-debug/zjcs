// Migrated original source; see map14-story-bubble-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.StoryBubbles
{
[RequireComponent(typeof(TMP_Text))]
public class TMP_TypeWriterEffect : MonoBehaviour
{
	[SerializeField]
	private bool mFixedDuration = true;

	[SerializeField]
	[Tooltip("固定时长")]
	private float mDuration = 1f;

	[Tooltip("显示每个字符所需要的时间")]
	[SerializeField]
	private float mWordInterval;

	private TMP_Text mTMPText;

	private Coroutine mTypeWriterCo;

	private Action<bool> mStopCallback;

	public TMP_Text TMPText
	{
		get
		{
			if ((Object)(object)mTMPText == (Object)null)
			{
				mTMPText = ((Component)this).GetComponent<TMP_Text>();
			}
			return mTMPText;
		}
	}

	public bool IsPlaying { get; private set; }

	protected void OnDisable()
	{
		StopType();
	}

	public void StartTypeWithFixedDuration(float duration, Action<bool> stopCallback)
	{
		mFixedDuration = true;
		mDuration = duration;
		StartType(stopCallback);
	}

	public void StartType(float wordIntervalS, Action<bool> stopCallback)
	{
		mFixedDuration = false;
		mWordInterval = wordIntervalS;
		StartType(stopCallback);
	}

	public void StartType(Action<bool> stopCallback = null)
	{
		if (!((Component)this).gameObject.activeInHierarchy)
		{
			Debug.LogError("播放打字机效果，节点未激活：" + gameObject.name, this);
		}
		StopType();
		mStopCallback = stopCallback;
		if (!string.IsNullOrEmpty(TMPText.text))
		{
			mTypeWriterCo = ((MonoBehaviour)this).StartCoroutine(DoTypeWriterEffect());
		}
	}

	public void StopType()
	{
		if (mTypeWriterCo != null)
		{
			IsPlaying = false;
			((MonoBehaviour)this).StopCoroutine(mTypeWriterCo);
			mTypeWriterCo = null;
			mStopCallback?.Invoke(obj: true);
			mStopCallback = null;
			TMPText.maxVisibleCharacters = int.MaxValue;
		}
	}

	public void FastEndType()
	{
		StopType();
	}

	private IEnumerator DoTypeWriterEffect()
	{
		IsPlaying = true;
		TMPText.ForceMeshUpdate(false, false);
		int characterCount = TMPText.textInfo.characterCount;
		float duration = (mFixedDuration ? mDuration : ((float)characterCount * mWordInterval));
		for (float time = 0f; time < duration; time += Time.deltaTime)
		{
			TMPText.maxVisibleCharacters = BubbleMath.Lerp(0, characterCount, time / duration);
			yield return null;
		}
		TMPText.maxVisibleCharacters = int.MaxValue;
		IsPlaying = false;
		mTypeWriterCo = null;
		mStopCallback?.Invoke(obj: false);
		mStopCallback = null;
	}
}

}
