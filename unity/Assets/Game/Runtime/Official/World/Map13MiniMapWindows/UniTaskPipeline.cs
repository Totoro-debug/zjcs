using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public class UniTaskPipeline : IDisposable
{
	private readonly Queue<Func<UniTask>> mTaskQueue = new Queue<Func<UniTask>>();

	private readonly object mLockObject = new object();

	private UniTask mCurrentTask = UniTask.CompletedTask;

	private CancellationTokenSource mCancellationTokenSource;

	private bool mIsProcessing;

	private bool mIsDisposed;

	public bool IsProcessing => mIsProcessing;

	public int PendingTaskCount
	{
		get
		{
			lock (mLockObject)
			{
				return mTaskQueue.Count;
			}
		}
	}

	public UniTaskPipeline()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		mCancellationTokenSource = new CancellationTokenSource();
	}

	public UniTask StartTask(Func<UniTask> taskFactory)
    {
        ThrowIfDisposed();
        var completionSource = new UniTaskCompletionSource();
        lock (mLockObject)
        {
            mTaskQueue.Enqueue(async () =>
            {
                try { await taskFactory(); completionSource.TrySetResult(); }
                catch (Exception ex) { completionSource.TrySetException(ex); }
            });
            if (!mIsProcessing) { mIsProcessing = true; ProcessQueueAsync().Forget(); }
        }
        return completionSource.Task;
    }

	public UniTask StartTask<T>(Func<T, UniTask> taskFactory, T parameter)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		return StartTask(() =>
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			return taskFactory(parameter);
		});
	}

	public UniTask<TResult> StartTask<TResult>(Func<UniTask<TResult>> taskFactory)
    {
        ThrowIfDisposed();
        var completionSource = new UniTaskCompletionSource<TResult>();
        lock (mLockObject)
        {
            mTaskQueue.Enqueue(async () =>
            {
                try { completionSource.TrySetResult(await taskFactory()); }
                catch (Exception ex) { completionSource.TrySetException(ex); }
            });
            if (!mIsProcessing) { mIsProcessing = true; ProcessQueueAsync().Forget(); }
        }
        return completionSource.Task;
    }

	public void ClearPendingTasks()
	{
		lock (mLockObject)
		{
			mTaskQueue.Clear();
		}
	}

	public async UniTask CancelAllAsync()
    {
        ThrowIfDisposed();
        mCancellationTokenSource?.Cancel();
        await mCurrentTask.SuppressCancellationThrow();
        lock (mLockObject) { mTaskQueue.Clear(); mIsProcessing = false; }
        mCancellationTokenSource?.Dispose();
        mCancellationTokenSource = new CancellationTokenSource();
    }

	public async UniTask WaitForCompletionAsync()
    {
        while (true)
        {
            UniTask currentTask;
            lock (mLockObject)
            {
                if (mTaskQueue.Count == 0 && !mIsProcessing) return;
                currentTask = mCurrentTask;
            }
            await currentTask;
            await UniTask.Yield();
        }
    }

	private async UniTaskVoid ProcessQueueAsync()
    {
        while (!mIsDisposed)
        {
            Func<UniTask> taskFactory;
            lock (mLockObject)
            {
                if (mTaskQueue.Count == 0) { mIsProcessing = false; return; }
                taskFactory = mTaskQueue.Dequeue();
            }
            try { mCurrentTask = taskFactory(); await mCurrentTask; }
            catch (Exception ex) { Debug.LogError($"UniTaskPipeline error: {ex}"); }
        }
    }

	private void ThrowIfDisposed()
	{
		if (mIsDisposed)
		{
			throw new ObjectDisposedException("UniTaskPipeline");
		}
	}

	public void Dispose()
	{
		if (mIsDisposed)
		{
			return;
		}
		mIsDisposed = true;
		mCancellationTokenSource?.Cancel();
		mCancellationTokenSource?.Dispose();
		lock (mLockObject)
		{
			mTaskQueue.Clear();
			mIsProcessing = false;
		}
	}
}

}
