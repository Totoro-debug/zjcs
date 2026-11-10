// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class GameRoundPlayerPortrait : MonoBehaviour
{
	[SerializeField]
	private GameRoundPortraitNormalHandler mNormalHandler;

	[SerializeField]
	private GameRoundPortraitETC2Handler mETC2Handler;

	[SerializeField]
	private GameRoundPortraitETC2SceneHandler mETC2SceneHandler;

	private IGameRoundPortraitHandler mCurrentHandler;

	private GamePlayManagerConfig mGamePlayConfig { get; set; }

	public void BindConfiguration(GamePlayManagerConfig config) => mGamePlayConfig = config ?? throw new System.ArgumentNullException(nameof(config));

	private void Awake()
	{
		mCurrentHandler = null;
		if (mGamePlayConfig.EnablePlayerPortraitETC2)
		{
			mCurrentHandler = mETC2Handler;
			if (mGamePlayConfig.EnablePlayerPortraitETC2_Scene)
			{
				mCurrentHandler = mETC2SceneHandler;
			}
		}
		else
		{
			mCurrentHandler = mNormalHandler;
		}
	}

	public void SetPortraitData(long playerId, Sex sex, int profession, object appearanceData)
	{
		if (mCurrentHandler != null)
		{
			mCurrentHandler.SetPortraitData(playerId, sex, profession, appearanceData);
		}
	}

	public void SetPortraitData(Sex sex, int profession, object appearanceData)
	{
		if (mCurrentHandler != null)
		{
			mCurrentHandler.SetPortraitData(sex, profession, appearanceData);
		}
	}

	public void SetPortraitFactionImg(string imgPath)
	{
		if (mCurrentHandler != null)
		{
			mCurrentHandler.SetPortraitFactionImg(imgPath);
		}
	}

	public void SetPortraitColor(Color color)
	{
		if (mCurrentHandler != null)
		{
			mCurrentHandler.SetPortraitColor(color);
		}
	}

	public void SetPortraitActive(bool active)
	{
		if (mCurrentHandler != null)
		{
			mCurrentHandler.SetPortraitActive(active);
		}
	}
}

}
