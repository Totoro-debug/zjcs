// Selected official Common implementation; namespace / C# syntax adaptation only.
namespace Zjcs.Official.PlayerPlay
{

public class PlayerPlayTeamAction : IPlayerPlayAction
{
	public PlayerPlayActionType Type => PlayerPlayActionType.Team;

	public IPlayerPlayParam Param => PlayerPlayUtil.NoneParam;
}
}
