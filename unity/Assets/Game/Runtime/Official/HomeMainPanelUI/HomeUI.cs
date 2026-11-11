// Original main-panel implementation; namespace/base binding only.
using UnityEngine;
using UnityEngine.UI;
using UIBase = Zjcs.Official.ProfessionUI.UIBase;
namespace Zjcs.Official.HomeMainPanelUI
{

public class HomeUI : MainUIPanelWithAnimator
{
	public GameObject myHomeLeftUI;

	public GameObject myHomeRightUI;

	public Button myFriendBtn;

	public Button myTeamDetailBtn;

	public override ESceneUIGraphicType SceneUIGraphicType => ESceneUIGraphicType.Dialog;
}

}
