// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;


public interface ICultureDateFormatter
{
	LanguageType LanguageType { get; }

	string DateFormat(DateTime dateTime, EDateTimeDisplayType displayType);
}

}
