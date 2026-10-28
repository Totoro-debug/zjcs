// Migrated official application source; see entity playable view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityViewPlayables
{
[Serializable]
public class ECPlayableBaseHandlerData
{
	[SerializeReference]
	public ECPlayableBaseHandler Handler;
}

}
