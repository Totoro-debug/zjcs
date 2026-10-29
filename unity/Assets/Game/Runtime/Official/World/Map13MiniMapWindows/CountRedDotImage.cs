
namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public class CountRedDotImage : CountRedDotBase
{
	private const int mSingleDigitSize = 26;

	private const int mTenDigitSize = 22;

	public override void SetCount(long count)
	{
		if (count <= 10)
		{
			mCountLanguage.FontSize = 26f;
		}
		else
		{
			mCountLanguage.FontSize = 22f;
		}
		mCountLanguage.SetKey("{0}", Map13WindowLanguage.GetTipNumberWithPlus(count, 99L));
	}
}

}
