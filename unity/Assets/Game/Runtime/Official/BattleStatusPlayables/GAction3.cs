// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
namespace Zjcs.Official.BattleStatusPlayables
{
public sealed class GAction<T1, T2, T3>
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action<T1, T2, T3> Observer;

		private GAction<T1, T2, T3>? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction<T1, T2, T3> parent, Action<T1, T2, T3> observer, ulong version)
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
			GAction<T1, T2, T3> gAction = Interlocked.Exchange(ref mParent, null);
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

	private ObserverNode? mRoot;

	private uint mVersion;

	public static GAction<T1, T2, T3> operator +(GAction<T1, T2, T3> gAction, Action<T1, T2, T3> action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction<T1, T2, T3> operator -(GAction<T1, T2, T3> gAction, Action<T1, T2, T3> action)
	{
		for (ObserverNode observerNode = gAction.mRoot; observerNode != null; observerNode = observerNode?.Next)
		{
			if ((Delegate)(observerNode?.Observer) == (Delegate)action)
			{
				observerNode.Dispose();
				break;
			}
		}
		return gAction;
	}

	public static implicit operator GAction<T1, T2, T3>(Action<T1, T2, T3> action)
	{
		GAction<T1, T2, T3> gAction = new GAction<T1, T2, T3>();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke(T1 param1, T2 param2, T3 param3)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer(param1, param2, param3);
			}
			next = next.Next;
		}
	}

	public void SafeInvoke(T1 param1, T2 param2, T3 param3)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer(param1, param2, param3);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action<T1, T2, T3> handler)
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
