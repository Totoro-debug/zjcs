using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    [MyPlayableApply(typeof(Renderer))]
    public sealed class MyPlayableRenderSortLayer : MyPlayable
    {
        [SerializeField]
        private string mSortLayerName;

        [SerializeField]
        private bool mNeedSetSortOrder;

        [SerializeField]
        private int mSortOrder;

        protected override void PlayImpl()
        {
            base.PlayImpl();
            if (!(myApplyComponent is Renderer renderer)) return;
            if (!string.IsNullOrEmpty(mSortLayerName)) renderer.sortingLayerName = mSortLayerName;
            if (mNeedSetSortOrder) renderer.sortingOrder = mSortOrder;
        }

        public override float GetDuration() => -1f;
    }
}
