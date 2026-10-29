namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.UI;

public class SkillStarInfo : MonoBehaviour
{
    private IProfessionSkillIconBindings mBindings;
    private IProfessionSkillIconBindings Bindings => mBindings ?? ProfessionSkillIconBindings.Required;
    public void Configure(IProfessionSkillIconBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public WorldStateSwitcher<bool> StarStateSwitcher;

	public StarMiniInfo StarMiniInfo;

	public Image StarNumImage;

	public WorldMultiLanguageText StarNum;

	[Header("星星数量阈值，超过该数量则显示星星数字")]
	public int ShowStarThresholdValue;

	public void Show(int rank, bool showStarNum = false, bool showBlankStar = false)
	{
		((Component)this).gameObject.SetActive(true);
		ProfessionSkillRankDefinition skillRankInfo = Bindings.GetRank(rank);
		int rankAddition = skillRankInfo.RankAddition;
		int maxRankByQuality = Bindings.GetMaxRankByQuality(skillRankInfo.Quality);
		showStarNum = showStarNum && rankAddition > ShowStarThresholdValue;
		StarStateSwitcher.SetState(showStarNum);
		if (showStarNum)
		{
			StarNum.Format(rankAddition);
			StarNumImage.sprite = GetQulityStarNumSprite(skillRankInfo.Quality);
		}
		else
		{
			StarMiniInfo.Show(rankAddition, maxRankByQuality, skillRankInfo.Quality, showBlankStar);
		}
	}

	public void ShowStarBlink(int star)
	{
		StarMiniInfo.StopAllBlink();
		StarMiniInfo.TryBlink(star);
	}

	public void StopStarBlink()
	{
		StarMiniInfo.StopAllBlink();
	}

	public void Hide()
	{
		StarMiniInfo.Hide();
	}

	private Sprite GetQulityStarNumSprite(Quality quality)
	{
		return StarMiniInfo.GetQulitySprite(quality);
	}
}

}
