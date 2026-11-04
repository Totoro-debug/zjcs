using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.StoryFollowBone
{
    // Original ApplyOnStopCheckSourceDie. The EC view lookup is supplied by the
    // owning battle scene rather than a global entity container.
    public class ApplyOnStopCheckSourceDie : ApplyOnStop
    {
        public interface ISourceDeathView
        {
            void ForcePlayDiePlayable(GameObject source);
        }

        private ISourceDeathView sourceDeathView;
        public void BindSourceDeathView(ISourceDeathView view) => sourceDeathView = view;

        protected override void HandleOnStop(MyPlayable playable)
        {
            if (playable.mySource == null) return;
            var view = sourceDeathView ?? playable.mySource.GetComponentInChildren<ISourceDeathView>(true);
            view?.ForcePlayDiePlayable(playable.mySource);
        }
    }
}
