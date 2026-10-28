namespace Zjcs.Official.ProfessionUI
{
using Cysharp.Threading.Tasks;

public interface IStorage
{
	UniTask WriteStateAsync();

	UniTask ReadStateAsync();

	UniTask ClearStateAsync();
}
public interface IStorage<TState> : IStorage
{
	TState State { get; }
}

}
