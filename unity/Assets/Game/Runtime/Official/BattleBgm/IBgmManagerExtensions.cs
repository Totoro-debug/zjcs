using AK.Wwise;

namespace Zjcs.Official.BattleBgm
{
public static class IBgmManagerExtensions
{
	public static PlayBgmHandle Play(this IBgmManager bgmManager, string key, BgmEventConfig config)
	{
		return bgmManager.Play(key, config.ConvertToBgmEventConfig());
	}

	public static BgmEventKeyConfig ConvertToBgmEventConfig(this BgmEventConfig config)
	{
		return new BgmEventKeyConfig
		{
			PlayEventKey = ((BaseType)config.PlayEvent).Name,
			PauseEventKey = ((BaseType)config.PauseEvent).Name,
			ResumeEventKey = ((BaseType)config.ResumeEvent).Name,
			StopEventKey = ((BaseType)config.StopEvent).Name
		};
	}
}
}
