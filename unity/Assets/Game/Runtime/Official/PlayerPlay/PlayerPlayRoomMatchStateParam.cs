// Selected official Common implementation; namespace / C# syntax adaptation only.
using System;

namespace Zjcs.Official.PlayerPlay
{

public readonly struct PlayerPlayRoomMatchStateParam : IPlayerPlayParam, IComparable<IPlayerPlayParam>
{
	public readonly bool IsMatchDone;

	public PlayerPlayRoomMatchStateParam(bool isMatchDone)
	{
		IsMatchDone = isMatchDone;
	}

	public int CompareTo(IPlayerPlayParam other)
	{
		if (other is PlayerPlayRoomMatchStateParam playerPlayRoomMatchStateParam)
		{
			return IsMatchDone.CompareTo(playerPlayRoomMatchStateParam.IsMatchDone);
		}
		return -1;
	}

	public override bool Equals(object obj)
	{
		if (obj is PlayerPlayRoomMatchStateParam playerPlayRoomMatchStateParam)
		{
			return IsMatchDone == playerPlayRoomMatchStateParam.IsMatchDone;
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

	public static bool operator ==(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return !(left == right);
	}

	public static bool operator <(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(PlayerPlayRoomMatchStateParam left, PlayerPlayRoomMatchStateParam right)
	{
		return left.CompareTo(right) >= 0;
	}
}
}
