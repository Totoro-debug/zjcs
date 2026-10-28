// Selected official Common implementation; namespace / C# syntax adaptation only.
using System;
using System.Runtime.InteropServices;

namespace Zjcs.Official.PlayerPlay
{

[StructLayout(LayoutKind.Sequential, Size = 1)]
public struct PlayerPlayActionNoneParam : IPlayerPlayParam, IComparable<IPlayerPlayParam>
{
	public int CompareTo(IPlayerPlayParam other)
	{
		return 0;
	}

	public override bool Equals(object obj)
	{
		return obj is PlayerPlayActionNoneParam;
	}

	public override int GetHashCode()
	{
		return 0;
	}

	public static bool operator ==(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return !(left == right);
	}

	public static bool operator <(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(PlayerPlayActionNoneParam left, PlayerPlayActionNoneParam right)
	{
		return left.CompareTo(right) >= 0;
	}
}
}
