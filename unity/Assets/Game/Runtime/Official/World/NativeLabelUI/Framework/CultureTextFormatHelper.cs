// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Text;


public static class CultureTextFormatHelper
{
	public static CultureTextArg[] GetKeyArgs(params string[] args)
	{
		if (args == null || args.Length == 0)
		{
			return null;
		}
		CultureTextArg[] array = new CultureTextArg[args.Length];
		for (int i = 0; i < args.Length; i++)
		{
			array[i] = new CultureTextArg(args[i]);
		}
		return array;
	}

	public static string GetKeyBySplit(int count, string splitKey)
	{
		if (count <= 0)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(splitKey);
			}
			stringBuilder.Append($"{{{i}}}");
		}
		return stringBuilder.ToString();
	}
}

}
