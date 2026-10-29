// Selected official Common implementation; namespace / C# syntax adaptation only.
namespace Zjcs.Official.PlayerPlay
{

public class PlayerPlayRoomAction : IPlayerPlayAction
{
	public PlayerPlayActionType Type => PlayerPlayActionType.Room;

	public IPlayerPlayParam Param => PlayerPlayUtil.NoneParam;
}
}
