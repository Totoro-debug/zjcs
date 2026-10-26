// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;


internal sealed class NumberCultureFormatter_DE : CultureNumberFormatterBase
{
	public override LanguageType LanguageType => LanguageType.de;

	public override IReadOnlyList<string> NumberSymbols { get; } = new string[7] { "", " T.", " Mio.", " Mrd.", " Bio.", " P", " E" };

	public override IReadOnlyList<string> BattleNumberSymbols { get; } = new string[7] { "", "K", "M", "B", "T", "P", "E" };

	public override int DivideBase => 1000;

	public override char DecimalSeparator => ',';

	public override string PercentSign => "\u00a0%";

	protected override bool UseRomanNumerals => true;

	public override string FormatBattle(long value)
	{
		return NumberFormatHelper.FormatBattleByThreeWidth(value, BattleNumberSymbols);
	}
}

}
