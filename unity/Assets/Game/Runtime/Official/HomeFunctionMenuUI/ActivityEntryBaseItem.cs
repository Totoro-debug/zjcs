// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
public abstract class ActivityEntryBaseItem : UIBase
{
	protected IHomeMenuActivityEntryData mActivityEntryData;

	protected bool mIsShowTipDot = true;

	public IHomeMenuActivityEntryData ActivityEntryData => mActivityEntryData;

	public void Init(IHomeMenuActivityEntryData activityEntryData)
	{
		InitWrapper(() =>
		{
			InitImp(activityEntryData);
		});
	}

	public void Init(IHomeMenuActivityEntryData activityEntryData, bool showTipDot)
	{
		mIsShowTipDot = showTipDot;
		InitWrapper(() =>
		{
			InitImp(activityEntryData);
		});
	}

	protected virtual void InitImp(IHomeMenuActivityEntryData activityEntryData)
	{
		mActivityEntryData = activityEntryData;
	}
}
}
