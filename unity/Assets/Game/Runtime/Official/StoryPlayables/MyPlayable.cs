// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public class MyPlayable : MonoBehaviour, IGameObjectPoolItem
	{
		public enum PlayableStatus
		{
			Play,
			Stop,
			FadeOut,
			FadeIn,
			Delay
		}

		public GAction<MyPlayable> OnInit = new GAction<MyPlayable>();

		public GAction<MyPlayable> OnPlay = new GAction<MyPlayable>();

		public GAction<MyPlayable> OnPostPlay = new GAction<MyPlayable>();

		public GAction<MyPlayable> OnStop = new GAction<MyPlayable>();

		public GAction<MyPlayable> OnRemove = new GAction<MyPlayable>();

		public GAction<MyPlayable, float> OnFadeOut = new GAction<MyPlayable, float>();

		public GameObject Prefab;

		public GameObject mySource;

		public GameObject myTarget;

		public Component myApplyComponent;

		[Tooltip("播放结束时是否销毁本GameObject")]
		public bool myEndDestroy = true;

		[Tooltip("强制在结束时销毁")]
		public bool DestroyOnRemove = true;

		[Tooltip("播放速度")]
		public float mySpeed = 1f;

		[Tooltip("淡入时长")]
		public float myFadeInDuration;

		[Tooltip("淡出时长")]
		public float myFadeOutDuration;

		[Tooltip("触发器-指定时刻")]
		public List<MyPlayableTriggerTimeCfg> myTriggerList;

		[Tooltip("触发器-淡出时，如果未淡出直接销毁也会触发")]
		public List<MyPlayableTrigger> myFadeoutTrigerList;

		[Tooltip("触发器-结束时")]
		public List<MyPlayableTrigger> myEndTrigerList;

		protected float mFadeInTime;

		protected bool mShouldEnd;

		protected int mFrame;

		protected bool mShouldFadeout;

		protected float mFadeOutStartTime;

		protected float mFadeOutTime;

		private float mScaledDeltaTime;

		private int mInitialFrame;

		private int mTriggerIndex;

		private float mAdditionalTime;

		private bool mRemoving;

		private float? mAutoStopCountDown;

		private bool mErrorReported;

		private TrailRenderer[] mCachedTrailRenderers;

		private Action<MyPlayable> mDestroyCallback;

		public IMyPlayableCenter MyPlayableCenter { get; set; }

		public float Speed { get; private set; }

		public float Time { get; private set; }

		public PlayableStatus myStatus { get; private set; } = PlayableStatus.Stop;

		public bool myInited { get; private set; }

		public GameObject myParentGo { get; set; }

		public Dictionary<string, object> myParam { get; set; }

		public IMyPlayableNeedStop NeedStopObj { get; private set; }

		protected virtual void Awake()
		{
			CaptureChildSettingSnapshots();
		}

		private void CaptureChildSettingSnapshots()
		{
			mCachedTrailRenderers = ((Component)this).GetComponentsInChildren<TrailRenderer>(true);
		}

		private void RestoreChildSetting()
		{
			if (mCachedTrailRenderers == null)
			{
				return;
			}
			for (int i = 0; i < mCachedTrailRenderers.Length; i++)
			{
				TrailRenderer val = mCachedTrailRenderers[i];
				if ((Object)(object)val != (Object)null)
				{
					val.Clear();
				}
			}
		}

		public virtual void Init(GameObject source, GameObject target, float speed = 1f, GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
		{
			mySource = source;
			myTarget = target;
			Speed = mySpeed * speed;
			myParentGo = parentGo;
			myParam = param;
			NeedStopObj = needStopObj;
		}

		public void SetDestroyCallback(Action<MyPlayable> stopCallback)
		{
			mDestroyCallback = stopCallback;
		}

		public virtual void ResetData()
		{
			mySource = null;
			myTarget = null;
			Speed = 1f;
			myParentGo = null;
			myParam = null;
			NeedStopObj = null;
			mInitialFrame = 0;
			mAdditionalTime = 0f;
			mTriggerIndex = 0;
			mRemoving = false;
			mAutoStopCountDown = null;
			mDestroyCallback = null;
			mErrorReported = false;
			myInited = false;
			RestoreChildSetting();
		}

		public void SetAutoStopCountDown(float countDown)
		{
			if (myStatus != PlayableStatus.Stop)
			{
				mAutoStopCountDown = countDown;
			}
		}

		protected void Init()
		{
			if (!myInited)
			{
				myInited = true;
				mInitialFrame = UnityEngine.Time.frameCount;
				Time = 0f;
				myTriggerList.Sort((MyPlayableTriggerTimeCfg l, MyPlayableTriggerTimeCfg r) => (int)(10000f * (l.myTime - r.myTime)));
				OnInit?.SafeInvoke(this);
				InitImpl();
			}
		}

		protected virtual void InitImpl()
		{
		}

		public void OnGameObjectPoolReset()
		{
		}

		public void OnGameObjectPoolClear()
		{
		}

		protected virtual void OnDestroy()
		{
			if (myStatus != PlayableStatus.Stop && MyPlayableCenter != null)
			{
				MyPlayableCenter.RemovePlayable(this, myApplyComponent);
			}
		}

		public void TryPlay()
		{
			if (MyPlayableCenter.CanPlayPlayable(this, out var mutexPlayable))
			{
				try
				{
					if ((Object)(object)mutexPlayable != (Object)null)
					{
						mutexPlayable.Stop(immediate: true);
					}
					Play();
					return;
				}
				catch (Exception exception)
				{
					string value = (((Object)(object)((Component)this).transform.parent != (Object)null) ? ((Object)((Component)this).transform.parent).name : "null");
					UnityEngine.Debug.LogException(exception);
					ForceStopOnError();
					return;
				}
			}
			if ((Object)(object)mutexPlayable == (Object)null)
			{
				Stop();
				return;
			}
			float num = mutexPlayable.Time + GetDuration();
			float num2 = mutexPlayable.TotalDuration();
			float num3 = num - num2;
			if (num3 > 0f)
			{
				mutexPlayable.AddAdditionalTime(num3);
			}
			Stop();
		}

		private void Play()
		{
			if (myStatus != PlayableStatus.Stop)
			{
				string value = (((Object)(object)((Component)this).transform.parent != (Object)null) ? ((Object)((Component)this).transform.parent).name : "null");
				UnityEngine.Debug.LogWarning("Official playable diagnostic");
				return;
			}
			if (myFadeInDuration <= 0f)
			{
				myStatus = PlayableStatus.Play;
			}
			else
			{
				myStatus = PlayableStatus.FadeIn;
			}
			if ((Object)(object)myApplyComponent != (Object)null)
			{
				MyPlayableApplyObj.Bind(myApplyComponent.gameObject, this);
			}
			Init();
			OnPlay?.SafeInvoke(this);
			MyPlayableCenter.AddPlayable(this, myApplyComponent);
			PlayImpl();
			OnPostPlay?.SafeInvoke(this);
			if ((Object)(object)myApplyComponent != (Object)null)
			{
				MyPlayableCenter.ForceFlushDependence(myApplyComponent);
				return;
			}
			ProcessFrame(0f);
			PostProcessFrame();
		}

		protected virtual void PlayImpl()
		{
		}

		public void Remove()
		{
			mRemoving = true;
			if (myStatus == PlayableStatus.Stop && DestroyOnRemove)
			{
				mDestroyCallback?.Invoke(this);
			}
			else
			{
				RemoveImpl();
			}
			OnRemove?.SafeInvoke(this);
		}

		protected virtual void RemoveImpl()
		{
			DoStop();
		}

		public virtual void Stop(bool immediate = false)
		{
			if (myStatus != PlayableStatus.Stop)
			{
				if (immediate)
				{
					mShouldEnd = true;
				}
				else
				{
					mShouldFadeout = true;
				}
			}
		}

		public virtual void StopBeforeResetData()
		{
		}

		protected void DoStop()
		{
			if (myStatus == PlayableStatus.Stop)
			{
				return;
			}
			for (int i = 0; i < myEndTrigerList.Count; i++)
			{
				try
				{
					myEndTrigerList[i].Play(this);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			Time = 0f;
			mScaledDeltaTime = 0f;
			mShouldEnd = false;
			mFadeInTime = 0f;
			mShouldFadeout = false;
			mFadeOutTime = 0f;
			MyPlayableCenter.RemovePlayable(this, myApplyComponent);
			try
			{
				StopImpl();
			}
			catch (Exception exception2)
			{
				UnityEngine.Debug.LogException(exception2);
			}
			myStatus = PlayableStatus.Stop;
			OnStop?.SafeInvoke(this);
			if (myEndDestroy || (DestroyOnRemove && mRemoving))
			{
				mDestroyCallback?.Invoke(this);
			}
		}

		protected virtual void StopImpl()
		{
		}

		public void ForceStopOnError()
		{
			try
			{
				DoStop();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
				MyPlayableCenter.RemovePlayable(this, myApplyComponent);
			}
		}

		public bool ShouldReportDriveError()
		{
			if (mErrorReported)
			{
				return false;
			}
			mErrorReported = true;
			return true;
		}

		public string GetPrefabName()
		{
			string text = (((Object)(object)Prefab != (Object)null) ? ((Object)Prefab).name : "null");
			return ((object)this).GetType().Name + "(prefab:" + text + ")";
		}

		protected float GetFadeInTime()
		{
			if (myFadeInDuration == 0f)
			{
				return 1f;
			}
			float num = mFadeInTime / myFadeInDuration;
			return Mathf.Min(1f, num);
		}

		private void FadeOut()
		{
			if (myStatus == PlayableStatus.Play || myStatus == PlayableStatus.FadeIn)
			{
				DoTriggerOnFadeOut();
				if (myFadeOutDuration <= 0f)
				{
					mShouldEnd = true;
					return;
				}
				myStatus = PlayableStatus.FadeOut;
				mFadeOutStartTime = Time;
				mFadeOutTime = 0f;
				FadeOutImpl();
				OnFadeOut?.SafeInvoke(this, myFadeOutDuration);
			}
		}

		private void DoTriggerOnFadeOut()
		{
			for (int i = 0; i < myFadeoutTrigerList.Count; i++)
			{
				try
				{
					myFadeoutTrigerList[i].Play(this);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
		}

		protected virtual void FadeOutImpl()
		{
		}

		protected float GetFadeOutTime()
		{
			if (myFadeOutDuration == 0f)
			{
				return 1f;
			}
			float num = mFadeOutTime / myFadeOutDuration;
			return Mathf.Min(1f, num);
		}

		public float GetPreferWeight()
		{
			if (myStatus == PlayableStatus.FadeOut)
			{
				return GetFadeOutWeight(mFadeOutStartTime, mFadeOutTime);
			}
			return GetFadeInWeight(Time);
		}

		protected virtual float GetFadeInWeight(float time)
		{
			if (myFadeInDuration <= 0f || time >= myFadeInDuration)
			{
				return 1f;
			}
			return time / myFadeInDuration;
		}

		protected virtual float GetFadeOutWeight(float startTime, float time)
		{
			float fadeInWeight = GetFadeInWeight(startTime);
			return Mathf.Max(0f, fadeInWeight * (1f - time / myFadeOutDuration));
		}

		public void ProcessFrame(float deltaTime)
		{
			using (ProfilerScope.Sample("MyPlayable.ProcessFrame"))
			{
				bool flag = Time == 0f && mInitialFrame == UnityEngine.Time.frameCount;
				if (NeedStopObj != null && NeedStopObj.NeedStop())
				{
					Stop();
				}
				mFrame = UnityEngine.Time.frameCount;
				mScaledDeltaTime = (flag ? 0f : (deltaTime * Speed));
				if (myStatus == PlayableStatus.FadeIn)
				{
					mFadeInTime += mScaledDeltaTime;
				}
				if (myStatus == PlayableStatus.FadeOut)
				{
					DoFadeOutByFrame(mScaledDeltaTime);
					return;
				}
				float num = GetDuration() + mAdditionalTime;
				if (IsFixDuration() && Time + mScaledDeltaTime >= num)
				{
					mScaledDeltaTime = num - Time;
					Time = num;
					ProcessFrameImpl(mScaledDeltaTime);
					mShouldFadeout = true;
				}
				else
				{
					Time += mScaledDeltaTime;
					if (myStatus == PlayableStatus.FadeIn && Time >= myFadeInDuration)
					{
						myStatus = PlayableStatus.Play;
					}
					if (mAutoStopCountDown.HasValue && Time >= mAutoStopCountDown)
					{
						Stop();
					}
					mShouldFadeout = mShouldFadeout || ProcessFrameImpl(mScaledDeltaTime);
				}
				DoTrigger();
			}
		}

		private void DoTrigger()
		{
			for (int i = mTriggerIndex; i < myTriggerList.Count && !(myTriggerList[i].myTime > Time); i++)
			{
				try
				{
					mTriggerIndex++;
					myTriggerList[i].myTrigger.Play(this);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
		}

		private void DoFadeOutByFrame(float deltaTime)
		{
			Time += mScaledDeltaTime;
			ProcessFrameImpl(mScaledDeltaTime);
			mFadeOutTime += deltaTime;
			if (mFadeOutTime >= myFadeOutDuration)
			{
				mShouldEnd = true;
			}
		}

		protected virtual bool ProcessFrameImpl(float deltaTime)
		{
			return false;
		}

		public void PostProcessFrame()
		{
			if (mFrame != UnityEngine.Time.frameCount)
			{
				return;
			}
			mFrame = 0;
			if (myStatus == PlayableStatus.FadeOut)
			{
				PostProcessFrameImpl(mScaledDeltaTime);
			}
			else
			{
				if (IsFixDuration())
				{
					PostProcessFrameImpl(mScaledDeltaTime);
				}
				else
				{
					mShouldFadeout = mShouldFadeout || PostProcessFrameImpl(mScaledDeltaTime);
				}
				if (mShouldFadeout)
				{
					FadeOut();
				}
			}
			if (mShouldEnd)
			{
				DoStop();
			}
		}

		protected virtual bool PostProcessFrameImpl(float deltaTime)
		{
			return false;
		}

		public object TryGetParam(string key)
		{
			if (myParam == null)
			{
				return null;
			}
			myParam.TryGetValue(key, out var value);
			return value;
		}

		public virtual float GetDuration()
		{
			return 0f;
		}

		protected bool IsFixDuration()
		{
			float duration = GetDuration();
			if (duration >= 0f)
			{
				return !float.IsPositiveInfinity(duration);
			}
			return false;
		}

		private float TotalDuration()
		{
			float duration = GetDuration();
			if (duration < 0f)
			{
				return -1f;
			}
			return duration + mAdditionalTime;
		}

		private void AddAdditionalTime(float additionalTime)
		{
			mAdditionalTime += additionalTime;
		}
	}
}
