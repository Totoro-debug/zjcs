using Object = UnityEngine.Object;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
public abstract class PreviewableProxyBase<TLoader, TData, TDataProxy>
{
	[SerializeField]
	protected TLoader mLoader;

	[SerializeField]
	private TDataProxy mDataProxy;

	protected abstract void Load(TData data);
}

}
