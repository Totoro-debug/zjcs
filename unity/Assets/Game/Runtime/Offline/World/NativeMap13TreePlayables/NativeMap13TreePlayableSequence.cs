using Zjcs.Official.StoryPlayables;

namespace Zjcs.Offline.World.NativeMap13TreePlayables
{
    // The complete official sequence implementation is inherited unchanged.
    // Unity invokes OnValidate while a text prefab's cross-object references
    // are still being imported. Run that same original diagnostic after the
    // import callback, when the complete prefab graph has been resolved.
    public sealed class NativeMap13TreePlayableSequence : MyPlayableSequence
    {
#if UNITY_EDITOR
        private bool validationQueued;
        private void OnValidate()
        {
            if (validationQueued) return;
            validationQueued = true;
            UnityEditor.EditorApplication.delayCall += ValidateOriginalSequence;
        }
        private void ValidateOriginalSequence()
        {
            if (!this) return;
            validationQueued = false;
            typeof(MyPlayableSequence).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(this, null);
        }
#endif
    }
}
