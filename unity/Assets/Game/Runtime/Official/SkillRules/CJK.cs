using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Common
{

public static class CJK
{
	public static int GetByteCount(string text)
	{
		int num = 0;
		if (string.IsNullOrEmpty(text))
		{
			return num;
		}
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
		while (textElementEnumerator.MoveNext())
		{
			string textElement = textElementEnumerator.GetTextElement();
			num += ((!IsDoubleByteCharacter(textElement)) ? 1 : 2);
		}
		return num;
	}

	public static string Truncate(string text, int maxByteCount)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		int num = 0;
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
		while (textElementEnumerator.MoveNext())
		{
			string textElement = textElementEnumerator.GetTextElement();
			num += ((!IsDoubleByteCharacter(textElement)) ? 1 : 2);
			if (num > maxByteCount)
			{
				break;
			}
			stringBuilder.Append(textElement);
		}
		if (stringBuilder.Length == text.Length)
		{
			return text;
		}
		return stringBuilder.ToString();
	}

	private static bool IsDoubleByteCharacter(string textElement)
	{
		if (string.IsNullOrEmpty(textElement))
		{
			return false;
		}
		int codePoint = ((textElement.Length == 1) ? textElement[0] : char.ConvertToUtf32(textElement, 0));
		if (!IsASCII(codePoint))
		{
			if (!IsCJK(codePoint))
			{
				return IsExtra(codePoint);
			}
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsASCII(int codePoint)
	{
		return codePoint < 128;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsCJK(int codePoint)
	{
		if (!IsBasicCJK(codePoint))
		{
			return IsExtendedCJK(codePoint);
		}
		return true;
	}

	private static bool IsBasicCJK(int codePoint)
	{
		if ((codePoint < 4352 || codePoint > 4607) && (codePoint < 11904 || codePoint > 12543) && (codePoint < 12592 || codePoint > 12687) && (codePoint < 12736 || codePoint > 12799) && (codePoint < 12800 || codePoint > 13311) && (codePoint < 13312 || codePoint > 19903) && (codePoint < 19968 || codePoint > 40959) && (codePoint < 43360 || codePoint > 43391) && (codePoint < 44032 || codePoint > 55295) && (codePoint < 63744 || codePoint > 64255) && (codePoint < 65040 || codePoint > 65055) && (codePoint < 65072 || codePoint > 65103))
		{
			if (codePoint >= 65280)
			{
				return codePoint <= 65519;
			}
			return false;
		}
		return true;
	}

	private static bool IsExtendedCJK(int codePoint)
	{
		if ((codePoint < 131072 || codePoint > 191471) && (codePoint < 191472 || codePoint > 192093) && (codePoint < 194560 || codePoint > 195103))
		{
			if (codePoint >= 196608)
			{
				return codePoint <= 205743;
			}
			return false;
		}
		return true;
	}

	private static bool IsExtra(int codePoint)
	{
		if (codePoint < 8192 || codePoint > 8303)
		{
			if (codePoint >= 9312)
			{
				return codePoint <= 9471;
			}
			return false;
		}
		return true;
	}
}

}
