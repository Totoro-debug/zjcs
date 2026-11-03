// Migrated selected official activity selector; see activity-selector-runtime-source-manifest.json.
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.ActivityBattleUI
{
using System;

public class ActivityFestivalBossNumContext
{
	public int SelectedIndex = -1;

	public int PreSelectedIndex = -1;

	public bool DataUpdateCompleted;

	public Action<int> OnCellClicked;

	public Action OnCellPreSelected;

	public Func<int, bool> IsUnlockedDelegate;

	public Func<int, bool> ShowRedDotDelegate;
}

}
