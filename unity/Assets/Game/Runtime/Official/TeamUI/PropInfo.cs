namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;
using UnityEngine.UI;

public class PropInfo : MonoBehaviour
{
	protected ITeamInviteUiServices services;
	public virtual void Configure(ITeamInviteUiServices value) { services = value; }

	[SerializeField]
	private Image mIcon;

	[SerializeField]
	private MultiLanguageText mValue;

	private TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();

	private void OnDisable()
	{
		mDisposableBag.Dispose();
	}

	public void Refresh(PropType propType, long propValue)
	{

		mDisposableBag.Dispose();
		mDisposableBag = new TeamInviteResourceOperationBag();
		string icon = services.PropSmallIcon(propType);
		mIcon.sprite = mDisposableBag.Add(services.Icons.PlayerInfo.Resources.LoadAsset<Sprite>(icon)).Result;
		mValue.SetKey("{0}", services.PropValue(propType, propValue, includeUnit: false, includePropDesc: false, -1L));
	}
}

}
