// Original serialized minimum-count specialization for the home pet tree.
using UnityEngine;
using Zjcs.Official.HomeTipDots;

namespace Zjcs.Official.HomePetGachaUI
{
    public sealed class GachaBlueDotForSetupMinCount : GachaBlueDotBase
    {
        [SerializeField, Header("显示红点所需的最少抽数")]
        private int mMinCountForShow = 10;

        protected override long GetShowRedDotMinCount(HomeTipGachaInfo info) => mMinCountForShow;
    }
}
