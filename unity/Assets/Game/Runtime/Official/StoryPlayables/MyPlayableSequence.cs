// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public class MyPlayableSequence : MyPlayable
	{
		[Serializable]
		public class MyPlayableTimeCfg
		{
			public float myTime;

			public MyPlayable myPlayable;
		}

		[Header("Playable序列的基本配置")]
		public List<MyPlayableTimeCfg> myPlayables;

		private float mDuration = -1f;

		private float mMaxDelay;

		private bool mStopped;

		private void OnValidate()
		{
			foreach (MyPlayableTimeCfg myPlayable in myPlayables)
			{
				if (myPlayable == null || (Object)(object)myPlayable.myPlayable == (Object)null)
				{
					UnityEngine.Debug.LogError("Official playable diagnostic");
				}
				else
				{
					if (!(myPlayable.myPlayable is MyPlayableSequence myPlayableSequence))
					{
						continue;
					}
					foreach (MyPlayableTimeCfg myPlayable2 in myPlayableSequence.myPlayables)
					{
						if (myPlayable2 != null && (Object)(object)myPlayable2.myPlayable != (Object)null && (Object)(object)((Component)myPlayable2.myPlayable).gameObject == (Object)(object)((Component)this).gameObject)
						{
							UnityEngine.Debug.LogError("Official playable diagnostic");
						}
					}
				}
			}
		}

		protected override void PlayImpl()
		{
			base.PlayImpl();
			InitDataOnPlay();
			foreach (MyPlayableTimeCfg myPlayable in myPlayables)
			{
				if (myPlayable.myTime == 0f)
				{
					SinglePlay(myPlayable);
				}
			}
		}

		public override void Stop(bool immediate = false)
		{
			base.Stop(immediate);
			if (mStopped)
			{
				return;
			}
			mStopped = true;
			foreach (MyPlayableTimeCfg myPlayable in myPlayables)
			{
				SingleStop(myPlayable, immediate);
			}
		}

		protected override void RemoveImpl()
		{
			foreach (MyPlayableTimeCfg myPlayable in myPlayables)
			{
				SingleRemvoe(myPlayable);
			}
			base.RemoveImpl();
		}

		protected override bool ProcessFrameImpl(float deltaTime)
		{
			base.ProcessFrameImpl(deltaTime);
			if (mStopped)
			{
				return true;
			}
			foreach (MyPlayableTimeCfg myPlayable in myPlayables)
			{
				if (myPlayable.myTime != 0f && FloatUtils.GreaterThan(myPlayable.myTime, Time - deltaTime) && FloatUtils.LessThanOrEqual(myPlayable.myTime, Time))
				{
					SinglePlay(myPlayable);
				}
			}
			return false;
		}

		protected override bool PostProcessFrameImpl(float deltaTime)
		{
			base.PostProcessFrameImpl(deltaTime);
			if (!IsFixDuration() && FloatUtils.GreaterThan(Time, mMaxDelay))
			{
				foreach (MyPlayableTimeCfg myPlayable in myPlayables)
				{
					if ((Object)(object)myPlayable.myPlayable != (Object)null && myPlayable.myPlayable.myStatus != PlayableStatus.Stop)
					{
						return false;
					}
				}
				mShouldEnd = true;
				return false;
			}
			return false;
		}

		protected override void StopImpl()
		{
			foreach (MyPlayableTimeCfg myPlayable2 in myPlayables)
			{
				MyPlayable myPlayable = myPlayable2.myPlayable;
				if ((Object)(object)myPlayable != (Object)null)
				{
					myPlayable.StopBeforeResetData();
				}
			}
			base.StopImpl();
		}

		public override float GetDuration()
		{
			return mDuration;
		}

		public override void ResetData()
		{
			foreach (MyPlayableTimeCfg myPlayable2 in myPlayables)
			{
				MyPlayable myPlayable = myPlayable2.myPlayable;
				if ((Object)(object)myPlayable != (Object)null)
				{
					myPlayable.ResetData();
				}
			}
			base.ResetData();
		}

		private void InitDataOnPlay()
		{
			mDuration = -1f;
			mMaxDelay = 0f;
			mStopped = false;
			foreach (MyPlayableTimeCfg myPlayable2 in myPlayables)
			{
				MyPlayable myPlayable = myPlayable2.myPlayable;
				if (!((Object)(object)myPlayable == (Object)null))
				{
					myPlayable.Init(mySource, myTarget, Speed, myParentGo, myParam);
					if (myPlayable2.myTime > mMaxDelay)
					{
						mMaxDelay = myPlayable2.myTime;
					}
				}
			}
		}

		private void SinglePlay(MyPlayableTimeCfg playable)
		{
			if (playable != null)
			{
				MyPlayable myPlayable = playable.myPlayable;
				if (!((Object)(object)myPlayable == (Object)null) && myPlayable.myStatus == PlayableStatus.Stop)
				{
					playable.myPlayable.TryPlay();
				}
			}
		}

		private void SingleStop(MyPlayableTimeCfg playable, bool immediate)
		{
			if (playable != null && !((Object)(object)playable.myPlayable == (Object)null))
			{
				playable.myPlayable.Stop(immediate);
			}
		}

		private void SingleRemvoe(MyPlayableTimeCfg playable)
		{
			if (playable != null && !((Object)(object)playable.myPlayable == (Object)null))
			{
				playable.myPlayable.Remove();
			}
		}
	}
}
