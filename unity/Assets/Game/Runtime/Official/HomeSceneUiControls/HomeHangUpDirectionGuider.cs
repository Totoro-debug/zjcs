// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using UnityEngine;

public class HomeHangUpDirectionGuider : AHomeDirectionGuider
{
	[SerializeField]
	private EFurniture mFurniture;

	private HomePanelDisposableBag mDisposableBag;



	protected override void OnEnable()
	{
		base.OnEnable();
		mDisposableBag = default;
		mDisposableBag.Add(Services.SubscribeHangUpTips(OnTipLevelChange));
		IHomeDirectionFurniture furnitureComponent = Services.GetFurniture(mFurniture);
		OnTipLevelChange(furnitureComponent.TipLevel);
	}

	protected override void OnDisable()
	{
		mDisposableBag.Dispose();
		base.OnDisable();
	}

	protected override Vector3 GetInitPos()
	{
		IHomeDirectionFurniture furnitureComponent = Services.GetFurniture(mFurniture);
		if ((furnitureComponent == null || furnitureComponent.UnityIdentity == null))
		{
			return Vector3.zero;
		}
		return furnitureComponent.transform.position;
	}

	private void OnTipLevelChange(HangUpTipLevelChangeData data)
	{
		if (data.FurnitureType == mFurniture)
		{
			OnTipLevelChange(data.TipLevel);
		}
	}

	private void OnTipLevelChange(int tipLevel)
	{
		if (tipLevel >= 0)
		{
			SetActive(value: true);
			return;
		}
		SetActive(value: false);
		Hide();
	}
}
}
