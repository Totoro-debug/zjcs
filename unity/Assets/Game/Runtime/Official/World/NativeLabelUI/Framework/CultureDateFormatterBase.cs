// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;


internal abstract class CultureDateFormatterBase : ICultureDateFormatter
{
	public abstract LanguageType LanguageType { get; }

	public virtual string DateFormat(DateTime dateTime, EDateTimeDisplayType displayType)
	{
		string text = LanguageManager.Instance.GetText(LanguageType, $"DateTimeFormat_{displayType}");
		return dateTime.ToString(text);
	}
}

}
