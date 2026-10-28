using System;
using UnityEngine;
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IStoryPlayableInstanceFactory
    {
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
    }
    // Original scene-container lookup and injection must complete before the
    // borrowed object is exposed. Scope owners provide this explicit boundary.
    public static class StoryPlayableInstantiation
    {
        public static Func<Transform,IStoryPlayableInstanceFactory> Resolve { get; set; }
        public static GameObject Instantiate(Transform scope, GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if(Resolve==null)throw new InvalidOperationException("Original playable scene injection is unbound.");
            var factory=Resolve(scope);
            if(factory==null)throw new InvalidOperationException("Original playable scope has no instance factory.");
            return factory.Instantiate(prefab,parent,worldPositionStays);
        }
    }
}
