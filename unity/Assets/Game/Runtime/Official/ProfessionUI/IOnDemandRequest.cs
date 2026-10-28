namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface IOnDemandRequest
{
	bool WasRequested { get; }

	bool WasSuccessfullyLoaded { get; }

	bool IsTarget(Texture texture);

	void Release();
}

}
