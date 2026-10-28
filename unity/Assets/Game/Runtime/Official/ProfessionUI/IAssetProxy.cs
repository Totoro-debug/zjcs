namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;

public interface IAssetProxy<T>
{
	T Load();

	void LoadAsync(Action<T> callback);

	void Unload();
}

}
