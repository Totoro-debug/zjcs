// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using UnityEngine;


public class HomeFadeProxy : MonoBehaviour
{
	public HomeFadeList FadeList;

	public HomeFadeList OppositeList;

	public bool Active = true;

	private void Awake()
	{
		FadeList?.Init();
		OppositeList?.Init();
		FadeList?.SetActiveStatusImmediately(Active);
		OppositeList?.SetActiveStatusImmediately(!Active);
	}

	private void OnDestroy()
	{
		ReleaseCancellationTokenSource();
	}

	public void SetActiveStatus(bool isActive)
	{
		if (Active != isActive)
		{
			FadeList?.SetActiveStatus(isActive);
			OppositeList?.SetActiveStatus(!isActive);
			Active = isActive;
		}
	}

	public void ReleaseCancellationTokenSource()
	{
		FadeList?.ReleaseCancellationTokenSource();
		OppositeList?.ReleaseCancellationTokenSource();
	}
}
}
