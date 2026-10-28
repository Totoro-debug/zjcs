// Selected official Common implementation; namespace / C# syntax adaptation only.
using System;

namespace Zjcs.Official.PlayerPlay
{

public struct PlayerPlayFightStateParam : IPlayerPlayParam, IComparable<IPlayerPlayParam>
{
	public readonly int Flag;

	public const int kAllowMultiPlayerFlag = 1;

	public const int kAllowInBackgroundFlag = 2;

	public const int kInBackgroundFlag = 4;

	public const int kAllowLeaveFlag = 8;

	public const int kFastFightFlag = 16;

	public const int kAllFlag = 31;

	public bool AllowMultiPlayer => (Flag & 1) == 1;

	public bool AllowInBackgroud => (Flag & 2) == 2;

	public bool InBackground => (Flag & 4) == 4;

	public PlayerPlayFightStateParam(int flag)
	{
		Flag = flag;
	}

	public int CompareTo(IPlayerPlayParam other)
	{
		if (other is PlayerPlayFightStateParam playerPlayFightStateParam)
		{
			return Flag.CompareTo(playerPlayFightStateParam.Flag);
		}
		return -1;
	}

	public override bool Equals(object obj)
	{
		if (obj is PlayerPlayFightStateParam playerPlayFightStateParam)
		{
			return Flag == playerPlayFightStateParam.Flag;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Flag;
	}

	public static bool operator ==(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return !(left == right);
	}

	public static bool operator <(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(PlayerPlayFightStateParam left, PlayerPlayFightStateParam right)
	{
		return left.CompareTo(right) >= 0;
	}
}
}
