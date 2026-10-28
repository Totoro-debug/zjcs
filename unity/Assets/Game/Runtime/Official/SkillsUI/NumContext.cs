// Ported from client-source/NumContext.cs. Dependency injection, Wwise and animator calls use local bindings.
using System;

namespace Zjcs.Official.SkillsUI
{
public class NumContext
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
