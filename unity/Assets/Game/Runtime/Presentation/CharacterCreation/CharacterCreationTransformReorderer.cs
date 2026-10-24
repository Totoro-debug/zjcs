using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Presentation.CharacterCreation
{
    [ExecuteAlways]
    public sealed class CharacterCreationTransformReorderer : UIBehaviour
    {
        [Serializable]
        private sealed class TransformOrder
        {
            public List<Transform> TransformOrderList = new List<Transform>();
        }

        [SerializeField] private List<TransformOrder> mTransformOrderList = new List<TransformOrder>();
        [SerializeField] private int mCurOrderIndex = 1;

        public void Configure(Transform[][] orders, int currentOrderIndex)
        {
            if (orders == null || currentOrderIndex < 0 || currentOrderIndex >= orders.Length)
                throw new ArgumentOutOfRangeException(nameof(currentOrderIndex));
            mTransformOrderList.Clear();
            foreach (Transform[] order in orders)
                mTransformOrderList.Add(new TransformOrder { TransformOrderList = new List<Transform>(order) });
            mCurOrderIndex = currentOrderIndex;
            ReorderChildNodes();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            ReorderChildNodes();
            base.OnDidApplyAnimationProperties();
        }

        private void ReorderChildNodes()
        {
            if (mTransformOrderList.Count == 0 || mCurOrderIndex < 0 || mCurOrderIndex >= mTransformOrderList.Count) return;
            List<Transform> order = mTransformOrderList[mCurOrderIndex].TransformOrderList;
            if (order == null || order.Count != transform.childCount) return;
            for (int i = 0; i < order.Count; i++)
                if (order[i] == null || order[i].parent != transform) return;
            for (int i = 0; i < order.Count; i++) order[i].SetSiblingIndex(i);
        }
    }
}
