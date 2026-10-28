// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Official.StoryPlayables
{

public sealed class GAction
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action Observer;

		private GAction? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction parent, Action observer, ulong version)
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
			GAction gAction = Interlocked.Exchange(ref mParent, null);
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

	public static GAction operator +(GAction gAction, Action action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction operator -(GAction gAction, Action action)
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

	public static implicit operator GAction(Action action)
	{
		GAction gAction = new GAction();
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
public sealed class GAction<T>
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action<T> Observer;

		private GAction<T>? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction<T> parent, Action<T> observer, ulong version)
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
			GAction<T> gAction = Interlocked.Exchange(ref mParent, null);
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

	public static GAction<T> operator +(GAction<T> gAction, Action<T> action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction<T> operator -(GAction<T> gAction, Action<T> action)
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

	public static implicit operator GAction<T>(Action<T> action)
	{
		GAction<T> gAction = new GAction<T>();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke(T param1)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer(param1);
			}
			next = next.Next;
		}
	}

	public void SafeInvoke(T param1)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer(param1);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action<T> handler)
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
public sealed class GAction<T1, T2>
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action<T1, T2> Observer;

		private GAction<T1, T2>? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction<T1, T2> parent, Action<T1, T2> observer, ulong version)
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
			GAction<T1, T2> gAction = Interlocked.Exchange(ref mParent, null);
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

	public static GAction<T1, T2> operator +(GAction<T1, T2> gAction, Action<T1, T2> action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction<T1, T2> operator -(GAction<T1, T2> gAction, Action<T1, T2> action)
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

	public static implicit operator GAction<T1, T2>(Action<T1, T2> action)
	{
		GAction<T1, T2> gAction = new GAction<T1, T2>();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke(T1 param1, T2 param2)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer(param1, param2);
			}
			next = next.Next;
		}
	}

	public void SafeInvoke(T1 param1, T2 param2)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer(param1, param2);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action<T1, T2> handler)
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
