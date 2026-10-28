// Selected official Common implementation; namespace / C# syntax adaptation only.
namespace Zjcs.Official.PlayerPlay
{

public interface IPlayerPlayAction
{
	PlayerPlayActionType Type { get; }

	IPlayerPlayParam Param { get; }
}
}
