// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;


internal sealed class NumberCultureFormatter_RU : CultureNumberFormatterBase
{
	public override LanguageType LanguageType => LanguageType.ru;

	public override IReadOnlyList<string> NumberSymbols { get; } = new string[7] { "", "K", "M", "B", "T", "P", "E" };

	public override IReadOnlyList<string> BattleNumberSymbols => NumberSymbols;

	public override int DivideBase => 1000;

	public override char DecimalSeparator => ',';

	public override string PercentSign => "%";

	protected override bool UseRomanNumerals => true;

	public override string FormatBattle(long value)
	{
		return NumberFormatHelper.FormatBattleByThreeWidth(value, BattleNumberSymbols);
	}
}

}
