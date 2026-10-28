using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Original HeadCharacterPortrait, reusing the original portrait request system.
    public sealed class TeamHeadCharacterPortrait : CharacterPortraitBase, IProfessionPlayerInfoHead
    {
        [SerializeField] private CharacterPortraitRequestor mRequestor;
        protected override void OnDisable() { mRequestor.StopRequest(); base.OnDisable(); }
        public void GeneratePortrait(long playerId, Sex sex, int profession, object appearanceData,
            bool needPersist = true, ProfessionPortraitKind portraitType = ProfessionPortraitKind.Head)
        {
            long hash = ProfessionPortraitRequestKey.GetLongHashCode(playerId, 0);
            GeneratePortrait(hash, sex, profession, needPersist, appearanceData, portraitType);
        }
        void IProfessionPlayerInfoHead.GeneratePortrait(long id, Sex sex, int profession, object appearance)
        { GeneratePortrait(id, sex, profession, appearance); }
        public void GenerateTemporarilyPortrait(Sex sex, int profession, object appearanceData,
            ProfessionPortraitKind portraitType = ProfessionPortraitKind.Head)
        {
            long hash = ProfessionPortraitRequestKey.GetLongHashCode(appearanceData.GetHashCode(), 1);
            GeneratePortrait(hash, sex, profession, false, appearanceData, portraitType);
        }
        private void GeneratePortrait(long requestId, Sex sex, int profession, bool needPersist,
            object appearanceData, ProfessionPortraitKind portraitType)
        {
            mRequestor.SetRequestInfo(requestId, portraitType, appearanceData, (int)sex, profession, needPersist);
            SetPortraitData(mRequestor);
        }
    }
}
