using UnityEngine;
namespace Zjcs.Official.StoryTimeline
{
public interface EntityConfigBase
{
	EntityConfigType EntityType();

	Transform GetMyEntity();
	void Bind(IStoryTimelineBindings bindings);
}

}
