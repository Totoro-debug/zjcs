
namespace Zjcs.Official.BattleBgm
{
public interface IBgmManager
{
	PlayBgmHandle Play(string key, BgmEventKeyConfig config);

	void Stop(string key);

	void SetSwitch(string rtpcName, string rtpcValue);

	void EnterDeferStatus();

	void ExitDeferStatus();
}
}
