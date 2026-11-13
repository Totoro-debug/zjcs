using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.BattleBgm
{
public class BgmManager : IBgmManager
{
	private enum DeferType
	{
		Play,
		Stop
	}

	private class BgmDeferData
	{
		public string Key { get; set; }

		public DeferType Type { get; set; }

		public BgmEventKeyConfig Config { get; set; }
	}

	private class BgmData
	{
		public string Key { get; set; }

		public int RefCount { get; set; }

		public BgmEventKeyConfig Config { get; set; }
	}

	private class BgmCollection
	{
		private List<string> mBgmPlayList = new List<string>();

		private Dictionary<string, BgmData> mBgmDataDict = new Dictionary<string, BgmData>();

		public BgmData GetLastBgmData()
		{
			string text = mBgmPlayList.LastOrDefault();
			if (text == null)
			{
				return null;
			}
			return mBgmDataDict[text];
		}

		public BgmData GetBgmData(string key)
		{
			mBgmDataDict.TryGetValue(key, out var value);
			return value;
		}

		public BgmData AddLast(string key, BgmData data)
		{
			mBgmPlayList.Add(key);
			if (!mBgmDataDict.TryGetValue(key, out var value))
			{
				value = data;
				mBgmDataDict[key] = data;
			}
			value.RefCount++;
			return value;
		}

		public void RemoveLast(string key)
		{
			if (mBgmDataDict.TryGetValue(key, out var value))
			{
				if (--value.RefCount <= 0)
				{
					mBgmDataDict.Remove(key);
				}
				int num = mBgmPlayList.LastIndexOf(key);
				if (num >= 0)
				{
					mBgmPlayList.RemoveAt(num);
				}
			}
		}
	}

	private bool mDeferStatus;

	private Queue<BgmDeferData> mDeferDataQueue = new Queue<BgmDeferData>();

	private BgmCollection mBgmCollection = new BgmCollection();

	private readonly IBgmGameObjectProvider mBgmGameObjectProvider;

	private readonly IBgmPlayback mPlayback;

	public BgmManager(IBgmGameObjectProvider bgmGameObjectProvider, IBgmPlayback playback)
	{
		mBgmGameObjectProvider = bgmGameObjectProvider;
		mPlayback = playback;
	}

	public PlayBgmHandle Play(string key, BgmEventKeyConfig config)
	{
		if (mDeferStatus)
		{
			mDeferDataQueue.Enqueue(new BgmDeferData
			{
				Key = key,
				Type = DeferType.Play,
				Config = config
			});
			return new PlayBgmHandle(key, this);
		}
		return InternalPlay(key, config);
	}

	private PlayBgmHandle InternalPlay(string key, BgmEventKeyConfig config)
	{
		BgmData lastBgmData = mBgmCollection.GetLastBgmData();
		if (lastBgmData != null)
		{
			mPlayback.PostEvent(lastBgmData.Config.PauseEventKey, mBgmGameObjectProvider.GameObject);
		}
		BgmData data = new BgmData
		{
			Key = key,
			Config = config
		};
		mBgmCollection.AddLast(key, data);
		data = mBgmCollection.GetLastBgmData();
		if (data.RefCount <= 1)
		{
			mPlayback.PostEvent(config.PlayEventKey, mBgmGameObjectProvider.GameObject);
		}
		else
		{
			mPlayback.PostEvent(config.ResumeEventKey, mBgmGameObjectProvider.GameObject);
		}
		return new PlayBgmHandle(key, this);
	}

	public void Stop(string key)
	{
		if (mDeferStatus)
		{
			mDeferDataQueue.Enqueue(new BgmDeferData
			{
				Key = key,
				Type = DeferType.Stop,
				Config = null
			});
		}
		else
		{
			InternalStop(key);
		}
	}

	public void InternalStop(string key)
	{
		BgmData bgmData = mBgmCollection.GetBgmData(key);
		if (bgmData != null)
		{
			BgmData lastBgmData = mBgmCollection.GetLastBgmData();
			if (bgmData.RefCount <= 1)
			{
				mPlayback.PostEvent(bgmData.Config.StopEventKey, mBgmGameObjectProvider.GameObject);
			}
			mBgmCollection.RemoveLast(key);
			BgmData lastBgmData2 = mBgmCollection.GetLastBgmData();
			if (lastBgmData2 != null && lastBgmData != lastBgmData2)
			{
				mPlayback.PostEvent(lastBgmData2.Config.ResumeEventKey, mBgmGameObjectProvider.GameObject);
			}
		}
	}

	public void SetSwitch(string rtpcName, string rtpcValue)
	{
		mPlayback.SetSwitch(rtpcName, rtpcValue, mBgmGameObjectProvider.GameObject);
	}

	public void EnterDeferStatus()
	{
		if (!mDeferStatus)
		{
			mDeferStatus = true;
		}
	}

	public void ExitDeferStatus()
	{
		if (!mDeferStatus)
		{
			return;
		}
		mDeferStatus = false;
		BgmDeferData bgmDeferData = default;
		while (mDeferDataQueue.TryDequeue(out bgmDeferData))
		{
			switch (bgmDeferData.Type)
			{
			case DeferType.Play:
				InternalPlay(bgmDeferData.Key, bgmDeferData.Config);
				break;
			case DeferType.Stop:
				InternalStop(bgmDeferData.Key);
				break;
			}
		}
		mDeferDataQueue.TrimExcess();
	}
}
}
