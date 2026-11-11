// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class HomeScreenShootActive : MonoBehaviour
{
	public IHomeSceneUiServices Services { private get; set; }

	public HomeFadeProxy HomeFadeProxy;

	public List<HomeViewMode> ViewModes;

	private HomePanelDisposableBag mDisposable;

	private Action<SetHomeSceneUIActiveData> mOnActive;



	private void Awake()
	{
		mOnActive = OnActive;
	}

	private void OnEnable()
	{
		mDisposable = default;
		mDisposable.Add(Services.SubscribeUiActive(mOnActive));
	}

	private void OnDisable()
	{
		mDisposable.Dispose();
	}

	private void OnActive(SetHomeSceneUIActiveData data)
	{
		if (ViewModes.Count <= 0 || ViewModes.Contains(Services.HomeViewMode))
		{
			HomeFadeProxy.SetActiveStatus(data.IsActive);
		}
	}
}
}
