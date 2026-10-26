// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;


public sealed class CultureDateTimeArg : ArgBase
{
	public DateTime Value { get; }

	public EDateTimeDisplayType DisplayType { get; }

	public CultureDateTimeArg(DateTime value, EDateTimeDisplayType displayType = EDateTimeDisplayType.yyyyMMddHHmmss)
	{
		Value = value;
		DisplayType = displayType;
	}

	public override string ToString()
	{
		return Culture.Date.DateFormat(Value, DisplayType);
	}
}

}
