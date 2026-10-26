// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using System;
using System.Text;

public static class XXHash64
{
	private const ulong Prime64_1 = 11400714785074694791uL;

	private const ulong Prime64_2 = 14029467366897019727uL;

	private const ulong Prime64_3 = 1609587929392839161uL;

	private const ulong Prime64_4 = 9650029242287828579uL;

	private const ulong Prime64_5 = 2870177450012600261uL;

	public static ulong ComputeHash(string input, ulong seed = 0uL)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(input);
		return ComputeHash(bytes, bytes.Length, seed);
	}

	public static ulong ComputeHash(byte[] input, int len, ulong seed = 0uL)
	{
		int i = 0;
		ulong acc;
		if (len >= 32)
		{
			ulong num = (ulong)((long)seed + -7046029288634856825L + -4417276706812531889L);
			ulong num2 = seed + 14029467366897019727uL;
			ulong num3 = seed;
			ulong num4 = seed - 11400714785074694791uL;
			int num5 = len - 32;
			do
			{
				num = Round(num, BitConverter.ToUInt64(input, i));
				i += 8;
				num2 = Round(num2, BitConverter.ToUInt64(input, i));
				i += 8;
				num3 = Round(num3, BitConverter.ToUInt64(input, i));
				i += 8;
				num4 = Round(num4, BitConverter.ToUInt64(input, i));
				i += 8;
			}
			while (i <= num5);
			acc = Rotl64(num, 1) + Rotl64(num2, 7) + Rotl64(num3, 12) + Rotl64(num4, 18);
			acc = MergeRound(acc, num);
			acc = MergeRound(acc, num2);
			acc = MergeRound(acc, num3);
			acc = MergeRound(acc, num4);
		}
		else
		{
			acc = seed + 2870177450012600261L;
		}
		acc += (ulong)len;
		for (; i <= len - 8; i += 8)
		{
			ulong num6 = Round(0uL, BitConverter.ToUInt64(input, i));
			acc ^= num6;
			acc = (ulong)((long)Rotl64(acc, 27) * -7046029288634856825L + -8796714831421723037L);
		}
		if (i <= len - 4)
		{
			acc ^= (ulong)(BitConverter.ToUInt32(input, i) * -7046029288634856825L);
			acc = (ulong)((long)Rotl64(acc, 23) * -4417276706812531889L + 1609587929392839161L);
			i += 4;
		}
		for (; i < len; i++)
		{
			acc ^= (ulong)(input[i] * 2870177450012600261L);
			acc = Rotl64(acc, 11) * 11400714785074694791uL;
		}
		acc ^= acc >> 33;
		acc *= 14029467366897019727uL;
		acc ^= acc >> 29;
		acc *= 1609587929392839161L;
		return acc ^ (acc >> 32);
	}

	private static ulong Round(ulong acc, ulong input)
	{
		acc += (ulong)((long)input * -4417276706812531889L);
		acc = Rotl64(acc, 31);
		acc *= 11400714785074694791uL;
		return acc;
	}

	private static ulong MergeRound(ulong acc, ulong val)
	{
		val = Round(0uL, val);
		acc ^= val;
		acc = (ulong)((long)acc * -7046029288634856825L + -8796714831421723037L);
		return acc;
	}

	private static ulong Rotl64(ulong x, int r)
	{
		return (x << r) | (x >> 64 - r);
	}
}

}
