using System;

namespace Zjcs.Official.BattleBgm
{
public class PlayBgmHandle : IDisposable
{
	private readonly string mKey;

	private readonly IBgmManager mBgmManager;

	private bool mDisposed;

	public PlayBgmHandle(string key, IBgmManager bgmManager)
	{
		mKey = key;
		mBgmManager = bgmManager;
	}

	public void Dispose()
	{
		if (!mDisposed)
		{
			mDisposed = true;
			mBgmManager.Stop(mKey);
		}
	}
}
}
