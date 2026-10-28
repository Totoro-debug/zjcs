// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public abstract class MyPlayableOnInit : MonoBehaviour
	{
		protected MyPlayableApplyAttribute mAttr;

		private bool mIsOnPlayableInit;

		private Action<MyPlayable> mPlayableInitCache;

		private Action<MyPlayable> mPlayablePlayCache;

		private Action<MyPlayable> mPlayablePostPlayCache;

		private Action<MyPlayable> mPlayableStopCache;

		public MyPlayable myPlayable { get; private set; }

		protected virtual void Awake()
		{
			myPlayable = ((Component)this).GetComponent<MyPlayable>();
			mPlayableInitCache = OnPlayableInit;
			mPlayablePlayCache = OnPlayablePlay;
			mPlayablePostPlayCache = OnPlayablePostPlay;
			mPlayableStopCache = OnPlayableStop;
			myPlayable.OnStop += mPlayableStopCache;
			object[] customAttributes = ((object)myPlayable).GetType().GetCustomAttributes(typeof(MyPlayableApplyAttribute), inherit: false);
			if (customAttributes != null && customAttributes.Length != 0)
			{
				mAttr = customAttributes[0] as MyPlayableApplyAttribute;
			}
		}

		protected virtual void OnEnable()
		{
			mIsOnPlayableInit = false;
			if (myPlayable.myInited)
			{
				Init(myPlayable, mAttr);
			}
			else
			{
				mIsOnPlayableInit = true;
				myPlayable.OnInit += mPlayableInitCache;
			}
			myPlayable.OnPlay += mPlayablePlayCache;
			myPlayable.OnPostPlay += mPlayablePostPlayCache;
		}

		protected virtual void OnDisable()
		{
			if (mIsOnPlayableInit)
			{
				myPlayable.OnInit -= mPlayableInitCache;
				mIsOnPlayableInit = false;
			}
			myPlayable.OnPlay -= mPlayablePlayCache;
			myPlayable.OnPostPlay -= mPlayablePostPlayCache;
		}

		protected virtual void OnDestroy()
		{
			myPlayable.OnStop -= mPlayableStopCache;
			myPlayable = null;
			mPlayableInitCache = null;
			mPlayablePlayCache = null;
			mPlayablePostPlayCache = null;
			mPlayableStopCache = null;
			mAttr = null;
		}

		private void OnPlayablePlay(MyPlayable playable)
		{
			PlayImpl(playable, mAttr);
		}

		private void OnPlayablePostPlay(MyPlayable playable)
		{
			PostPlayImpl(playable, mAttr);
		}

		private void OnPlayableInit(MyPlayable playable)
		{
			Init(playable, mAttr);
		}

		private void OnPlayableStop(MyPlayable playable)
		{
			StopImpl(playable, mAttr);
		}

		private void Init(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			InitImpl(playable, applyAttr);
		}

		protected abstract void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr);

		protected abstract void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr);

		protected virtual void PostPlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
		}

		protected virtual void StopImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
		}
	}
}
