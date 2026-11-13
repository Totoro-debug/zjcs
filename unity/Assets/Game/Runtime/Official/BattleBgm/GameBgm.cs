using AK.Wwise;
using UnityEngine;

namespace Zjcs.Official.BattleBgm
{
public class GameBgm : MonoBehaviour
{
	[SerializeField]
	private Switch mSwitch;

	[SerializeField]
	[Tooltip("可选：为本战斗指定一套独立的 Event（Play/Stop/Pause/Resume）。留空则使用全局默认的 GameBgmConfig。需与 mSwitch 选择的新组配套。")]
	private GameBgmConfig mBgmConfigOverride;

	public IBgmGameObjectProvider mBgmGameObjectProvider { get; set; }

	public BgmEventConfig BgmEventConfigOverride
	{
		get
		{
			if (!((Object)(object)mBgmConfigOverride != (Object)null))
			{
				return null;
			}
			return mBgmConfigOverride.BgmEventConfig;
		}
	}

	private void OnEnable()
	{
		mSwitch.SetValue(mBgmGameObjectProvider.GameObject);
	}
}
}
