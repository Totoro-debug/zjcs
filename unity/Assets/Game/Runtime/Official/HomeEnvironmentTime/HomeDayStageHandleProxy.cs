using System;
using System.Collections.Generic;

namespace Zjcs.Official.HomeEnvironmentTime
{
public class HomeDayStageHandleProxy
{
	private readonly Stack<IHomeDayStageHandler> mStack = new Stack<IHomeDayStageHandler>(2);

	public HomeDayTime HomeDayTime
	{
		get
		{
			IHomeDayStageHandler homeDayStageHandler = default;
			if (mStack.TryPeek(out homeDayStageHandler))
			{
				return homeDayStageHandler.HomeDayTime;
			}
			return default;
		}
	}

	public bool InFasting
	{
		get
		{
			IHomeDayStageHandler homeDayStageHandler = default;
			if (mStack.TryPeek(out homeDayStageHandler))
			{
				return homeDayStageHandler is FastCycleDayStageHandler;
			}
			return false;
		}
	}

	public bool HasHandler => mStack.Count > 0;

	public float DeltaTime
	{
		get
		{
			IHomeDayStageHandler homeDayStageHandler = default;
			if (mStack.TryPeek(out homeDayStageHandler))
			{
				return homeDayStageHandler.DeltaTime;
			}
			return 0f;
		}
	}

	public void Update()
	{
		IHomeDayStageHandler homeDayStageHandler = default;
		if (mStack.TryPeek(out homeDayStageHandler) && homeDayStageHandler.TryUpdate())
		{
			mStack.Pop();
		}
	}

	public void AddHandler(IHomeDayStageHandler handler)
	{
		IHomeDayStageHandler homeDayStageHandler = default;
		if (mStack.TryPeek(out homeDayStageHandler) && !homeDayStageHandler.CanInterrupt)
		{
			mStack.Pop();
			mStack.Push(handler);
			mStack.Push(homeDayStageHandler);
		}
		else
		{
			mStack.Push(handler);
		}
	}

	public void RemoveHandler(IHomeDayStageHandler handler)
	{
		if (!mStack.Contains(handler))
		{
			return;
		}
		IHomeDayStageHandler[] array = mStack.ToArray();
		mStack.Clear();
		for (int num = array.Length - 1; num >= 0; num--)
		{
			if (array[num] != handler)
			{
				mStack.Push(array[num]);
			}
		}
	}

	public void Clear()
	{
		foreach (IHomeDayStageHandler item in mStack)
		{
			if (item is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
		mStack.Clear();
	}
}

}
