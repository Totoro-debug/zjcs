// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System.Collections.Generic;

namespace Zjcs.Official.EntityViews
{
public interface IECView
{
	int Order { get; }

	IECView Append { get; }

	IEnumerable<IECView> AppendMulti { get; }

	void Init(IECViewBridge bridge);

	void Load(ECViewEntityData entity, bool isStatic);

	void OnShow();

	void OnHide();

	void Unload();

	void OnApplicationPause(bool pause);
}

}
