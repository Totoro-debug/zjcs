namespace Zjcs.Official.TreasureUI
{
    // Original detail-window options consumed by an equipped treasure icon.
    public interface ITreasureDetailOptions
    {
        void SetCanOperation(bool canOperation);
        void SetCanShowReplace(bool canShow);
        void SetCanShowNpcTreasureAndState(bool canShow,bool canShowState);
    }
}
