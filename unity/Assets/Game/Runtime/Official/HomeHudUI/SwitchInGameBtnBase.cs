namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class SwitchInGameBtnBase : MonoBehaviour
{
	public IHomeBottomBarEntryServices Services { get; set; }

	public Button SwitchBtn;

	public GameObject GameObject;

	protected long mGameId;

	private void Awake()
	{
		if ((Object)(object)SwitchBtn != (Object)null)
		{
			((UnityEvent)SwitchBtn.onClick).AddListener((UnityAction)HandleOnClick);
		}
	}

	private void OnEnable()
	{
		Services.MainPlayer.OnGameDataAdd += MainPlayer_OnGameDataAdd;
		Services.MainPlayer.OnGameDataRemove += MainPlayer_OnGameDataRemove;
		Refresh();
	}

	private void OnDisable()
	{
		if (Services.MainPlayer != null)
		{
			Services.MainPlayer.OnGameDataAdd -= MainPlayer_OnGameDataAdd;
			Services.MainPlayer.OnGameDataRemove -= MainPlayer_OnGameDataRemove;
		}
	}

	private void MainPlayer_OnGameDataAdd(IHomeHudGameData data)
	{
		if (mGameId <= 0)
		{
			Refresh();
		}
	}

	private void MainPlayer_OnGameDataRemove(long gameId, bool arg2)
	{
		if (mGameId == gameId)
		{
			mGameId = 0L;
			Refresh();
		}
	}

	public void Refresh()
	{
		bool active = HasGameToSwitchIn();
		GameObject.SetActive(active);
	}

	public abstract bool HasGameToSwitchIn();

	private void HandleOnClick()
	{
		Services.SwitchInGame(mGameId);
	}

	public void TriggerClick()
	{
		HandleOnClick();
	}
}

}
