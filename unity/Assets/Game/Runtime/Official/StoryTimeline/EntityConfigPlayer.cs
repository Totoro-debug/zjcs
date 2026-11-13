using UnityEngine;
using System;

namespace Zjcs.Official.StoryTimeline
{
[Serializable]
public class EntityConfigPlayer : EntityConfigBase
{
	public EntityConfigType EntityType()
	{
		return EntityConfigType.MapPlayer;
	}

private IStoryTimelineBindings bindings;
    public void Bind(IStoryTimelineBindings value) => bindings = value;
    public Transform GetMyEntity() => bindings.PlayerTransform;
}

}
