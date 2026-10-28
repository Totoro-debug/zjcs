// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public interface IECViewBridge
{
	IServiceProvider ServiceProvider { get; }

	CancellationToken DestroyCancellationToken { get; }

	GameObject GameObject { get; }

	IClientVisibilitySupporter ClientVisibilitySupporter { get; }

	IEntityFadeAnimationBridge EntityFadeAnimationBridge { get; }

	long StartCoroutine(IEnumerator execution);

	void StopCoroutine(long coroutineId);

	T GetView<T>() where T : class, IECView;
}

}
