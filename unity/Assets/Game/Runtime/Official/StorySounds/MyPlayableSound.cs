// Migrated official source; see map14-story-sound-runtime source receipt.
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.StorySounds
{
public class MyPlayableSound : MyPlayable
{
	[Header("声音的基本配置")]
	public Event SoundEvent;

	[Tooltip("循环播放时间间隔，小于等于0无效")]
	public float LoopInterval = -1f;

	[Tooltip("声音时长")]
	public float Duration;

	[Tooltip("停止时发送的声音事件")]
	public Event StopSoundEvent;

	private float mNextPlayTime;

	protected override void InitImpl()
	{
		mNextPlayTime = 0f;
	}

	protected override void PlayImpl()
	{
		mNextPlayTime = Time + LoopInterval;
		Event soundEvent = SoundEvent;
		if (soundEvent != null)
		{
			soundEvent.Post(GetEventGO());
		}
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		if (LoopInterval <= 0f)
		{
			return false;
		}
		if (mNextPlayTime <= Time - deltaTime)
		{
			mNextPlayTime = Time + LoopInterval;
			Event soundEvent = SoundEvent;
			if (soundEvent != null)
			{
				soundEvent.Post(GetEventGO());
			}
		}
		return false;
	}

	public override void Stop(bool immediate = false)
	{
		base.Stop(immediate);
		DoStop();
	}

	protected override void StopImpl()
	{
		Event stopSoundEvent = StopSoundEvent;
		if (stopSoundEvent != null)
		{
			stopSoundEvent.Post(GetEventGO());
		}
		mNextPlayTime = 0f;
		base.StopImpl();
	}

	public override float GetDuration()
	{
		return Duration;
	}

	private GameObject GetEventGO()
	{
		if ((Object)(object)myTarget != (Object)null)
		{
			return myTarget;
		}
		return ((Component)this).gameObject;
	}
}

}
