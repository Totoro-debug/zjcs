using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Official.WorldUI
{

public sealed class WorldGAction
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action Observer;

		private WorldGAction mParent;

		public ulong Version;

		public ObserverNode Previous { get; set; }

		public ObserverNode Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(WorldGAction parent, Action observer, ulong version)
		{
			mParent = parent;
			Observer = observer;
			Version = version;
			lock (mParent)
			{
				if (mParent.mRoot == null)
				{
					Volatile.Write(ref mParent.mRoot, this);
					return;
				}
				ObserverNode observerNode = mParent.mRoot.Previous ?? mParent.mRoot;
				observerNode.Next = this;
				Previous = observerNode;
				mParent.mRoot.Previous = this;
			}
		}

		public void Dispose()
		{
			WorldGAction gAction = Interlocked.Exchange(ref mParent, null);
			if (gAction == null)
			{
				return;
			}
			lock (gAction)
			{
				if (this == gAction.mRoot)
				{
					if (Previous == null || Next == null)
					{
						gAction.mRoot = null;
						return;
					}
					ObserverNode next = Next;
					if (next.Next == null)
					{
						next.Previous = null;
					}
					else
					{
						next.Previous = Previous;
					}
					gAction.mRoot = next;
				}
				else
				{
					Previous.Next = Next;
					if (Next != null)
					{
						Next.Previous = Previous;
					}
					else
					{
						gAction.mRoot.Previous = Previous;
					}
				}
			}
		}
	}

	private ObserverNode mRoot;

	private uint mVersion;

	public static WorldGAction operator +(WorldGAction gAction, Action action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static WorldGAction operator -(WorldGAction gAction, Action action)
	{
		for (ObserverNode observerNode = gAction.mRoot; observerNode != null; observerNode = observerNode?.Next)
		{
			if (observerNode?.Observer == action)
			{
				observerNode.Dispose();
				break;
			}
		}
		return gAction;
	}

	public static implicit operator WorldGAction(Action action)
	{
		WorldGAction gAction = new WorldGAction();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke()
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer();
			}
			next = next.Next;
		}
	}

	public void SafeInvoke()
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer();
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action handler)
	{
		return new ObserverNode(this, handler, mVersion);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetVersion()
	{
		if (mVersion == uint.MaxValue)
		{
			ResetAllObserverVersion();
			return 0u;
		}
		return mVersion++;
		void ResetAllObserverVersion()
		{
			lock (this)
			{
				for (ObserverNode next = mRoot; next != null; next = next.Next)
				{
					next.Version = 0uL;
				}
				mVersion = 1u;
			}
		}
	}
}

}
