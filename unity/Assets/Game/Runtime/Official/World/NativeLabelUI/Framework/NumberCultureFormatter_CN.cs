// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;


internal sealed class NumberCultureFormatter_CN : CultureNumberFormatterBase
{
	public override LanguageType LanguageType => LanguageType.zh_CN;

	public override IReadOnlyList<string> NumberSymbols { get; } = new string[5] { "", "万", "亿", "兆", "京" };

	public override IReadOnlyList<string> BattleNumberSymbols => NumberSymbols;

	public override int DivideBase => 10000;

	public override char DecimalSeparator => '.';

	public override string PercentSign => "%";

	public override string FormatBattle(long value)
	{
		return NumberFormatHelper.FormatBattleByFourWidth(value, BattleNumberSymbols);
	}
}

}
