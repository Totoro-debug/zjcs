using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Official.BattleRoundUI
{
    public sealed class GameSceneObjectOrderLayer : MonoBehaviour
    {
        [SerializeField] private SortingGroup mSortingGroup;
        [SerializeField] private int mStartOrderLayer = 1000;
        private Transform mTransformCache;
        private int mLastSiblingIndex = -1;

        private void Awake() => mTransformCache = transform;

        private void Update()
        {
            if (mTransformCache == null || mSortingGroup == null) return;
            int siblingIndex = mTransformCache.GetSiblingIndex();
            if (siblingIndex == mLastSiblingIndex) return;
            mLastSiblingIndex = siblingIndex;
            mSortingGroup.sortingOrder = mStartOrderLayer + siblingIndex;
        }
    }
}
