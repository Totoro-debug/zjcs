// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Zjcs.Official.StoryPlayables
{
	public class MyPlayableCenter : IMyPlayableCenter
	{
		private struct PlayableRefKey : IEquatable<PlayableRefKey>
		{
			public EcEntityIdentifier Owner;

			public int ClassId;

			public bool Equals(PlayableRefKey other)
			{
				if (other.Owner == Owner)
				{
					return other.ClassId == ClassId;
				}
				return false;
			}
		}

		private struct PlayableRefValue
		{
			public MyPlayable Playable;

			public int Count;
		}

		public object myNullObj = new object();

		private Dictionary<object, OrderedFastList<MyPlayable>> mPlayableDict = new Dictionary<object, OrderedFastList<MyPlayable>>();

		private bool mIsUpdatting;

		private bool mIsUpdateByApply;

		private List<(object comp, MyPlayable myPlayable)> mAddPlayableList = new List<(object, MyPlayable)>();

		private List<(object comp, MyPlayable myPlayable)> mRemovePlayableList = new List<(object, MyPlayable)>();

		private Dictionary<PlayableRefKey, PlayableRefValue> mPlayableRefDict = new Dictionary<PlayableRefKey, PlayableRefValue>();

		private HashSet<MyPlayable> mApplyDependence = new HashSet<MyPlayable>();

		private bool mIsRemovingAll;

		private bool isPause;

		public IEnumerable<OrderedFastList<MyPlayable>> AllPlayables => mPlayableDict.Values;

		bool IMyPlayableCenter.CanPlayPlayable(MyPlayable playable, out MyPlayable mutexPlayable)
		{
			mutexPlayable = null;
			if ((Object)(object)playable == (Object)null || mIsRemovingAll)
			{
				return false;
			}
			if (!(playable is IMyPlayableStateMutex myPlayableStateMutex) || !myPlayableStateMutex.IsOpenStateMutex())
			{
				return true;
			}
			GameObject stateMutexGo = myPlayableStateMutex.GetStateMutexGo();
			string stateMutexID = myPlayableStateMutex.GetStateMutexID();
			foreach (KeyValuePair<object, OrderedFastList<MyPlayable>> item in mPlayableDict)
			{
				foreach (MyPlayable item2 in item.Value)
				{
					if (item2.myStatus != MyPlayable.PlayableStatus.Stop && item2 is IMyPlayableStateMutex myPlayableStateMutex2 && myPlayableStateMutex2.IsOpenStateMutex() && !((Object)(object)stateMutexGo != (Object)(object)myPlayableStateMutex2.GetStateMutexGo()) && stateMutexID.Equals(myPlayableStateMutex2.GetStateMutexID()))
					{
						mutexPlayable = item2;
						if (myPlayableStateMutex.GetMutexAction() == ActionBehaviour.Immediate)
						{
							return true;
						}
						return false;
					}
				}
			}
			return true;
		}

		void IMyPlayableCenter.AddPlayable(MyPlayable playable, object comp)
		{
			if (!((Object)(object)playable == (Object)null) && !mIsRemovingAll)
			{
				object obj = comp ?? myNullObj;
				if (mIsUpdatting || mIsUpdateByApply)
				{
					mAddPlayableList.Add((obj, playable));
				}
				else
				{
					DoAddPlayable(obj, playable);
				}
			}
		}

		void IMyPlayableCenter.RemovePlayable(MyPlayable playable, object comp)
		{
			if (!((Object)(object)playable == (Object)null) && !mIsRemovingAll)
			{
				object obj = comp ?? myNullObj;
				if (mIsUpdatting || mIsUpdateByApply)
				{
					mRemovePlayableList.Add((obj, playable));
				}
				else
				{
					DoRemovePlayable(obj, playable);
				}
			}
		}

		public void Update(float deltaTime)
		{
			if (isPause)
			{
				return;
			}
			mIsUpdatting = true;
			try
			{
				EnsureLatestPlayables();
				foreach (KeyValuePair<object, OrderedFastList<MyPlayable>> item in mPlayableDict)
				{
					if (mIsRemovingAll)
					{
						break;
					}
					foreach (MyPlayable item2 in item.Value)
					{
						if (mIsRemovingAll)
						{
							break;
						}
						try
						{
							item2.ProcessFrame(deltaTime);
						}
						catch (Exception ex)
						{
							HandleDriveException(item2, ex, "ProcessFrame");
						}
					}
				}
			}
			finally
			{
				mIsUpdatting = false;
			}
			if (mIsRemovingAll)
			{
				ClearData();
			}
		}

		void IMyPlayableCenter.ForceFlushDependence(Component component)
		{
			mIsUpdateByApply = true;
			using (ProfilerScope.Sample("IMyPlayableCenter.ForceFlushDepend"))
			{
				try
				{
					if (mAddPlayableList != null && mAddPlayableList.Count > 0)
					{
						for (int i = 0; i < mAddPlayableList.Count; i++)
						{
							MyPlayable item = mAddPlayableList[i].myPlayable;
							if (mApplyDependence.Add(item))
							{
								FlushPlayable(item);
							}
						}
					}
					object key = (((Object)(object)component != (Object)null) ? component : myNullObj);
					mPlayableDict.TryGetValue(key, out var value);
					if (value == null || value.Count <= 0)
					{
						return;
					}
					foreach (MyPlayable item2 in value)
					{
						if (mApplyDependence.Add(item2))
						{
							FlushPlayable(item2);
						}
					}
				}
				finally
				{
					mIsUpdateByApply = false;
				}
			}
		}

		private void FlushPlayable(MyPlayable playable)
		{
			try
			{
				playable.ProcessFrame(0f);
				playable.PostProcessFrame();
			}
			catch (Exception ex)
			{
				HandleDriveException(playable, ex, "ForceFlushDependence");
			}
		}

		private void HandleDriveException(MyPlayable playable, Exception ex, string stage)
		{
			if (playable.ShouldReportDriveError())
			{
				UnityEngine.Debug.LogException(ex);
			}
			playable.ForceStopOnError();
		}

		public void RemoveAll()
		{
			mIsRemovingAll = true;
			if (!mIsUpdatting && !mIsUpdateByApply)
			{
				ClearData();
			}
		}

		private void ClearData()
		{
			mPlayableDict.Clear();
			mIsUpdatting = false;
			mIsUpdateByApply = false;
			mAddPlayableList.Clear();
			mRemovePlayableList.Clear();
			mIsRemovingAll = false;
		}

		public void LateUpdate()
		{
			if (isPause)
			{
				return;
			}
			mIsUpdatting = true;
			try
			{
				EnsureLatestPlayables();
				foreach (KeyValuePair<object, OrderedFastList<MyPlayable>> item in mPlayableDict)
				{
					if (mIsRemovingAll)
					{
						break;
					}
					foreach (MyPlayable item2 in item.Value)
					{
						if (mIsRemovingAll)
						{
							break;
						}
						try
						{
							item2.PostProcessFrame();
						}
						catch (Exception ex)
						{
							HandleDriveException(item2, ex, "PostProcessFrame");
						}
					}
				}
			}
			finally
			{
				mIsUpdatting = false;
			}
			if (mIsRemovingAll)
			{
				ClearData();
			}
			DoRemovePlayable();
		}

		private void EnsureLatestPlayables()
		{
			if (mAddPlayableList.Count != 0)
			{
				int i = 0;
				for (int count = mAddPlayableList.Count; i < count; i++)
				{
					var (comp, myPlayable) = mAddPlayableList[i];
					DoAddPlayable(comp, myPlayable);
				}
				mAddPlayableList.Clear();
			}
			DoRemovePlayable();
			mApplyDependence.Clear();
		}

		private void DoRemovePlayable()
		{
			if (mRemovePlayableList.Count != 0)
			{
				int i = 0;
				for (int count = mRemovePlayableList.Count; i < count; i++)
				{
					var (comp, myPlayable) = mRemovePlayableList[i];
					DoRemovePlayable(comp, myPlayable);
				}
				mRemovePlayableList.Clear();
			}
		}

		private void DoAddPlayable(object comp, MyPlayable myPlayable)
		{
			if (!mPlayableDict.TryGetValue(comp, out var value))
			{
				value = CollectionPool<OrderedFastList<MyPlayable>, MyPlayable>.Get();
				mPlayableDict.Add(comp, value);
			}
			value.Add(myPlayable);
		}

		private void DoRemovePlayable(object comp, MyPlayable myPlayable)
		{
			if (mPlayableDict.TryGetValue(comp, out var value))
			{
				value.Remove(myPlayable);
				if (value.Count == 0)
				{
					mPlayableDict.Remove(comp);
					CollectionPool<OrderedFastList<MyPlayable>, MyPlayable>.Release(value);
				}
			}
		}

		public void AddPlayableRef(EcEntityIdentifier owner, int classId, MyPlayable playable = null)
		{
			PlayableRefKey key = new PlayableRefKey
			{
				Owner = owner,
				ClassId = classId
			};
			if (!mPlayableRefDict.TryGetValue(key, out var value))
			{
				mPlayableRefDict.Add(key, new PlayableRefValue
				{
					Playable = playable,
					Count = 1
				});
			}
			else
			{
				value.Count++;
				mPlayableRefDict[key] = value;
			}
		}

		public int GetPlayableRef(EcEntityIdentifier owner, int classId)
		{
			PlayableRefKey key = new PlayableRefKey
			{
				Owner = owner,
				ClassId = classId
			};
			if (mPlayableRefDict.TryGetValue(key, out var value))
			{
				return value.Count;
			}
			return 0;
		}

		public void RemovePlayableRef(EcEntityIdentifier owner, int classId)
		{
			PlayableRefKey key = new PlayableRefKey
			{
				Owner = owner,
				ClassId = classId
			};
			if (!mPlayableRefDict.TryGetValue(key, out var value))
			{
				return;
			}
			value.Count--;
			if (value.Count <= 0)
			{
				if ((Object)(object)value.Playable != (Object)null)
				{
					value.Playable.Stop();
				}
				mPlayableRefDict.Remove(key);
			}
			else
			{
				mPlayableRefDict[key] = value;
			}
		}

		public void PauseAllPlayable()
		{
			isPause = true;
		}

		public void ResumeAllPlayable()
		{
			isPause = false;
		}

		private void ShowOrHideAllPlayable(bool isShow)
		{
			foreach (KeyValuePair<object, OrderedFastList<MyPlayable>> item in mPlayableDict)
			{
				foreach (MyPlayable item2 in item.Value)
				{
					if (!((Object)(object)item2 == (Object)null))
					{
						((Component)item2).gameObject.SetActive(isShow);
					}
				}
			}
		}

		public void HideAllPlayable()
		{
			ShowOrHideAllPlayable(isShow: false);
		}

		public void ShowAllPlayable()
		{
			ShowOrHideAllPlayable(isShow: true);
		}
	}
}
