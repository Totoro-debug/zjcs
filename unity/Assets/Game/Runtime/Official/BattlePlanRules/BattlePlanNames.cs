using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Official.BattlePlanRules
{


public enum BattlePlanType
{
	None,
	Skill,
	Equip,
	Treasure
}

    public static class BattlePlanNameDiagnostics { public static Action<Exception> ReportObserverError; }
    public sealed class GAction<T1, T2, T3, T4>
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action<T1, T2, T3, T4> Observer;

		private GAction<T1, T2, T3, T4>? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction<T1, T2, T3, T4> parent, Action<T1, T2, T3, T4> observer, ulong version)
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
			GAction<T1, T2, T3, T4> gAction = Interlocked.Exchange(ref mParent, null);
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

	public static GAction<T1, T2, T3, T4> operator +(GAction<T1, T2, T3, T4> gAction, Action<T1, T2, T3, T4> action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction<T1, T2, T3, T4> operator -(GAction<T1, T2, T3, T4> gAction, Action<T1, T2, T3, T4> action)
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

	public static implicit operator GAction<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
	{
		GAction<T1, T2, T3, T4> gAction = new GAction<T1, T2, T3, T4>();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke(T1 param1, T2 param2, T3 param3, T4 param4)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer(param1, param2, param3, param4);
			}
			next = next.Next;
		}
	}

	public void SafeInvoke(T1 param1, T2 param2, T3 param3, T4 param4)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer(param1, param2, param3, param4);
				}
				catch (Exception exception)
				{
					BattlePlanNameDiagnostics.ReportObserverError?.Invoke(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action<T1, T2, T3, T4> handler)
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
    public sealed class PlayerChangeBattlePlanNameRequest
    {
        public BattlePlanType PlanType;
        public int PlanIndex;
        public string Name;
        public bool Custom;
    }
    public sealed class BattlePlanNameEntry { public string Name; }
    public interface IBattlePlanNamePlayer
    {
        bool TryGetPlanContent(BattlePlanType type,int index,out BattlePlanNameEntry entry);
        void FireOnBattlePlanNameChanged(BattlePlanType type,int index,string oldName,string newName);
    }
    public static class BattlePlanNames
    {
        public static bool IsValidPlanType(BattlePlanType planType)
	{
		if (planType != BattlePlanType.None && (uint)(planType - 1) <= 2u)
		{
			return true;
		}
		return false;
	}
public static string GetPlanName(this IBattlePlanNamePlayer player, BattlePlanType planType, int planIndex)
	{
		player.TryGetPlanContent(planType, planIndex, out var content);
		return content?.Name ?? string.Empty;
	}
public static bool SetPlanName(this IBattlePlanNamePlayer player, BattlePlanType planType, int planIndex, string name)
	{
		if (!IsValidPlanType(planType))
		{
			return false;
		}
		if (!player.TryGetPlanContent(planType, planIndex, out var content))
		{
			return false;
		}
		string name2 = content.Name;
		content.Name = name;
		player.FireOnBattlePlanNameChanged(planType, planIndex, name2, name);
		return true;
	}
        public static int CheckName(string name,bool custom,int characterMaxCount)
        {
            if(!custom) return string.IsNullOrEmpty(name) ? 0 : 62503;
            return Zjcs.Official.SkillRules.OfficialSkillPlanName.Check(name,characterMaxCount)==0 ? 0 : 62503;
        }
    }
    public static class BattlePlanLabels { public static string GetBattlePlanName(BattlePlanType planType, int planIndex, string name, Func<string,string> text)
	{
		if (!string.IsNullOrEmpty(name))
		{
			return name;
		}
		return GetDefaultBattlePlanName(planType, planIndex, text);
	}
private static string GetDefaultBattlePlanName(BattlePlanType planType, int planIndex, Func<string,string> text)
	{
		return planType switch
		{
			BattlePlanType.Equip => text($"equip_plan_default_name_{planIndex + 1}"),
			BattlePlanType.Treasure => text($"treasure_plan_default_name_{planIndex + 1}"),
			_ => text($"skill_plan_default_name_{planIndex + 1}"),
		};
	} }
}
