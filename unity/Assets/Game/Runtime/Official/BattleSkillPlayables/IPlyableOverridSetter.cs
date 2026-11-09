// Migrated original skill-prefab role; see skill-prefab-runtime source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.BattleSkillPlayables
{
public interface IPlyableOverridSetter
{
	void Prepare();

	void OverridePlayable(string tag, GameObject playable);

	GameObject GetOriginPlayable(string tag);

	void DoOverride(PlayableOverrideInfo info);

	MyEntity GetLifecycleEntity();
}

}
