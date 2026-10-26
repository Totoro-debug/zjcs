// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{

public sealed class CultureNumberArg : ArgBase
{
	public long Value { get; }

	public ECultureNumberArgFormatType FormatType { get; }

	public int DecimalPlaces { get; }

	public string Format { get; }

	private CultureNumberArg(long value)
		: this(value, ECultureNumberArgFormatType.Compact, 0, null)
	{
	}

	private CultureNumberArg(long value, ECultureNumberArgFormatType format, int decimalPlaces, string rawFormat)
	{
		Value = value;
		FormatType = format;
		DecimalPlaces = decimalPlaces;
		Format = rawFormat;
	}

	public static CultureNumberArg Compact(long value)
	{
		return new CultureNumberArg(value);
	}

	public static CultureNumberArg Battle(long value)
	{
		return new CultureNumberArg(value, ECultureNumberArgFormatType.Battle, 0, null);
	}

	public static CultureNumberArg Decimal(long value, int decimalPlaces = 1)
	{
		return new CultureNumberArg(value, ECultureNumberArgFormatType.DecimalPlaces, decimalPlaces, null);
	}

	public static CultureNumberArg RawFormat(long value, string format = "D2")
	{
		return new CultureNumberArg(value, ECultureNumberArgFormatType.RawFormat, 0, format);
	}

	public static CultureNumberArg Roman(long value)
	{
		return new CultureNumberArg(value, ECultureNumberArgFormatType.Roman, 0, null);
	}

	public override string ToString()
	{
		switch (FormatType)
		{
		case ECultureNumberArgFormatType.Compact:
			return Culture.Number.FormatCompact(Value);
		case ECultureNumberArgFormatType.Battle:
			return Culture.Number.FormatBattle(Value);
		case ECultureNumberArgFormatType.DecimalPlaces:
			return Culture.Number.FormatWithDecimalPlaces(Value, DecimalPlaces);
		case ECultureNumberArgFormatType.RawFormat:
			if (Format != null)
			{
				return Value.ToString(Format);
			}
			return Value.ToString();
		case ECultureNumberArgFormatType.Roman:
			return Culture.Number.FormatRoman(Value);
		default:
			return Value.ToString();
		}
	}
}

}
