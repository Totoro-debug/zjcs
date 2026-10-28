// Selected official Common implementation; namespace / C# syntax adaptation only.
using System;

namespace Zjcs.Official.PlayerPlay
{

public readonly struct PlayerPlayMatchStateParam : IPlayerPlayParam, IComparable<IPlayerPlayParam>
{
	public readonly bool IsMatchDone;

	public PlayerPlayMatchStateParam(bool isMatchDone)
	{
		IsMatchDone = isMatchDone;
	}

	public int CompareTo(IPlayerPlayParam other)
	{
		if (other is PlayerPlayMatchStateParam playerPlayMatchStateParam)
		{
			return IsMatchDone.CompareTo(playerPlayMatchStateParam.IsMatchDone);
		}
		return -1;
	}

	public override bool Equals(object obj)
	{
		if (obj is PlayerPlayMatchStateParam playerPlayMatchStateParam)
		{
			return IsMatchDone == playerPlayMatchStateParam.IsMatchDone;
		}
		return false;
	}

	public override int GetHashCode()
	{
		if (!IsMatchDone)
		{
			return 0;
		}
		return 1;
	}

	public static bool operator ==(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return !(left == right);
	}

	public static bool operator <(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(PlayerPlayMatchStateParam left, PlayerPlayMatchStateParam right)
	{
		return left.CompareTo(right) >= 0;
	}
}
}
