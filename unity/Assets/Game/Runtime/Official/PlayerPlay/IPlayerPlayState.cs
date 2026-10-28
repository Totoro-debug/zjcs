// Selected official Common implementation; namespace / C# syntax adaptation only.
namespace Zjcs.Official.PlayerPlay
{

public interface IPlayerPlayState
{
	PlayerPlayStateType Type { get; }

	IPlayerPlayParam Param { get; }
}
}
