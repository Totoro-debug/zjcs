// Selected official Common implementation; namespace / C# syntax adaptation only.
using System;

namespace Zjcs.Official.PlayerPlay
{

public readonly struct PlayerPlayConfig<T> : IComparable<PlayerPlayConfig<T>> where T : IComparable
{
	public readonly T Type;

	public readonly IPlayerPlayParam Param;

	public PlayerPlayConfig(T type, IPlayerPlayParam param)
	{
		Type = type;
		Param = param;
	}

	public int CompareTo(PlayerPlayConfig<T> other)
	{
		int num = Type.CompareTo(other.Type);
		if (num != 0)
		{
			return num;
		}
		return Param.CompareTo(other.Param);
	}

	public override bool Equals(object obj)
	{
		if (obj is PlayerPlayConfig<T> playerPlayConfig)
		{
			if (Type.Equals(playerPlayConfig.Type))
			{
				return Param.Equals(playerPlayConfig.Param);
			}
			return false;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (Type.GetHashCode() << 28) | Param.GetHashCode();
	}

	public static bool operator ==(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return !(left == right);
	}

	public static bool operator <(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(PlayerPlayConfig<T> left, PlayerPlayConfig<T> right)
	{
		return left.CompareTo(right) >= 0;
	}
}
}
